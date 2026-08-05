using System.Text.Json;

namespace AgentPilot.Domain.Campaigns;

/// <summary>
/// Fotografía de las instrucciones del asistente de una campaña en un momento dado.
/// Un prompt es configuración que cambia el comportamiento del producto: merece
/// histórico y responsable, igual que un despliegue. Se guarda una fila por cada
/// cambio (incluido "restaurar por defecto" y cada restauración de una versión
/// anterior), nunca se sobrescribe: es un registro de auditoría, no una caché.
/// </summary>
public class PromptVersion
{
    public Guid Id { get; private set; }
    public Guid CampaignId { get; private set; }

    /// <summary>Fotografía serializada de <see cref="AssistantPromptSettings"/> en ese momento.</summary>
    public string SettingsJson { get; private set; } = "{}";

    /// <summary>Quién publicó este cambio (usuario autenticado que hizo la petición).</summary>
    public string PublishedBy { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    private PromptVersion() { } // EF Core

    public PromptVersion(Guid campaignId, AssistantPromptSettings settings, string publishedBy)
    {
        Id = Guid.NewGuid();
        CampaignId = campaignId;
        SettingsJson = JsonSerializer.Serialize(new PromptSettingsSnapshot(
            settings.Tone, settings.DetailLevel, settings.MandatoryNotice,
            settings.AvoidWords, settings.ExtraInstructions));
        PublishedBy = string.IsNullOrWhiteSpace(publishedBy) ? "desconocido" : publishedBy;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public AssistantPromptSettings ToSettings()
    {
        var snapshot = JsonSerializer.Deserialize<PromptSettingsSnapshot>(SettingsJson)
            ?? new PromptSettingsSnapshot(null, null, null, [], null);
        return new AssistantPromptSettings(
            snapshot.Tone, snapshot.DetailLevel, snapshot.MandatoryNotice,
            snapshot.AvoidWords, snapshot.ExtraInstructions);
    }

    /// <summary>
    /// Forma serializable de <see cref="AssistantPromptSettings"/>: la clase de dominio
    /// valida en el constructor y no tiene uno vacío público, así que no se puede
    /// deserializar directamente sobre ella.
    /// </summary>
    private sealed record PromptSettingsSnapshot(
        string? Tone, string? DetailLevel, string? MandatoryNotice,
        IReadOnlyList<string> AvoidWords, string? ExtraInstructions);
}
