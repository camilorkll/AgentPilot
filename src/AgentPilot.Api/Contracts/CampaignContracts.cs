using AgentPilot.Application.Campaigns;
using AgentPilot.Domain.Campaigns;

namespace AgentPilot.Api.Contracts;

/// <summary>DTO de administración (esquema Campaign del OpenAPI).</summary>
public record CampaignResponse(
    Guid Id,
    string Name,
    string Status,
    int DocumentCount,
    int ActiveDocumentCount,
    string? AssistantInstructions,
    DateTime? ClosedAtUtc,
    DateTime CreatedAtUtc);

/// <summary>DTO reducido para el selector del agente (esquema CampaignSummary).</summary>
public record CampaignSummaryResponse(Guid Id, string Name, int ActiveDocumentCount);

public record CampaignRequest(string Name, string? AssistantInstructions);

public record CampaignStatusRequest(string Status);

public static class CampaignMappings
{
    /// <summary>
    /// El estado se expone con nombre (inactive/active/closed) aunque se persista como
    /// entero: un "status": 2 en la respuesta obligaría a buscar la tabla de
    /// conversión, y el contrato ya lo documenta así.
    /// </summary>
    public static string ToDto(this EstadoCampaña status) => status switch
    {
        EstadoCampaña.Inactiva => "inactive",
        EstadoCampaña.Activa => "active",
        EstadoCampaña.Cerrada => "closed",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public static EstadoCampaña ParseStatus(string status) => status switch
    {
        "inactive" => EstadoCampaña.Inactiva,
        "active" => EstadoCampaña.Activa,
        "closed" => EstadoCampaña.Cerrada,
        _ => throw new FormatException(
            $"Estado de campaña desconocido: '{status}'. Valores válidos: inactive, active, closed."),
    };

    public static CampaignResponse ToResponse(this CampaignWithCounts c) => new(
        c.Campaign.Id,
        c.Campaign.Name,
        c.Campaign.Status.ToDto(),
        c.DocumentCount,
        c.ActiveDocumentCount,
        c.Campaign.AssistantInstructions,
        c.Campaign.ClosedAtUtc,
        c.Campaign.CreatedAtUtc);

    /// <summary>
    /// Para el detalle de una sola campaña (alta, actualización, cambio de estado), sin
    /// pasar por la proyección de conteo en lista.
    /// </summary>
    public static CampaignResponse ToResponse(this Campaña c, int documentCount, int activeDocumentCount) => new(
        c.Id, c.Name, c.Status.ToDto(), documentCount, activeDocumentCount,
        c.AssistantInstructions, c.ClosedAtUtc, c.CreatedAtUtc);

    public static CampaignSummaryResponse ToSummary(this CampaignWithCounts c) => new(
        c.Campaign.Id, c.Campaign.Name, c.ActiveDocumentCount);
}
