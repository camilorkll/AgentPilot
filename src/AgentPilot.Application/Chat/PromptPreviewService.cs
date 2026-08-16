using System.Text;
using AgentPilot.Application.Abstractions;
using AgentPilot.Application.Retrieval;
using AgentPilot.Domain.Campaigns;
using AgentPilot.Domain.Conversations;

namespace AgentPilot.Application.Chat;

/// <summary>
/// Orquestación de la vista previa de prompts: recupera el contexto UNA sola vez y
/// genera dos respuestas sobre él (instrucciones publicadas vs. candidato), para
/// que la comparación sea justa y no dependa de qué chunks devolvió cada búsqueda.
///
/// Depende solo de <see cref="IEmbeddingService"/>, <see cref="IChunkSearchService"/>
/// y <see cref="IChatCompletionService"/> — a propósito NO de
/// <see cref="IConversationRepository"/> ni <see cref="IMetricsRepository"/>: una
/// previsualización no es tráfico real y no debe crear una conversación ni
/// contaminar el dashboard de coste con llamadas de prueba de un administrador.
/// </summary>
public class PromptPreviewService(
    IEmbeddingService embeddings,
    IChunkSearchService search,
    IChatCompletionService chat,
    ICampaignRepository campaigns) : IPromptPreviewService
{
    // Mismos valores que AskQuestionService, y por el mismo motivo que se comparte
    // SystemPromptBuilder (ADR-011): si la vista previa recuperase distinto que el chat
    // real, lo que el administrador previsualiza dejaría de ser lo que se publica.
    private const int TopK = 10;
    private const int Candidatos = 30;

    public async Task<PromptPreviewResult> PreviewAsync(
        Guid campaignId, AssistantPromptSettings candidateSettings, string question,
        CancellationToken cancellationToken = default)
    {
        var campaign = await campaigns.GetByIdAsync(campaignId, cancellationToken)
            ?? throw new KeyNotFoundException($"La campaña {campaignId} no existe.");

        var queryVector = await embeddings.EmbedAsync(question, cancellationToken);
        var candidatos = await search.SearchAsync(queryVector, campaign.Id, Candidatos, cancellationToken);
        var matches = Retrieval.ChunkReranker.Rerank(candidatos, question, TopK);

        var citations = matches
            .Select(m => new Citation(m.DocumentId, m.DocumentTitle, m.ChunkId, m.Content, m.Score))
            .ToList();

        var currentAnswer = await GenerateAsync(campaign.AssistantPrompt, question, matches, cancellationToken);
        var candidateAnswer = await GenerateAsync(candidateSettings, question, matches, cancellationToken);

        return new PromptPreviewResult(
            currentAnswer, candidateAnswer, citations, candidateSettings.AdviertePatronesSospechosos());
    }

    private async Task<string> GenerateAsync(
        AssistantPromptSettings settings, string question, IReadOnlyList<ChunkMatch> matches,
        CancellationToken cancellationToken)
    {
        var messages = new List<PromptMessage>
        {
            new(PromptRole.System, SystemPromptBuilder.Build(settings)),
            new(PromptRole.User, ContextBlockBuilder.Build(matches) + "\n\nPregunta del agente: " + question),
        };

        var answer = new StringBuilder();
        await foreach (var chunk in chat.StreamAsync(messages, cancellationToken))
            if (chunk.TextDelta is { Length: > 0 } delta)
                answer.Append(delta);

        return answer.ToString();
    }
}
