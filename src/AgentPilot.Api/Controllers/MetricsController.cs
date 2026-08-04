using System.Text;
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
    /// Resumen de uso, calidad y coste (LLMOps), con desglose por operador y la matriz
    /// día×operador que alimenta las dos vistas del panel. Se puede limitar a uno o
    /// varios operadores repitiendo el parámetro <c>operator</c>, a un rango de meses
    /// con <c>monthFrom</c>/<c>monthTo</c> y a una campaña con <c>campaignId</c>.
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<MetricsSummary>> GetSummary(
        [FromQuery] string? monthFrom,
        [FromQuery] string? monthTo,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery(Name = "operator")] string[]? @operator,
        [FromQuery] string? campaignId,
        CancellationToken cancellationToken)
    {
        CampaignFilter campaignFilter;
        try
        {
            campaignFilter = CampaignFilter.Parse(campaignId);
        }
        catch (FormatException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }

        DateTime? fromUtc, toUtc;
        string? monthFromLabel, monthToLabel;
        try
        {
            (fromUtc, toUtc, monthFromLabel, monthToLabel) = await ResolveRangeAsync(
                monthFrom, monthTo, from, to, cancellationToken);
        }
        catch (FormatException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }

        var summary = await metrics.GetSummaryAsync(
            fromUtc, toUtc, @operator, campaignFilter, monthFromLabel, monthToLabel, cancellationToken);
        return summary;
    }

    /// <summary>Operadores que han usado el copiloto, para poblar el filtro.</summary>
    [HttpGet("operators")]
    public Task<IReadOnlyList<string>> GetOperators(CancellationToken cancellationToken)
        => metrics.GetOperatorsAsync(cancellationToken);

    /// <summary>
    /// Exporta a CSV la misma matriz día×operador que <see cref="GetSummary"/>, con
    /// exactamente los mismos parámetros: reutiliza la consulta, así que «respetar los
    /// filtros aplicados» es cierto por construcción.
    /// </summary>
    [HttpGet("export.csv")]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] string? monthFrom,
        [FromQuery] string? monthTo,
        [FromQuery(Name = "operator")] string[]? @operator,
        [FromQuery] string? campaignId,
        CancellationToken cancellationToken)
    {
        CampaignFilter campaignFilter;
        try
        {
            campaignFilter = CampaignFilter.Parse(campaignId);
        }
        catch (FormatException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }

        DateTime? fromUtc, toUtc;
        string? monthFromLabel, monthToLabel;
        try
        {
            (fromUtc, toUtc, monthFromLabel, monthToLabel) = await ResolveRangeAsync(
                monthFrom, monthTo, from: null, to: null, cancellationToken);
        }
        catch (FormatException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }

        var rows = await metrics.GetDailyByOperatorAsync(
            fromUtc, toUtc, @operator, campaignFilter, cancellationToken);

        var csv = BuildCsv(rows);
        // ResolveRangeAsync siempre resuelve un mes concreto cuando no se le pasan
        // from/to heredados (como aquí), así que las etiquetas nunca llegan nulas.
        var fileName = $"metricas-{monthFromLabel}" +
            (monthFromLabel == monthToLabel ? "" : $"_a_{monthToLabel}") + ".csv";

        // BOM UTF-8 explícito: sin él, Excel en español abre el fichero con los
        // acentos rotos ("Averías" como "AverÃ­as") al hacer doble clic.
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
        return File(bytes, "text/csv", fileName);
    }

    /// <summary>
    /// Traduce los parámetros de consulta a un rango de instantes UTC. Con
    /// <c>monthFrom</c>/<c>monthTo</c> (o sin ningún parámetro, que por defecto es el
    /// mes en curso) se resuelve en Europe/Madrid; con los <c>from</c>/<c>to</c>
    /// heredados —solo disponibles en <c>/summary</c>— se respeta el rango exacto tal
    /// como llega, sin agrupar por mes, para no romper a quien ya los use.
    /// </summary>
    private async Task<(DateTime? FromUtc, DateTime? ToUtc, string? MonthFrom, string? MonthTo)> ResolveRangeAsync(
        string? monthFrom, string? monthTo, DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        if (monthFrom is null && monthTo is null && (from is not null || to is not null))
            return (from, to, null, null);

        Month.Validate(monthFrom, "monthFrom");
        Month.Validate(monthTo, "monthTo");
        if (Month.IsInvertedRange(monthFrom, monthTo))
            throw new FormatException("monthTo no puede ser anterior a monthFrom.");

        var range = await metrics.ResolveMonthRangeAsync(monthFrom, monthTo, cancellationToken);
        return (range.FromUtc, range.ToUtcExclusive, range.MonthFrom, range.MonthTo);
    }

    private static string BuildCsv(IReadOnlyList<DailyOperatorUsage> rows)
    {
        var sb = new StringBuilder();
        // Separador ';' y coma decimal: es lo que Excel en español espera al abrir un
        // CSV a doble clic sin pasar por el asistente de importación.
        sb.AppendLine("Mes;Fecha;Operador;Preguntas;Coste USD;Latencia media ms;% útiles");

        foreach (var row in rows)
        {
            var month = row.Date.ToString("yyyy-MM");
            var date = row.Date.ToString("yyyy-MM-dd");
            var cost = row.CostUsd.ToString("F6").Replace('.', ',');
            var latency = row.AvgLatencyMs.ToString("F1").Replace('.', ',');
            var useful = row.PositiveFeedbackRate is { } rate
                ? (rate * 100).ToString("F0").Replace('.', ',') : "";

            sb.AppendLine(
                $"{month};{date};{CsvField(row.UserName)};{row.Questions};{cost};{latency};{useful}");
        }

        return sb.ToString();
    }

    /// <summary>Escapa un campo de texto libre si contiene el separador, comillas o salto de línea.</summary>
    private static string CsvField(string value) =>
        value.Contains(';') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
