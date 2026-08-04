using AgentPilot.Application.Metrics;
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

    /// <summary>
    /// La razón de todo el bloque de zona horaria: una consulta hecha a las 23:30 UTC
    /// del 31 de julio ocurre a la 01:30 del 1 de agosto en Madrid (CEST, verano). Con
    /// el agrupamiento antiguo (".Date" sobre UTC) aparecería como día 31 de julio;
    /// para un supervisor en Madrid, es una consulta del día 1 de agosto.
    /// </summary>
    [Fact]
    public async Task GetDailyByOperator_AgrupaPorElDiaEnMadrid_NoPorElDiaEnUtc()
    {
        await using var db = await FreshContextAsync();

        // El constructor del dominio fija CreatedAtUtc a "ahora": para fijar un
        // instante histórico exacto hay que insertarlo directamente por SQL.
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO llm_call_logs
                (""Id"", ""Model"", ""UserName"", ""PromptTokens"", ""CompletionTokens"",
                 ""EstimatedCostUsd"", ""LatencyMs"", ""CreatedAtUtc"")
            VALUES
                ({id}, 'gpt-5-mini', 'agente', 100, 50, 0.001, 100,
                 '2026-07-31T23:30:00Z'::timestamptz);");

        var rows = await new MetricsRepository(db).GetDailyByOperatorAsync(null, null);

        var row = Assert.Single(rows);
        Assert.Equal(new DateOnly(2026, 8, 1), row.Date); // no el 31 de julio
        Assert.Equal("agente", row.UserName);
    }

    /// <summary>
    /// El total mensual "todos los operadores" no es un promedio de los promedios por
    /// operador: si lo fuera, dos operadores con latencias muy distintas darían un
    /// número que no representa el conjunto real de llamadas.
    /// </summary>
    [Fact]
    public async Task GetSummary_TotalMensualDeTodos_NoEsElPromedioDeLosPromediosPorOperador()
    {
        await using var db = await FreshContextAsync();

        db.LlmCallLogs.AddRange(
            new LlmCallLog("gpt-5-mini", 10, 5, 0.001, 100, null, "agente"),
            new LlmCallLog("gpt-5-mini", 10, 5, 0.001, 300, null, "agente"),   // media agente: 200
            new LlmCallLog("gpt-5-mini", 10, 5, 0.001, 1000, null, "supervisor")); // media supervisor: 1000
        await db.SaveChangesAsync();

        var summary = await new MetricsRepository(db).GetSummaryAsync(null, null);

        var total = summary.MonthlyTotals.Single(m => m.UserName is null);
        var agente = summary.MonthlyTotals.Single(m => m.UserName == "agente");

        Assert.Equal(3, total.Questions);
        // Media real de las 3 llamadas: (100+300+1000)/3 ≈ 466,7 — NO (200+1000)/2 = 600,
        // que es lo que daría promediar los promedios por operador.
        Assert.Equal(466.7, total.AvgLatencyMs, precision: 1);
        Assert.Equal(200, agente.AvgLatencyMs);
    }

    /// <summary>
    /// "Sin campaña" es un caso de negocio real (el histórico anterior a esta fase), no
    /// un "no filtrado": debe poder aislarse explícitamente, y el filtro no debe
    /// hacerlo desaparecer al filtrar por otra cosa.
    /// </summary>
    [Fact]
    public async Task GetSummary_ConNoCampaign_SoloCuentaElHistoricoSinCampaña()
    {
        await using var db = await FreshContextAsync();

        db.LlmCallLogs.AddRange(
            new LlmCallLog("gpt-5-mini", 10, 5, 0.001, 100, null, "agente"), // sin campaña
            new LlmCallLog("gpt-5-mini", 10, 5, 0.001, 100, null, "agente", Campaña, "Campaña de métricas"));
        await db.SaveChangesAsync();

        var summary = await new MetricsRepository(db)
            .GetSummaryAsync(null, null, campaignFilter: CampaignFilter.NoCampaign);

        Assert.Equal(1, summary.TotalQuestions);
        Assert.Null(summary.CampaignId); // "sin campaña" no es una campaña
    }

    [Fact]
    public async Task GetSummary_ConCampañaEspecifica_ExcluyeElRestoYElHistorico()
    {
        await using var db = await FreshContextAsync();

        var otraCampaña = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO campaigns (""Id"", ""Name"", ""Status"", ""CreatedAtUtc"")
            VALUES ({otraCampaña}, 'Otra campaña', 1, now());");

        db.LlmCallLogs.AddRange(
            new LlmCallLog("gpt-5-mini", 10, 5, 0.001, 100, null, "agente"), // sin campaña
            new LlmCallLog("gpt-5-mini", 10, 5, 0.001, 100, null, "agente", otraCampaña, "Otra campaña"),
            new LlmCallLog("gpt-5-mini", 10, 5, 0.001, 100, null, "agente", Campaña, "Campaña de métricas"));
        await db.SaveChangesAsync();

        var summary = await new MetricsRepository(db)
            .GetSummaryAsync(null, null, campaignFilter: CampaignFilter.Specific(Campaña));

        Assert.Equal(1, summary.TotalQuestions);
        Assert.Equal(Campaña.ToString(), summary.CampaignId);
    }

    [Fact]
    public async Task ResolveMonthRange_SinParametros_EsElMesEnCurso()
    {
        await using var db = await FreshContextAsync();

        var range = await new MetricsRepository(db).ResolveMonthRangeAsync(null, null);

        Assert.Equal(range.MonthFrom, range.MonthTo);
        Assert.True(range.FromUtc < range.ToUtcExclusive);
    }

    [Fact]
    public async Task ResolveMonthRange_SoloDesde_LlegaHastaElMesEnCurso()
    {
        await using var db = await FreshContextAsync();
        var mesActual = (await new MetricsRepository(db).ResolveMonthRangeAsync(null, null)).MonthFrom;

        var range = await new MetricsRepository(db).ResolveMonthRangeAsync("2026-01", null);

        Assert.Equal("2026-01", range.MonthFrom);
        Assert.Equal(mesActual, range.MonthTo);
    }

    [Fact]
    public async Task ResolveMonthRange_AmbosMeses_EsUnIntervaloCerrado()
    {
        await using var db = await FreshContextAsync();

        var range = await new MetricsRepository(db).ResolveMonthRangeAsync("2026-06", "2026-08");

        Assert.Equal("2026-06", range.MonthFrom);
        Assert.Equal("2026-08", range.MonthTo);
        // El 1 de septiembre en Madrid, menos un tick, es la última cota de agosto.
        Assert.True(range.ToUtcExclusive.Month is 8 or 9);
    }

    /// <summary>
    /// No basta con comprobar los valores que devuelve ResolveMonthRangeAsync (los
    /// tests de arriba ya lo hacen): hay que comprobar que ese resultado sirve como
    /// entrada de otra consulta. Un DateTime con Kind=Unspecified compara igual que uno
    /// con Kind=Utc con los mismos ticks, así que un test que solo mira los valores no
    /// detecta el problema; solo se ve al usarlos de verdad contra una columna
    /// timestamptz, que es justo lo que hace GetSummaryAsync.
    /// </summary>
    [Fact]
    public async Task ResolveMonthRange_SuResultado_SirveComoEntradaDeOtraConsulta()
    {
        await using var db = await FreshContextAsync();
        db.LlmCallLogs.Add(new LlmCallLog("gpt-5-mini", 10, 5, 0.001, 100, null, "agente"));
        await db.SaveChangesAsync();

        var repo = new MetricsRepository(db);
        var range = await repo.ResolveMonthRangeAsync(null, null);

        var summary = await repo.GetSummaryAsync(range.FromUtc, range.ToUtcExclusive);

        Assert.Equal(1, summary.TotalQuestions);
    }

    /// <summary>
    /// El binder de ASP.NET produce DateTime con Kind=Unspecified a partir de un
    /// string de query (los parámetros from/to heredados); Npgsql rechaza compararlo
    /// con una columna timestamptz. Apareció de verdad al probar
    /// <c>/metrics/summary?from=2026-08-01&amp;to=2026-08-31</c> contra la aplicación
    /// real. El repositorio debe aceptar un Kind=Unspecified sin lanzar: normalizarlo
    /// aquí protege a cualquier llamador, no solo al controlador de hoy.
    /// </summary>
    [Fact]
    public async Task GetSummary_AceptaFechasConKindNoEspecificado()
    {
        await using var db = await FreshContextAsync();
        db.LlmCallLogs.Add(new LlmCallLog("gpt-5-mini", 10, 5, 0.001, 100, null, "agente"));
        await db.SaveChangesAsync();

        var sinKind = DateTime.SpecifyKind(new DateTime(2000, 1, 1), DateTimeKind.Unspecified);

        var summary = await new MetricsRepository(db).GetSummaryAsync(sinKind, toUtc: null);

        Assert.Equal(1, summary.TotalQuestions);
    }
}
