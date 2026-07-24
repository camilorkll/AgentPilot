namespace AgentPilot.Application.Chat;

/// <summary>
/// Orquestación RAG de una pregunta: embeber → buscar → prompt con citas →
/// generar en streaming. Devuelve un flujo de eventos para el endpoint SSE.
/// </summary>
public interface IAskQuestionService
{
    IAsyncEnumerable<AskEvent> AskAsync(
        string question, Guid? conversationId, CancellationToken cancellationToken = default);
}
