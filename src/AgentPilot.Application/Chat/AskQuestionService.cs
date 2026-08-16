using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using AgentPilot.Application.Abstractions;
using AgentPilot.Application.Campaigns;
using AgentPilot.Application.Retrieval;
using AgentPilot.Domain.Campaigns;
using AgentPilot.Domain.Conversations;

namespace AgentPilot.Application.Chat;

/// <summary>
/// Corazón del RAG: embebe la pregunta, recupera los chunks más relevantes,
/// construye un prompt "grounded" (con citas y defensa anti prompt-injection),
/// genera la respuesta en streaming y persiste la conversación.
/// </summary>
public class AskQuestionService(
    IEmbeddingService embeddings,
    IChunkSearchService search,
    IChatCompletionService chat,
    IConversationRepository conversations,
    IMetricsRepository metrics,
    ICurrentUser currentUser,
    CampaignGuard campaigns) : IAskQuestionService
{
    private const int TopK = 5;

    public async IAsyncEnumerable<AskEvent> AskAsync(
        string question, Guid campaignId, Guid? conversationId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // 1. La campaña debe existir y estar activa. Se comprueba en cada pregunta, no
        //    solo al poblar el selector: una campaña desactivada a media sesión tiene
        //    que dejar de responder sin esperar a que el agente recargue la página.
        var campaign = await campaigns.ExigirActivaAsync(campaignId, cancellationToken);

        // 2. Cargar o crear la conversación y registrar la pregunta del usuario.
        Conversation conversation;
        if (conversationId is Guid id)
        {
            conversation = await conversations.GetByIdAsync(id, cancellationToken)
                ?? throw new KeyNotFoundException($"Conversación {id} no encontrada.");

            // Manda la campaña de la conversación, no la que venga en la petición: el
            // historial se reenvía al modelo en cada turno, así que responder con otra
            // campaña filtraría contenido de la anterior.
            if (conversation.CampaignId != campaign.Id)
                throw new CampaignMismatchException(
                    conversation.Id, conversation.CampaignId, campaign.Id);
        }
        else
        {
            conversation = new Conversation(campaign.Id, currentUser.UserName);
            await conversations.AddAsync(conversation, cancellationToken);
        }
        conversation.AddUserMessage(question);
        await conversations.SaveChangesAsync(cancellationToken);

        // 3. Recuperación: embeber la pregunta y buscar los chunks más cercanos, solo
        //    dentro de la campaña.
        var queryVector = await embeddings.EmbedAsync(question, cancellationToken);
        var matches = await search.SearchAsync(queryVector, campaign.Id, TopK, cancellationToken);

        var citations = matches
            .Select(m => new Citation(m.DocumentId, m.DocumentTitle, m.ChunkId, m.Content, m.Score))
            .ToList();

        // Las fuentes se emiten en cuanto se recuperan, sin esperar al modelo: el agente
        // ve de inmediato en qué documentos se va a basar la respuesta mientras el LLM
        // todavía está generando (que es la parte lenta).
        yield return new CitationsEvent(citations);

        // 3. Construir el prompt "grounded" (system + historial + contexto + pregunta).
        var messages = BuildPrompt(campaign.AssistantPrompt, conversation, question, matches);

        // 4. Generar en streaming, reenviando cada token y acumulando la respuesta.
        var answer = new StringBuilder();
        ChatUsage? usage = null;
        var stopwatch = Stopwatch.StartNew();

        await foreach (var chunk in chat.StreamAsync(messages, cancellationToken))
        {
            if (chunk.TextDelta is { Length: > 0 } delta)
            {
                answer.Append(delta);
                yield return new TokenEvent(delta);
            }
            if (chunk.Usage is not null)
                usage = chunk.Usage;
        }
        stopwatch.Stop();

        // 5. Emitir la telemetría de uso (las citas ya se enviaron antes de generar).
        var promptTokens = usage?.PromptTokens ?? 0;
        var completionTokens = usage?.CompletionTokens ?? 0;
        var costUsd = LlmPricing.EstimateUsd(chat.ModelName, promptTokens, completionTokens);
        var latencyMs = stopwatch.ElapsedMilliseconds;
        yield return new UsageEvent(chat.ModelName, promptTokens, completionTokens, costUsd, latencyMs);

        // 6. Persistir la respuesta del asistente con sus citas.
        conversation.AddAssistantMessage(answer.ToString(), citations);
        await conversations.SaveChangesAsync(cancellationToken);

        // 7. Registrar la llamada para el dashboard de coste (LLMOps).
        await metrics.RecordCallAsync(
            new Domain.Telemetry.LlmCallLog(
                chat.ModelName, promptTokens, completionTokens, costUsd, latencyMs,
                conversation.Id, currentUser.UserName, campaign.Id, campaign.Name),
            cancellationToken);

        yield return new DoneEvent(conversation.Id);
    }

    private static List<PromptMessage> BuildPrompt(
        AssistantPromptSettings campaignPrompt,
        Conversation conversation, string question, IReadOnlyList<ChunkMatch> matches)
    {
        var messages = new List<PromptMessage> { new(PromptRole.System, SystemPromptBuilder.Build(campaignPrompt)) };

        // Historial previo (todos los mensajes menos la pregunta actual, ya añadida).
        foreach (var previous in conversation.Messages.Take(conversation.Messages.Count - 1))
            messages.Add(new PromptMessage(
                previous.Role == MessageRole.Assistant ? PromptRole.Assistant : PromptRole.User,
                previous.Content));

        // Mensaje final: contexto recuperado + la pregunta.
        messages.Add(new PromptMessage(PromptRole.User,
            ContextBlockBuilder.Build(matches) + "\n\nPregunta del agente: " + question));

        return messages;
    }
}
