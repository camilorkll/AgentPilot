using AgentPilot.Application.Metrics;
using AgentPilot.Domain.Telemetry;

namespace AgentPilot.Application.Abstractions;

public interface IMetricsRepository
{
    /// <summary>Registra una llamada al LLM (para el dashboard de coste).</summary>
    Task RecordCallAsync(LlmCallLog log, CancellationToken cancellationToken = default);

    /// <summary>Operadores que han usado el copiloto (para poblar el filtro).</summary>
    Task<IReadOnlyList<string>> GetOperatorsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resuelve <paramref name="monthFrom"/>/<paramref name="monthTo"/> —con los valores
    /// por defecto del plan: ambos nulos = mes en curso; solo el inicial = desde él hasta
    /// el mes en curso— a un rango de instantes UTC. Se calcula en SQL porque PostgreSQL
    /// conoce la zona horaria "Europe/Madrid"; hacerlo en .NET dependería del tzdata del
    /// sistema operativo del contenedor, que no conviene dar por hecho.
    /// </summary>
    Task<MonthRange> ResolveMonthRangeAsync(
        string? monthFrom, string? monthTo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Agrega uso, coste, latencia y feedback en el rango indicado, con desglose por
    /// operador, por día×operador y por mes. Si se indican operadores, el resumen se
    /// limita a ellos; <paramref name="monthFromLabel"/>/<paramref name="monthToLabel"/>
    /// son solo para que el informe refleje qué meses se aplicaron, no filtran nada por
    /// sí mismos (eso ya lo hacen <paramref name="fromUtc"/>/<paramref name="toUtc"/>).
    /// </summary>
    Task<MetricsSummary> GetSummaryAsync(
        DateTime? fromUtc, DateTime? toUtc,
        IReadOnlyList<string>? operators = null,
        CampaignFilter campaignFilter = default,
        string? monthFromLabel = null, string? monthToLabel = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Solo la matriz día×operador. La usa <see cref="GetSummaryAsync"/> para su campo
    /// <c>DailyByOperator</c> y también la exportación CSV, que reutiliza esta misma
    /// consulta para que «respetar los filtros aplicados» sea cierto por construcción y
    /// no algo que haya que mantener sincronizado en dos sitios.
    /// </summary>
    Task<IReadOnlyList<DailyOperatorUsage>> GetDailyByOperatorAsync(
        DateTime? fromUtc, DateTime? toUtc,
        IReadOnlyList<string>? operators = null,
        CampaignFilter campaignFilter = default,
        CancellationToken cancellationToken = default);
}
