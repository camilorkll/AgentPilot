using AgentPilot.Domain.Conversations;
using AgentPilot.Domain.Telemetry;
using AgentPilot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentPilot.Integration.Tests;

public class MetricsRepositoryTests(PgVectorFixture fixture) : IClassFixture<PgVectorFixture>
{
    [Fact]
    public async Task GetSummary_AgregaUsoCosteYFeedback()
    {
        await using var db = fixture.CreateContext();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE llm_call_logs, feedback, conversations, documents CASCADE;");

        // 3 llamadas al LLM: 2 de gpt-5-mini y 1 de gpt-5.
        db.LlmCallLogs.AddRange(
            new LlmCallLog("gpt-5-mini", 100, 50, 0.001, 100, null),
            new LlmCallLog("gpt-5-mini", 120, 40, 0.001, 200, null),
            new LlmCallLog("gpt-5", 200, 80, 0.010, 300, null));

        // Una conversación con respuesta y un feedback positivo sobre ella.
        var conversation = new Conversation();
        conversation.AddUserMessage("¿pregunta?");
        var assistant = conversation.AddAssistantMessage("respuesta", []);
        db.Conversations.Add(conversation);
        db.Feedback.Add(new Feedback(assistant.Id, FeedbackRating.Positive, "útil", "agente"));
        await db.SaveChangesAsync();

        var summary = await new MetricsRepository(db).GetSummaryAsync(null, null);

        Assert.Equal(3, summary.TotalQuestions);
        Assert.Equal(0.012, summary.TotalCostUsd, precision: 6);
        Assert.Equal(2, summary.CostByModel.Count);
        Assert.Equal(0.002, summary.CostByModel["gpt-5-mini"], precision: 6);
        Assert.Equal(1.0, summary.PositiveFeedbackRate);
        Assert.Equal(3, summary.QuestionsPerDay.Sum(d => d.Count));
        Assert.True(summary.P95LatencyMs >= summary.AvgLatencyMs);
    }
}
