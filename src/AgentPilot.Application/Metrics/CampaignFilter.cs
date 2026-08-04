namespace AgentPilot.Application.Metrics;

public enum CampaignFilterKind
{
    /// <summary>Sin filtro: se incluye todo, con y sin campaña.</summary>
    All,

    /// <summary>Solo el histórico anterior a las campañas (CampaignId nulo).</summary>
    NoCampaign,

    /// <summary>Una campaña concreta.</summary>
    Specific,
}

/// <summary>
/// Filtro de campaña de un informe de métricas. Tres estados, no un `Guid?` con un
/// valor mágico: el histórico sin campaña es un caso de negocio real (no un "no
/// filtrado"), y expresarlo como estado explícito evita que desaparezca en silencio
/// al filtrar — que es justo lo que el plan pide evitar.
/// </summary>
public readonly record struct CampaignFilter(CampaignFilterKind Kind, Guid? CampaignId = null)
{
    public static readonly CampaignFilter All = new(CampaignFilterKind.All);
    public static readonly CampaignFilter NoCampaign = new(CampaignFilterKind.NoCampaign);

    public static CampaignFilter Specific(Guid campaignId) => new(CampaignFilterKind.Specific, campaignId);

    /// <summary>
    /// Interpreta el parámetro de consulta <c>campaignId</c>: vacío = todo, "none" =
    /// solo el histórico sin campaña, cualquier otro valor debe ser un GUID válido.
    /// </summary>
    public static CampaignFilter Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return All;
        if (raw.Equals("none", StringComparison.OrdinalIgnoreCase)) return NoCampaign;

        return Guid.TryParse(raw, out var id)
            ? Specific(id)
            : throw new FormatException(
                $"campaignId inválido: '{raw}'. Usa un identificador de campaña o 'none'.");
    }
}
