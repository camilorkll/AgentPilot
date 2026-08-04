namespace AgentPilot.Application.Chat;

/// <summary>
/// Orquestación RAG de una pregunta: embeber → buscar → prompt con citas →
/// generar en streaming. Devuelve un flujo de eventos para el endpoint SSE.
/// </summary>
public interface IAskQuestionService
{
    /// <summary>
    /// Responde con el corpus de <paramref name="campaignId"/> y solo con él. La campaña
    /// es obligatoria y no tiene valor por defecto.
    ///
    /// Si <paramref name="conversationId"/> viene informado, manda la campaña de esa
    /// conversación: una campaña distinta lanza <see cref="Campaigns.CampaignMismatchException"/>,
    /// porque cambiar de campaña exige empezar otra conversación.
    /// </summary>
    IAsyncEnumerable<AskEvent> AskAsync(
        string question, Guid campaignId, Guid? conversationId,
        CancellationToken cancellationToken = default);
}
