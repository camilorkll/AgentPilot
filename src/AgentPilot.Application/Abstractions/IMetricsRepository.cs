using AgentPilot.Application.Metrics;
using AgentPilot.Domain.Telemetry;

namespace AgentPilot.Application.Abstractions;

public interface IMetricsRepository
{
    /// <summary>Registra una llamada al LLM (para el dashboard de coste).</summary>
    Task RecordCallAsync(LlmCallLog log, CancellationToken cancellationToken = default);

    /// <summary>
    /// Agrega uso, coste, latencia y feedback en el rango indicado, con desglose por
    /// operador. Si se indican operadores, el resumen se limita a ellos.
    /// </summary>
    Task<MetricsSummary> GetSummaryAsync(
        DateTime? fromUtc, DateTime? toUtc,
        IReadOnlyList<string>? operators = null, CancellationToken cancellationToken = default);

    /// <summary>Operadores que han usado el copiloto (para poblar el filtro).</summary>
    Task<IReadOnlyList<string>> GetOperatorsAsync(CancellationToken cancellationToken = default);
}
