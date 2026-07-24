namespace AgentPilot.Application.Metrics;

/// <summary>Preguntas atendidas en un día concreto.</summary>
public sealed record QuestionsPerDay(DateOnly Date, int Count);

/// <summary>
/// Resumen agregado de uso, calidad y coste (esquema MetricsSummary del OpenAPI).
/// </summary>
public sealed record MetricsSummary
{
    public int TotalQuestions { get; init; }

    /// <summary>Ratio de feedback positivo (0..1); null si aún no hay valoraciones.</summary>
    public double? PositiveFeedbackRate { get; init; }

    public double AvgLatencyMs { get; init; }
    public double P95LatencyMs { get; init; }
    public double TotalCostUsd { get; init; }
    public IReadOnlyDictionary<string, double> CostByModel { get; init; } = new Dictionary<string, double>();
    public IReadOnlyList<QuestionsPerDay> QuestionsPerDay { get; init; } = [];
}
