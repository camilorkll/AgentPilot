using System.Text.Json.Serialization;

namespace AgentPilot.Evals;

/// <summary>Un caso del set dorado.</summary>
public record EvalCase
{
    public int Id { get; init; }
    public string Question { get; init; } = string.Empty;

    /// <summary>Documento que debería aparecer en las citas (null si no procede).</summary>
    public string? ExpectedDocument { get; init; }

    /// <summary>Datos clave; basta que la respuesta contenga uno.</summary>
    public string[] ExpectedKeywords { get; init; } = [];

    /// <summary>False = la pregunta NO tiene respuesta en el corpus (debe abstenerse).</summary>
    public bool Answerable { get; init; }
}

public record GoldenSet
{
    public string Description { get; init; } = string.Empty;
    public List<EvalCase> Cases { get; init; } = [];
}

/// <summary>
/// Una entrada de <c>evals/golden-set/campaigns.json</c>: qué set dorado se ejecuta
/// contra qué campaña. Permite correr varias campañas en una sola pasada (modo
/// <c>--all</c>) sin tocar código: añadir una campaña es añadir una línea al manifiesto.
/// </summary>
public record CampaignEntry
{
    public string Label { get; init; } = string.Empty;
    public Guid CampaignId { get; init; }
    public string GoldenSet { get; init; } = string.Empty;
}

/// <summary>Resultado de evaluar un caso.</summary>
public record EvalResult
{
    public EvalCase Case { get; init; } = new();
    public string Answer { get; init; } = string.Empty;
    public string[] CitedDocuments { get; init; } = [];
    public long LatencyMs { get; init; }

    /// <summary>Momento en que el agente ve el primer indicio en pantalla (las fuentes).</summary>
    public long CitationsMs { get; init; }

    /// <summary>Momento en que el modelo empieza a redactar.</summary>
    public long FirstTokenMs { get; init; }

    public double CostUsd { get; init; }

    /// <summary>El documento esperado está entre las citas.</summary>
    public bool RetrievalHit { get; init; }

    /// <summary>La respuesta contiene alguno de los datos clave.</summary>
    public bool AnswerHit { get; init; }

    /// <summary>Para no respondibles: el asistente se abstuvo correctamente.</summary>
    public bool AbstainedCorrectly { get; init; }

    /// <summary>Éxito global del caso según su tipo.</summary>
    public bool Passed => Case.Answerable ? RetrievalHit && AnswerHit : AbstainedCorrectly;
}
