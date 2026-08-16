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
    /// <summary>
    /// Fragmentos que acaban en el contexto del modelo. Subió de 5 a 10 al trocear por
    /// estructura (ADR-016): los fragmentos pasaron de ~1.000 a ~400 caracteres, así que
    /// mantener 5 habría recortado el contexto a menos de la mitad. Medido: con 5 se
    /// perdían dos casos del set dorado que antes acertaba.
    /// </summary>
    private const int TopK = 10;

    /// <summary>
    /// Candidatos que se traen de la búsqueda vectorial antes de reordenar. Se pide de
    /// más para que el reordenado tenga margen: si el fragmento bueno no entra en el
    /// pool, ningún reordenado lo rescata.
    /// </summary>
    private const int Candidatos = 30;

    /// <summary>
    /// Mensajes anteriores que se reenvían al modelo: los 3 últimos intercambios
    /// (pregunta + respuesta). La conversación se sigue guardando ENTERA; esto solo
    /// acota lo que viaja en el prompt.
    ///
    /// Sin este límite el prompt crecía con cada turno, y con él el coste: medido sobre
    /// una conversación real, 1.358 → 1.535 → 1.626 tokens en tres turnos. Un agente que
    /// encadene preguntas durante horas acabaría (a) pagando por pregunta una cifra que
    /// crece sin fin, (b) agotando la ventana de contexto —con Ollama a 4.096 tokens eso
    /// llega hacia el turno 18, y el truncado es SILENCIOSO— y (c) diluyendo lo relevante
    /// entre horas de charla ajena a la pregunta actual.
    ///
    /// Tres intercambios bastan para la continuidad real ("¿y de la otra tarifa?"). Lo de
    /// hace dos horas es de otro cliente y no debe influir en esta respuesta.
    /// </summary>
    private const int MensajesDeHistorial = 6;

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
        var candidatos = await search.SearchAsync(queryVector, campaign.Id, Candidatos, cancellationToken);

        // Reordenado local (sin llamada al LLM): la búsqueda vectorial acierta el tema
        // pero no siempre pone delante el fragmento que contiene el dato concreto.
        var matches = ChunkReranker.Rerank(candidatos, question, TopK);

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

        // Historial previo, acotado a los últimos turnos: se descarta la pregunta actual
        // (ya añadida a la conversación) y de lo anterior solo viajan los más recientes.
        var previos = conversation.Messages
            .Take(conversation.Messages.Count - 1)
            .TakeLast(MensajesDeHistorial);

        foreach (var previous in previos)
            messages.Add(new PromptMessage(
                previous.Role == MessageRole.Assistant ? PromptRole.Assistant : PromptRole.User,
                previous.Content));

        // Mensaje final: contexto recuperado + la pregunta.
        messages.Add(new PromptMessage(PromptRole.User,
            ContextBlockBuilder.Build(matches) + "\n\nPregunta del agente: " + question));

        return messages;
    }
}
