using AgentPilot.Domain.Campaigns;
using AgentPilot.Domain.Conversations;

namespace AgentPilot.Application.Chat;

/// <summary>
/// Respuestas de prueba para el mismo contexto recuperado: con las instrucciones
/// publicadas de la campaña y con un candidato que todavía no se ha guardado. Deja
/// comparar antes de publicar, sin tocar conversaciones ni métricas.
/// </summary>
public record PromptPreviewResult(
    string CurrentAnswer,
    string CandidateAnswer,
    IReadOnlyList<Citation> Citations,
    IReadOnlyList<string> Warnings);

public interface IPromptPreviewService
{
    /// <summary>
    /// Lanza <see cref="KeyNotFoundException"/> si la campaña no existe. No exige que
    /// esté activa: previsualizar es de solo lectura y sirve también para preparar el
    /// prompt de una campaña que todavía no se ha reactivado.
    /// </summary>
    Task<PromptPreviewResult> PreviewAsync(
        Guid campaignId, AssistantPromptSettings candidateSettings, string question,
        CancellationToken cancellationToken = default);
}
