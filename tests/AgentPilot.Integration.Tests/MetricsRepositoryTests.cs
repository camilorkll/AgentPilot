using AgentPilot.Domain.Conversations;
using AgentPilot.Domain.Telemetry;
using AgentPilot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentPilot.Integration.Tests;

public class MetricsRepositoryTests(PgVectorFixture fixture) : IClassFixture<PgVectorFixture>
{
    /// <summary>
    /// Campaña de las conversaciones de prueba: hay clave foránea desde conversations,
    /// así que tiene que existir en la tabla.
    /// </summary>
    private static readonly Guid Campaña = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private async Task<AgentPilotDbContext> FreshContextAsync()
    {
        var db = fixture.CreateContext();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE campaigns, llm_call_logs, feedback, conversations, documents CASCADE;");
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO campaigns (""Id"", ""Name"", ""Status"", ""CreatedAtUtc"")
            VALUES ({Campaña}, 'Campaña de métricas', 1, now());");
        return db;
    }

    [Fact]
    public async Task GetSummary_AgregaUsoCosteYFeedback()
    {
        await using var db = await FreshContextAsync();

        // 3 llamadas al LLM: 2 de gpt-5-mini y 1 de gpt-5.
        db.LlmCallLogs.AddRange(
            new LlmCallLog("gpt-5-mini", 100, 50, 0.001, 100, null, "agente"),
            new LlmCallLog("gpt-5-mini", 120, 40, 0.001, 200, null, "agente"),
            new LlmCallLog("gpt-5", 200, 80, 0.010, 300, null, "admin"));

        // Una conversación con respuesta y un feedback positivo sobre ella.
        var conversation = new Conversation(Campaña);
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

    [Fact]
    public async Task GetSummary_DesglosaPorOperador()
    {
        await using var db = await FreshContextAsync();

        db.LlmCallLogs.AddRange(
            new LlmCallLog("gpt-5-mini", 100, 50, 0.001, 100, null, "agente"),
            new LlmCallLog("gpt-5-mini", 100, 50, 0.001, 300, null, "agente"),
            new LlmCallLog("gpt-5-mini", 100, 50, 0.002, 200, null, "supervisor"));
        await db.SaveChangesAsync();

        var summary = await new MetricsRepository(db).GetSummaryAsync(null, null);

        Assert.Equal(2, summary.ByOperator.Count);
        var agente = summary.ByOperator.First();          // ordenado por uso
        Assert.Equal("agente", agente.UserName);
        Assert.Equal(2, agente.Questions);
        Assert.Equal(200, agente.AvgLatencyMs);           // (100 + 300) / 2
        Assert.Equal(0.002, agente.TotalCostUsd, precision: 6);
    }

    [Fact]
    public async Task GetSummary_FiltradoPorOperador_SoloCuentaEseOperador()
    {
        await using var db = await FreshContextAsync();

        db.LlmCallLogs.AddRange(
            new LlmCallLog("gpt-5-mini", 100, 50, 0.001, 100, null, "agente"),
            new LlmCallLog("gpt-5-mini", 100, 50, 0.005, 100, null, "supervisor"));
        await db.SaveChangesAsync();

        var summary = await new MetricsRepository(db).GetSummaryAsync(null, null, ["supervisor"]);

        Assert.Equal(1, summary.TotalQuestions);
        Assert.Equal(0.005, summary.TotalCostUsd, precision: 6);
        Assert.Equal(["supervisor"], summary.FilteredOperators);
    }

    [Fact]
    public async Task GetOperators_DevuelveLosOperadoresConActividad()
    {
        await using var db = await FreshContextAsync();

        db.LlmCallLogs.AddRange(
            new LlmCallLog("gpt-5-mini", 10, 5, 0.001, 100, null, "zeta"),
            new LlmCallLog("gpt-5-mini", 10, 5, 0.001, 100, null, "alfa"),
            new LlmCallLog("gpt-5-mini", 10, 5, 0.001, 100, null, "alfa"),
            new LlmCallLog("gpt-5-mini", 10, 5, 0.001, 100, null, null)); // sin operador
        await db.SaveChangesAsync();

        var operators = await new MetricsRepository(db).GetOperatorsAsync();

        Assert.Equal(["alfa", "zeta"], operators); // sin duplicados y ordenados
    }
}
