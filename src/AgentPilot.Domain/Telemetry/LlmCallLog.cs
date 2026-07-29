namespace AgentPilot.Domain.Telemetry;

/// <summary>
/// Registro de una llamada al LLM: tokens, coste estimado y latencia. Es la
/// materia prima del dashboard de observabilidad y control de coste (LLMOps).
/// </summary>
public class LlmCallLog
{
    public Guid Id { get; private set; }
    public Guid? ConversationId { get; private set; }
    public string Model { get; private set; } = string.Empty;

    /// <summary>Operador que hizo la consulta, para el desglose por agente del dashboard.</summary>
    public string? UserName { get; private set; }

    public int PromptTokens { get; private set; }
    public int CompletionTokens { get; private set; }
    public double EstimatedCostUsd { get; private set; }
    public long LatencyMs { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private LlmCallLog() { } // EF

    public LlmCallLog(
        string model, int promptTokens, int completionTokens,
        double estimatedCostUsd, long latencyMs, Guid? conversationId, string? userName = null)
    {
        Id = Guid.NewGuid();
        Model = model;
        PromptTokens = promptTokens;
        CompletionTokens = completionTokens;
        EstimatedCostUsd = estimatedCostUsd;
        LatencyMs = latencyMs;
        ConversationId = conversationId;
        UserName = userName;
        CreatedAtUtc = DateTime.UtcNow;
    }
}
