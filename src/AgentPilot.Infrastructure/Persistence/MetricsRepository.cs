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

    public async Task<MetricsSummary> GetSummaryAsync(
        DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default)
    {
        var logs = db.LlmCallLogs.AsQueryable();
        if (fromUtc is not null) logs = logs.Where(l => l.CreatedAtUtc >= fromUtc);
        if (toUtc is not null) logs = logs.Where(l => l.CreatedAtUtc <= toUtc);

        var total = await logs.CountAsync(cancellationToken);
        if (total == 0)
            return new MetricsSummary();

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

        // Ratio de feedback positivo en el mismo rango.
        var feedback = db.Feedback.AsQueryable();
        if (fromUtc is not null) feedback = feedback.Where(f => f.CreatedAtUtc >= fromUtc);
        if (toUtc is not null) feedback = feedback.Where(f => f.CreatedAtUtc <= toUtc);
        var feedbackTotal = await feedback.CountAsync(cancellationToken);
        var feedbackPositive = await feedback.CountAsync(f => f.Rating == FeedbackRating.Positive, cancellationToken);

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
        };
    }
}
