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
    /// <summary>Resumen de uso, calidad y coste (LLMOps). Solo administradores.</summary>
    [HttpGet("summary")]
    public Task<MetricsSummary> GetSummary(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken)
        => metrics.GetSummaryAsync(from, to, cancellationToken);
}
