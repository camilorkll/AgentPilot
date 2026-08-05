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
    DateTime? ClosedAtUtc,
    DateTime CreatedAtUtc);

/// <summary>DTO reducido para el selector del agente (esquema CampaignSummary).</summary>
public record CampaignSummaryResponse(Guid Id, string Name, int ActiveDocumentCount);

/// <summary>Cuerpo de POST/PUT /campaigns: solo el nombre. Las instrucciones del asistente se gestionan en /campaigns/{id}/prompt.</summary>
public record CampaignRequest(string Name);

public record CampaignStatusRequest(string Status);

/// <summary>
/// Esquema AssistantPromptSettings del OpenAPI. Todos los campos son opcionales: un
/// objeto completamente vacío significa "sin instrucciones propias, solo el núcleo".
/// </summary>
public record AssistantPromptRequest(
    string? Tone,
    string? DetailLevel,
    string? MandatoryNotice,
    IReadOnlyList<string>? AvoidWords,
    string? ExtraInstructions);

public record AssistantPromptResponse(
    string? Tone,
    string? DetailLevel,
    string? MandatoryNotice,
    IReadOnlyList<string> AvoidWords,
    string? ExtraInstructions,
    bool IsEmpty);

/// <summary>Respuesta de PUT /campaigns/{id}/prompt y de restaurar una versión.</summary>
public record PromptUpdateResponse(
    AssistantPromptResponse Prompt,
    IReadOnlyList<string> Warnings,
    Guid VersionId,
    DateTime CreatedAtUtc);

/// <summary>Una entrada del historial (esquema PromptVersion del OpenAPI).</summary>
public record PromptVersionResponse(
    Guid Id,
    AssistantPromptResponse Prompt,
    string PublishedBy,
    DateTime CreatedAtUtc);

/// <summary>Cuerpo de POST /campaigns/{id}/prompt/preview: una pregunta de prueba y un candidato sin publicar.</summary>
public record PromptPreviewRequest(
    string Question,
    string? Tone,
    string? DetailLevel,
    string? MandatoryNotice,
    IReadOnlyList<string>? AvoidWords,
    string? ExtraInstructions);

public record PromptPreviewResponse(
    string CurrentAnswer,
    string CandidateAnswer,
    IReadOnlyList<CitationDto> Citations,
    IReadOnlyList<string> Warnings);

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
        c.Campaign.ClosedAtUtc,
        c.Campaign.CreatedAtUtc);

    /// <summary>
    /// Para el detalle de una sola campaña (alta, actualización, cambio de estado), sin
    /// pasar por la proyección de conteo en lista.
    /// </summary>
    public static CampaignResponse ToResponse(this Campaña c, int documentCount, int activeDocumentCount) => new(
        c.Id, c.Name, c.Status.ToDto(), documentCount, activeDocumentCount,
        c.ClosedAtUtc, c.CreatedAtUtc);

    public static CampaignSummaryResponse ToSummary(this CampaignWithCounts c) => new(
        c.Campaign.Id, c.Campaign.Name, c.ActiveDocumentCount);

    public static AssistantPromptSettings ToDomain(this AssistantPromptRequest r) => new(
        r.Tone, r.DetailLevel, r.MandatoryNotice, r.AvoidWords, r.ExtraInstructions);

    public static AssistantPromptResponse ToResponse(this AssistantPromptSettings s) => new(
        s.Tone, s.DetailLevel, s.MandatoryNotice, s.AvoidWords, s.ExtraInstructions, s.EstáVacío);

    public static PromptUpdateResponse ToResponse(this PromptUpdateResult r) => new(
        r.Settings.ToResponse(), r.Warnings, r.VersionId, r.CreatedAtUtc);

    public static PromptVersionResponse ToResponse(this PromptVersion v) => new(
        v.Id, v.ToSettings().ToResponse(), v.PublishedBy, v.CreatedAtUtc);
}
