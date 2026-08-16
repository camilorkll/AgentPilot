namespace AgentPilot.Application.Metrics;

/// <summary>Preguntas atendidas en un día concreto.</summary>
public sealed record QuestionsPerDay(DateOnly Date, int Count);

/// <summary>
/// Actividad de un operador: cuánto usa el copiloto, cuánto cuesta y cómo de
/// útiles resultan sus respuestas. Es la vista que necesita un supervisor.
/// </summary>
public sealed record OperatorUsage(
    string UserName,
    int Questions,
    double TotalCostUsd,
    double AvgLatencyMs,
    double? PositiveFeedbackRate);

/// <summary>
/// Actividad de un operador en un día concreto, en hora de Europe/Madrid. Es la matriz
/// que alimenta las dos vistas del panel (Agente → Días y Día → Agentes): una sola
/// consulta, agrupada de dos maneras distintas en el cliente.
/// </summary>
public sealed record DailyOperatorUsage(
    DateOnly Date,
    string UserName,
    int Questions,
    double CostUsd,
    double AvgLatencyMs,
    double? PositiveFeedbackRate);

/// <summary>
/// Total de un mes. Con <paramref name="UserName"/> es el total de ESE operador en el
/// mes (vista Agente → Días); con <paramref name="UserName"/> a null es el total de
/// TODOS los operadores incluidos en el filtro (vista Día → Agentes).
///
/// Se calcula aparte de <see cref="DailyOperatorUsage"/> y no sumando sus filas: las
/// preguntas y el coste son aditivos, pero la latencia media y el porcentaje de
/// respuestas útiles no lo son, y promediar promedios da un número equivocado.
/// </summary>
public sealed record MonthlyTotal(
    string Month,
    string? UserName,
    int Questions,
    double CostUsd,
    double AvgLatencyMs,
    double? PositiveFeedbackRate);

/// <summary>
/// Resumen agregado de uso, calidad y coste (esquema MetricsSummary del OpenAPI).
/// </summary>
public sealed record MetricsSummary
{
    public int TotalQuestions { get; init; }

    /// <summary>Ratio de feedback positivo (0..1); null si aún no hay valoraciones.</summary>
    public double? PositiveFeedbackRate { get; init; }

    /// <summary>
    /// Respuestas que alguien valoró, y de ellas las positivas. Son el denominador y el
    /// numerador de <see cref="PositiveFeedbackRate"/>.
    ///
    /// Se publican porque sin ellos el porcentaje engaña: aparecía junto a
    /// <see cref="TotalQuestions"/> y se leía como "la mitad de las respuestas no
    /// sirvieron", cuando en realidad valoradas había cuatro. Valorar es voluntario, así
    /// que este denominador es siempre mucho menor que el total de preguntas y no debe
    /// confundirse con él.
    /// </summary>
    public int RatedAnswers { get; init; }

    /// <inheritdoc cref="RatedAnswers"/>
    public int PositiveAnswers { get; init; }

    public double AvgLatencyMs { get; init; }
    public double P95LatencyMs { get; init; }
    public double TotalCostUsd { get; init; }
    public IReadOnlyDictionary<string, double> CostByModel { get; init; } = new Dictionary<string, double>();

    /// <summary>
    /// Coste por nombre de campaña (desnormalizado en la telemetría, así que sigue
    /// leyéndose después de eliminarla). El histórico sin campaña aparece bajo la
    /// clave "Sin campaña (histórico)", nunca omitido: si desapareciera, la suma de
    /// este diccionario dejaría de cuadrar con <see cref="TotalCostUsd"/> y parecería
    /// un error.
    /// </summary>
    public IReadOnlyDictionary<string, double> CostByCampaign { get; init; } = new Dictionary<string, double>();

    public IReadOnlyList<QuestionsPerDay> QuestionsPerDay { get; init; } = [];

    /// <summary>Desglose por operador, de mayor a menor uso.</summary>
    public IReadOnlyList<OperatorUsage> ByOperator { get; init; } = [];

    /// <summary>
    /// Matriz (día, operador) que alimenta las dos vistas del panel. Solo incluye las
    /// combinaciones con actividad: los días sin uso no aparecen.
    /// </summary>
    public IReadOnlyList<DailyOperatorUsage> DailyByOperator { get; init; } = [];

    public IReadOnlyList<MonthlyTotal> MonthlyTotals { get; init; } = [];

    /// <summary>Operadores incluidos en el resumen (vacío = todos).</summary>
    public IReadOnlyList<string> FilteredOperators { get; init; } = [];

    /// <summary>Mes inicial (YYYY-MM) realmente aplicado, ya con los valores por defecto resueltos.</summary>
    public string? MonthFrom { get; init; }

    /// <summary>Mes final (YYYY-MM) realmente aplicado.</summary>
    public string? MonthTo { get; init; }

    /// <summary>Campaña aplicada al informe; null si no se filtró por campaña.</summary>
    public string? CampaignId { get; init; }
}
