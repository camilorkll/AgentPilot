using AgentPilot.Application.Abstractions;
using AgentPilot.Application.Metrics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentPilot.Api.Controllers;

[ApiController]
[Route("api/v1/metrics")]
[Authorize(Roles = "admin")]
public class MetricsController(IMetricsRepository metrics) : ControllerBase
{
    /// <summary>
    /// Resumen de uso, calidad y coste (LLMOps), con desglose por operador.
    /// Se puede limitar a uno o varios operadores repitiendo el parámetro
    /// <c>operator</c> (p. ej. <c>?operator=agente&amp;operator=admin</c>).
    /// </summary>
    [HttpGet("summary")]
    public Task<MetricsSummary> GetSummary(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery(Name = "operator")] string[]? @operator,
        CancellationToken cancellationToken)
        => metrics.GetSummaryAsync(from, to, @operator, cancellationToken);

    /// <summary>Operadores que han usado el copiloto, para poblar el filtro.</summary>
    [HttpGet("operators")]
    public Task<IReadOnlyList<string>> GetOperators(CancellationToken cancellationToken)
        => metrics.GetOperatorsAsync(cancellationToken);
}
