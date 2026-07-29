using AgentPilot.Application.Abstractions;
using AgentPilot.Application.Metrics;
using AgentPilot.Domain.Conversations;
using AgentPilot.Domain.Telemetry;
using Microsoft.EntityFrameworkCore;

namespace AgentPilot.Infrastructure.Persistence;

public class MetricsRepository(AgentPilotDbContext db) : IMetricsRepository
{
    public async Task RecordCallAsync(LlmCallLog log, CancellationToken cancellationToken = default)
    {
        await db.LlmCallLogs.AddAsync(log, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetOperatorsAsync(CancellationToken cancellationToken = default)
        => await db.LlmCallLogs
            .Where(l => l.UserName != null)
            .Select(l => l.UserName!)
            .Distinct()
            .OrderBy(u => u)
            .ToListAsync(cancellationToken);

    public async Task<MetricsSummary> GetSummaryAsync(
        DateTime? fromUtc, DateTime? toUtc,
        IReadOnlyList<string>? operators = null, CancellationToken cancellationToken = default)
    {
        var selected = operators?.Where(o => !string.IsNullOrWhiteSpace(o)).ToList() ?? [];

        var logs = db.LlmCallLogs.AsQueryable();
        if (fromUtc is not null) logs = logs.Where(l => l.CreatedAtUtc >= fromUtc);
        if (toUtc is not null) logs = logs.Where(l => l.CreatedAtUtc <= toUtc);
        if (selected.Count > 0) logs = logs.Where(l => l.UserName != null && selected.Contains(l.UserName));

        var total = await logs.CountAsync(cancellationToken);
        if (total == 0)
            return new MetricsSummary { FilteredOperators = selected };

        var avgLatency = await logs.AverageAsync(l => (double)l.LatencyMs, cancellationToken);
        var totalCost = await logs.SumAsync(l => l.EstimatedCostUsd, cancellationToken);

        var costByModel = await logs
            .GroupBy(l => l.Model)
            .Select(g => new { Model = g.Key, Cost = g.Sum(l => l.EstimatedCostUsd) })
            .ToListAsync(cancellationToken);

        var perDay = await logs
            .GroupBy(l => l.CreatedAtUtc.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .OrderBy(x => x.Day)
            .ToListAsync(cancellationToken);

        // p95 en memoria: para el volumen del MVP es suficiente. A gran escala se
        // empujaría a SQL con percentile_cont(0.95) WITHIN GROUP (...).
        var latencies = await logs.Select(l => l.LatencyMs).OrderBy(x => x).ToListAsync(cancellationToken);
        var p95Index = (int)Math.Ceiling(latencies.Count * 0.95) - 1;
        var p95 = latencies[Math.Clamp(p95Index, 0, latencies.Count - 1)];

        // Feedback del mismo rango (y operadores, si se filtró).
        var feedback = db.Feedback.AsQueryable();
        if (fromUtc is not null) feedback = feedback.Where(f => f.CreatedAtUtc >= fromUtc);
        if (toUtc is not null) feedback = feedback.Where(f => f.CreatedAtUtc <= toUtc);
        if (selected.Count > 0) feedback = feedback.Where(f => f.CreatedBy != null && selected.Contains(f.CreatedBy));

        var feedbackTotal = await feedback.CountAsync(cancellationToken);
        var feedbackPositive = await feedback.CountAsync(f => f.Rating == FeedbackRating.Positive, cancellationToken);

        // Desglose por operador: uso y coste de la telemetría, satisfacción del feedback.
        var usageByOperator = await logs
            .Where(l => l.UserName != null)
            .GroupBy(l => l.UserName!)
            .Select(g => new
            {
                UserName = g.Key,
                Questions = g.Count(),
                Cost = g.Sum(l => l.EstimatedCostUsd),
                AvgLatency = g.Average(l => (double)l.LatencyMs),
            })
            .ToListAsync(cancellationToken);

        var feedbackByOperator = await feedback
            .Where(f => f.CreatedBy != null)
            .GroupBy(f => f.CreatedBy!)
            .Select(g => new
            {
                UserName = g.Key,
                Total = g.Count(),
                Positive = g.Count(f => f.Rating == FeedbackRating.Positive),
            })
            .ToListAsync(cancellationToken);

        var byOperator = usageByOperator
            .OrderByDescending(u => u.Questions)
            .Select(u =>
            {
                var fb = feedbackByOperator.FirstOrDefault(f => f.UserName == u.UserName);
                return new OperatorUsage(
                    u.UserName,
                    u.Questions,
                    Math.Round(u.Cost, 6),
                    Math.Round(u.AvgLatency, 1),
                    fb is { Total: > 0 } ? (double)fb.Positive / fb.Total : null);
            })
            .ToList();

        return new MetricsSummary
        {
            TotalQuestions = total,
            PositiveFeedbackRate = feedbackTotal > 0 ? (double)feedbackPositive / feedbackTotal : null,
            AvgLatencyMs = Math.Round(avgLatency, 1),
            P95LatencyMs = p95,
            TotalCostUsd = Math.Round(totalCost, 6),
            CostByModel = costByModel.ToDictionary(x => x.Model, x => Math.Round(x.Cost, 6)),
            QuestionsPerDay = perDay
                .Select(x => new QuestionsPerDay(DateOnly.FromDateTime(x.Day), x.Count))
                .ToList(),
            ByOperator = byOperator,
            FilteredOperators = selected,
        };
    }
}
