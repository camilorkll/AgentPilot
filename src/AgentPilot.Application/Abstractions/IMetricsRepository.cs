using AgentPilot.Application.Metrics;
using AgentPilot.Domain.Telemetry;

namespace AgentPilot.Application.Abstractions;

public interface IMetricsRepository
{
    /// <summary>Registra una llamada al LLM (para el dashboard de coste).</summary>
    Task RecordCallAsync(LlmCallLog log, CancellationToken cancellationToken = default);

    /// <summary>Agrega uso, coste, latencia y feedback en el rango indicado.</summary>
    Task<MetricsSummary> GetSummaryAsync(
        DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default);
}
