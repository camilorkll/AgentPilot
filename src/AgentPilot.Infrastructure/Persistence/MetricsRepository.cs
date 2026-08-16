using AgentPilot.Application.Abstractions;
using AgentPilot.Application.Metrics;
using AgentPilot.Domain.Conversations;
using AgentPilot.Domain.Telemetry;
using Microsoft.EntityFrameworkCore;

namespace AgentPilot.Infrastructure.Persistence;

/// <summary>
/// Toda la aritmética de fechas vive en SQL, no en LINQ ni en .NET: el agrupamiento
/// diario y mensual usa <c>AT TIME ZONE 'Europe/Madrid'</c>, que PostgreSQL conoce de
/// forma nativa (su propia base de datos tzdata) sin depender de que el contenedor
/// tenga instalado el tzdata del sistema operativo. Es el mismo motivo por el que
/// ChunkSearchService usa SQL crudo para la búsqueda vectorial: el traductor de LINQ
/// no sabe hacer esto.
/// </summary>
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

    public async Task<MonthRange> ResolveMonthRangeAsync(
        string? monthFrom, string? monthTo, CancellationToken cancellationToken = default)
    {
        // COALESCE en cascada resuelve las tres reglas del plan en una sola pasada:
        //   - ninguno de los dos           -> [mes en curso, mes en curso]
        //   - solo monthFrom               -> [monthFrom, mes en curso]
        //   - solo monthTo                 -> [monthTo, monthTo] (un único mes)
        //   - ambos                        -> [monthFrom, monthTo]
        // "now() AT TIME ZONE 'Europe/Madrid'" da el instante actual como si fuera esa
        // zona: por eso "hoy" para el resumen no depende de la zona horaria del servidor.
        //
        // OJO con el sentido de "AT TIME ZONE": sobre un "timestamp" (sin zona) lo
        // INTERPRETA como hora local de esa zona y da el instante UTC equivalente (lo
        // que se quiere aquí); sobre un "timestamptz" hace justo lo contrario, muestra
        // la hora local de esa zona para ese instante. "to_timestamp(...)" ya devuelve
        // timestamptz, así que encadenarlo con AT TIME ZONE habría ido en la dirección
        // equivocada. Por eso el literal se construye como timestamp sin zona
        // ("(mfrom || '-01')::timestamp") y solo entonces se aplica AT TIME ZONE.
        FormattableString sql = $"""
            WITH bounds AS (
              SELECT
                coalesce({monthFrom}, coalesce({monthTo}, to_char(now() AT TIME ZONE 'Europe/Madrid', 'YYYY-MM'))) AS mfrom,
                coalesce({monthTo}, to_char(now() AT TIME ZONE 'Europe/Madrid', 'YYYY-MM')) AS mto
            )
            SELECT
              mfrom AS "MonthFrom",
              mto AS "MonthTo",
              ((mfrom || '-01')::timestamp AT TIME ZONE 'Europe/Madrid') AS "FromUtc",
              (((mto || '-01')::timestamp AT TIME ZONE 'Europe/Madrid') + interval '1 month') AS "ToUtcExclusive"
            FROM bounds
            """;

        var row = await db.Database.SqlQuery<MonthRangeRow>(sql).SingleAsync(cancellationToken);

        // La cota superior se guarda como exclusiva-menos-un-tick para poder seguir
        // usando el mismo criterio "<= toUtc" inclusivo en el resto del repositorio, en
        // vez de mantener dos convenios de cota distintos.
        return new MonthRange(row.MonthFrom, row.MonthTo, row.FromUtc, row.ToUtcExclusive.AddTicks(-1));
    }

    public async Task<MetricsSummary> GetSummaryAsync(
        DateTime? fromUtc, DateTime? toUtc,
        IReadOnlyList<string>? operators = null,
        CampaignFilter campaignFilter = default,
        string? monthFromLabel = null, string? monthToLabel = null,
        CancellationToken cancellationToken = default)
    {
        (fromUtc, toUtc) = (AsUtc(fromUtc), AsUtc(toUtc));
        var selected = operators?.Where(o => !string.IsNullOrWhiteSpace(o)).ToList() ?? [];

        var logs = FilterLogs(db.LlmCallLogs.AsQueryable(), fromUtc, toUtc, selected, campaignFilter);

        var total = await logs.CountAsync(cancellationToken);
        if (total == 0)
        {
            return new MetricsSummary
            {
                FilteredOperators = selected,
                MonthFrom = monthFromLabel,
                MonthTo = monthToLabel,
                CampaignId = campaignFilter.Kind == CampaignFilterKind.Specific
                    ? campaignFilter.CampaignId!.Value.ToString() : null,
            };
        }

        var avgLatency = await logs.AverageAsync(l => (double)l.LatencyMs, cancellationToken);
        var totalCost = await logs.SumAsync(l => l.EstimatedCostUsd, cancellationToken);

        var costByModel = await logs
            .GroupBy(l => l.Model)
            .Select(g => new { Model = g.Key, Cost = g.Sum(l => l.EstimatedCostUsd) })
            .ToListAsync(cancellationToken);

        // Sin clave foránea a campaña (CampaignName va desnormalizado a propósito, ver
        // LlmCallLog), así que el histórico previo a las campañas necesita una etiqueta
        // explícita en vez de desaparecer del diccionario sin más.
        var costByCampaign = await logs
            .GroupBy(l => l.CampaignName)
            .Select(g => new { CampaignName = g.Key, Cost = g.Sum(l => l.EstimatedCostUsd) })
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

        var feedback = FilterFeedback(fromUtc, toUtc, selected, campaignFilter);

        var feedbackTotal = await feedback.CountAsync(cancellationToken);
        var feedbackPositive = await feedback.CountAsync(f => f.Rating == FeedbackRating.Positive, cancellationToken);

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

        var dailyByOperator = await GetDailyByOperatorAsync(
            fromUtc, toUtc, selected, campaignFilter, cancellationToken);

        var monthlyByOperator = await QueryUsageBucketsAsync(
            MonthBucket, groupByUser: true, fromUtc, toUtc, selected, campaignFilter, cancellationToken);
        var monthlyFeedbackByOperator = await QueryFeedbackBucketsAsync(
            MonthBucket, groupByUser: true, fromUtc, toUtc, selected, campaignFilter, cancellationToken);
        var monthlyAll = await QueryUsageBucketsAsync(
            MonthBucket, groupByUser: false, fromUtc, toUtc, selected, campaignFilter, cancellationToken);
        var monthlyFeedbackAll = await QueryFeedbackBucketsAsync(
            MonthBucket, groupByUser: false, fromUtc, toUtc, selected, campaignFilter, cancellationToken);

        var monthlyTotals = MergeMonthly(monthlyByOperator, monthlyFeedbackByOperator)
            .Concat(MergeMonthly(monthlyAll, monthlyFeedbackAll))
            .ToList();

        return new MetricsSummary
        {
            TotalQuestions = total,
            PositiveFeedbackRate = feedbackTotal > 0 ? (double)feedbackPositive / feedbackTotal : null,
            RatedAnswers = feedbackTotal,
            PositiveAnswers = feedbackPositive,
            AvgLatencyMs = Math.Round(avgLatency, 1),
            P95LatencyMs = p95,
            TotalCostUsd = Math.Round(totalCost, 6),
            CostByModel = costByModel.ToDictionary(x => x.Model, x => Math.Round(x.Cost, 6)),
            CostByCampaign = costByCampaign.ToDictionary(
                x => x.CampaignName ?? SinCampañaHistórica, x => Math.Round(x.Cost, 6)),
            QuestionsPerDay = perDay
                .Select(x => new QuestionsPerDay(DateOnly.FromDateTime(x.Day), x.Count))
                .ToList(),
            ByOperator = byOperator,
            DailyByOperator = dailyByOperator,
            MonthlyTotals = monthlyTotals,
            FilteredOperators = selected,
            MonthFrom = monthFromLabel,
            MonthTo = monthToLabel,
            CampaignId = campaignFilter.Kind == CampaignFilterKind.Specific
                ? campaignFilter.CampaignId!.Value.ToString() : null,
        };
    }

    public async Task<IReadOnlyList<DailyOperatorUsage>> GetDailyByOperatorAsync(
        DateTime? fromUtc, DateTime? toUtc,
        IReadOnlyList<string>? operators = null,
        CampaignFilter campaignFilter = default,
        CancellationToken cancellationToken = default)
    {
        (fromUtc, toUtc) = (AsUtc(fromUtc), AsUtc(toUtc));
        var selected = operators?.Where(o => !string.IsNullOrWhiteSpace(o)).ToList() ?? [];

        var usage = await QueryUsageBucketsAsync(
            DayBucket, groupByUser: true, fromUtc, toUtc, selected, campaignFilter, cancellationToken);
        var feedback = await QueryFeedbackBucketsAsync(
            DayBucket, groupByUser: true, fromUtc, toUtc, selected, campaignFilter, cancellationToken);

        var feedbackByKey = feedback.ToDictionary(f => (f.Bucket, f.UserName));

        return usage
            .Select(u =>
            {
                var fb = feedbackByKey.GetValueOrDefault((u.Bucket, u.UserName));
                return new DailyOperatorUsage(
                    DateOnly.ParseExact(u.Bucket, "yyyy-MM-dd"),
                    u.UserName!,
                    u.Questions,
                    Math.Round(u.Cost, 6),
                    Math.Round(u.AvgLatencyMs, 1),
                    fb is { Total: > 0 } ? (double)fb.Positive / fb.Total : null);
            })
            .OrderBy(d => d.Date).ThenBy(d => d.UserName)
            .ToList();
    }

    // --- Construcción de las consultas SQL crudas ---

    private const string SinCampañaHistórica = "Sin campaña (histórico)";

    /// <summary>
    /// Como con la campaña, "sin operador" es histórico real (llamadas de antes de que
    /// existiera el seguimiento por agente), no una fila vacía que se pueda ocultar.
    /// Etiquetarla es obligatorio y no cosmético: en el desglose por mes,
    /// <c>UserName = null</c> ya significa "total de todos los operadores"; si una
    /// llamada anónima conservara null, sería indistinguible de esa fila-total y las
    /// mezclaría. El COALESCE va en el propio SELECT (no después, en C#) para que el
    /// valor con el que se fusiona el feedback ya sea consistente.
    /// </summary>
    private const string SinOperadorHistórico = "Sin operador (histórico)";

    /// <summary>
    /// Normaliza a Kind=Utc en la propia entrada del repositorio, no solo en el
    /// controlador: Npgsql rechaza un DateTime con Kind=Unspecified (el que produce,
    /// por ejemplo, el binder de ASP.NET desde un string de query) al compararlo con
    /// una columna timestamptz, con un error que no dice de dónde vino la fecha. Hacerlo
    /// aquí protege a cualquier llamador, no solo al controlador actual.
    /// </summary>
    private static DateTime? AsUtc(DateTime? value) =>
        value is null ? null : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);

    private static string DayBucket(string column) =>
        $"to_char({column} AT TIME ZONE 'Europe/Madrid', 'YYYY-MM-DD')";

    private static string MonthBucket(string column) =>
        $"to_char({column} AT TIME ZONE 'Europe/Madrid', 'YYYY-MM')";

    /// <summary>
    /// Uso agregado (preguntas, coste, latencia) agrupado por el "cubo" temporal
    /// indicado (día o mes) y, opcionalmente, por operador.
    /// </summary>
    private async Task<List<BucketRow>> QueryUsageBucketsAsync(
        Func<string, string> bucket, bool groupByUser,
        DateTime? fromUtc, DateTime? toUtc, IReadOnlyList<string> operators, CampaignFilter campaignFilter,
        CancellationToken cancellationToken)
    {
        var (whereSql, args) = BuildWhereClause(
            fromUtc, toUtc, operators, campaignFilter,
            dateColumn: "l.\"CreatedAtUtc\"", userColumn: "l.\"UserName\"", campaignColumn: "l.\"CampaignId\"");

        var bucketExpr = bucket("l.\"CreatedAtUtc\"");
        // El GROUP BY agrupa sobre la columna real (NULL agrupa con NULL igual que
        // cualquier otro valor); el coalesce solo decide la etiqueta con la que sale.
        var groupBy = groupByUser ? $"{bucketExpr}, l.\"UserName\"" : bucketExpr;
        var userSelect = groupByUser ? $"coalesce(l.\"UserName\", '{SinOperadorHistórico}')" : "NULL::text";

        var sql = $"""
            SELECT
              {bucketExpr} AS "Bucket",
              {userSelect} AS "UserName",
              count(*)::int AS "Questions",
              coalesce(sum(l."EstimatedCostUsd"), 0)::double precision AS "Cost",
              coalesce(avg(l."LatencyMs"), 0)::double precision AS "AvgLatencyMs"
            FROM llm_call_logs l
            WHERE {whereSql}
            GROUP BY {groupBy}
            """;

        return await db.Database.SqlQueryRaw<BucketRow>(sql, args.ToArray()).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Total y positivas de feedback agrupadas por el mismo "cubo" y criterio de
    /// operador que <see cref="QueryUsageBucketsAsync"/>, para poder fusionarlas por
    /// clave (bucket, operador) en C#.
    ///
    /// El feedback no tiene columna de campaña propia (una valoración es del mensaje,
    /// no de la llamada al LLM), así que el filtro de campaña llega uniendo con la
    /// conversación del mensaje valorado.
    /// </summary>
    private async Task<List<FeedbackBucketRow>> QueryFeedbackBucketsAsync(
        Func<string, string> bucket, bool groupByUser,
        DateTime? fromUtc, DateTime? toUtc, IReadOnlyList<string> operators, CampaignFilter campaignFilter,
        CancellationToken cancellationToken)
    {
        var (whereSql, args) = BuildWhereClause(
            fromUtc, toUtc, operators, campaignFilter,
            dateColumn: "f.\"CreatedAtUtc\"", userColumn: "f.\"CreatedBy\"", campaignColumn: "c.\"CampaignId\"");

        var bucketExpr = bucket("f.\"CreatedAtUtc\"");
        var groupBy = groupByUser ? $"{bucketExpr}, f.\"CreatedBy\"" : bucketExpr;
        // Mismo coalesce que en QueryUsageBucketsAsync y por el mismo motivo: si aquí
        // se dejara null y allí la etiqueta, la fusión por (bucket, operador) en C# no
        // encontraría la fila y el feedback de esas llamadas se perdería en silencio.
        var userSelect = groupByUser ? $"coalesce(f.\"CreatedBy\", '{SinOperadorHistórico}')" : "NULL::text";

        var sql = $"""
            SELECT
              {bucketExpr} AS "Bucket",
              {userSelect} AS "UserName",
              count(*)::int AS "Total",
              count(*) FILTER (WHERE f."Rating" = 'Positive')::int AS "Positive"
            FROM feedback f
            JOIN messages m ON m."Id" = f."MessageId"
            JOIN conversations c ON c."Id" = m."ConversationId"
            WHERE {whereSql}
            GROUP BY {groupBy}
            """;

        return await db.Database.SqlQueryRaw<FeedbackBucketRow>(sql, args.ToArray()).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Construye el WHERE parametrizado que comparten todas las consultas crudas.
    /// Los placeholders <c>{n}</c> son de <see cref="Microsoft.EntityFrameworkCore.RelationalQueryableExtensions.SqlQueryRaw{TResult}"/>
    /// (equivalentes a <c>string.Format</c>, pero convertidos a parámetros reales del
    /// proveedor): nunca se concatena un valor directamente en el texto, solo nombres
    /// de columna fijados por el propio código.
    /// </summary>
    private static (string Sql, List<object> Args) BuildWhereClause(
        DateTime? fromUtc, DateTime? toUtc, IReadOnlyList<string>? operators, CampaignFilter campaignFilter,
        string dateColumn, string userColumn, string campaignColumn)
    {
        var conditions = new List<string>();
        var args = new List<object>();

        if (fromUtc is not null)
        {
            conditions.Add($"{dateColumn} >= {{{args.Count}}}");
            args.Add(fromUtc.Value);
        }
        if (toUtc is not null)
        {
            conditions.Add($"{dateColumn} <= {{{args.Count}}}");
            args.Add(toUtc.Value);
        }
        if (operators is { Count: > 0 })
        {
            conditions.Add($"{userColumn} = ANY({{{args.Count}}})");
            args.Add(operators.ToArray());
        }
        switch (campaignFilter.Kind)
        {
            case CampaignFilterKind.NoCampaign:
                conditions.Add($"{campaignColumn} IS NULL");
                break;
            case CampaignFilterKind.Specific:
                conditions.Add($"{campaignColumn} = {{{args.Count}}}");
                args.Add(campaignFilter.CampaignId!.Value);
                break;
        }

        return conditions.Count > 0 ? (string.Join(" AND ", conditions), args) : ("1=1", args);
    }

    private static IQueryable<LlmCallLog> FilterLogs(
        IQueryable<LlmCallLog> logs, DateTime? fromUtc, DateTime? toUtc,
        IReadOnlyList<string> operators, CampaignFilter campaignFilter)
    {
        if (fromUtc is not null) logs = logs.Where(l => l.CreatedAtUtc >= fromUtc);
        if (toUtc is not null) logs = logs.Where(l => l.CreatedAtUtc <= toUtc);
        if (operators.Count > 0) logs = logs.Where(l => l.UserName != null && operators.Contains(l.UserName));

        return campaignFilter.Kind switch
        {
            CampaignFilterKind.NoCampaign => logs.Where(l => l.CampaignId == null),
            CampaignFilterKind.Specific => logs.Where(l => l.CampaignId == campaignFilter.CampaignId),
            _ => logs,
        };
    }

    /// <summary>
    /// El feedback se correlaciona con el rango/operador/campaña por su propia fecha y
    /// autor (no hay una clave directa entre una valoración y la llamada al LLM que
    /// generó la respuesta valorada), igual que ya hacía el código antes de esta fase.
    /// </summary>
    private IQueryable<Feedback> FilterFeedback(
        DateTime? fromUtc, DateTime? toUtc, IReadOnlyList<string> operators, CampaignFilter campaignFilter)
    {
        var feedback = db.Feedback.AsQueryable();
        if (fromUtc is not null) feedback = feedback.Where(f => f.CreatedAtUtc >= fromUtc);
        if (toUtc is not null) feedback = feedback.Where(f => f.CreatedAtUtc <= toUtc);
        if (operators.Count > 0) feedback = feedback.Where(f => f.CreatedBy != null && operators.Contains(f.CreatedBy));

        if (campaignFilter.Kind == CampaignFilterKind.All)
            return feedback;

        // Solo se necesita el join cuando de verdad se filtra por campaña: una
        // valoración no tiene columna de campaña propia, hay que llegar a ella a
        // través del mensaje y su conversación.
        var byCampaign =
            from f in feedback
            join m in db.Set<Message>() on f.MessageId equals m.Id
            join c in db.Conversations on m.ConversationId equals c.Id
            select new { f, c.CampaignId };

        byCampaign = campaignFilter.Kind == CampaignFilterKind.NoCampaign
            ? byCampaign.Where(x => x.CampaignId == null)
            : byCampaign.Where(x => x.CampaignId == campaignFilter.CampaignId);

        return byCampaign.Select(x => x.f);
    }

    private static IEnumerable<MonthlyTotal> MergeMonthly(
        List<BucketRow> usage, List<FeedbackBucketRow> feedback)
    {
        var feedbackByKey = feedback.ToDictionary(f => (f.Bucket, f.UserName));

        return usage.Select(u =>
        {
            var fb = feedbackByKey.GetValueOrDefault((u.Bucket, u.UserName));
            return new MonthlyTotal(
                u.Bucket,
                u.UserName,
                u.Questions,
                Math.Round(u.Cost, 6),
                Math.Round(u.AvgLatencyMs, 1),
                fb is { Total: > 0 } ? (double)fb.Positive / fb.Total : null);
        });
    }

    // Registros de proyección para las consultas SQL crudas de arriba. Anidados y
    // privados porque no representan entidades del dominio, solo la forma de una
    // fila devuelta por una consulta concreta de este repositorio.
    private sealed record MonthRangeRow(string MonthFrom, string MonthTo, DateTime FromUtc, DateTime ToUtcExclusive);
    private sealed record BucketRow(string Bucket, string? UserName, int Questions, double Cost, double AvgLatencyMs);
    private sealed record FeedbackBucketRow(string Bucket, string? UserName, int Total, int Positive);
}
