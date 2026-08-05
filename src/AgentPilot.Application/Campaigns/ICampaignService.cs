using AgentPilot.Domain.Campaigns;

namespace AgentPilot.Application.Campaigns;

public interface ICampaignService
{
    Task<IReadOnlyList<CampaignWithCounts>> ListAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CampaignWithCounts>> ListActiveAsync(CancellationToken cancellationToken = default);

    Task<CampaignWithCounts> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Crea una campaña activa, sin instrucciones propias. Lanza <see cref="DuplicateCampaignNameException"/> si el nombre ya existe.</summary>
    Task<CampaignWithCounts> CreateAsync(
        string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renombra la campaña. Lanza <see cref="InvalidOperationException"/> si está
    /// cerrada y <see cref="DuplicateCampaignNameException"/> si el nuevo nombre ya
    /// lo usa otra campaña. Las instrucciones del asistente se gestionan aparte, ver
    /// <see cref="GetPromptAsync"/>/<see cref="UpdatePromptAsync"/>.
    /// </summary>
    Task<CampaignWithCounts> UpdateAsync(
        Guid id, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aplica una transición de estado. Lanza <see cref="InvalidOperationException"/> si
    /// la transición no es válida desde el estado actual (la propia campaña decide).
    /// </summary>
    Task<CampaignWithCounts> SetStatusAsync(
        Guid id, EstadoCampaña status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina la campaña y, en cascada, su corpus. Lanza
    /// <see cref="InvalidOperationException"/> si no está cerrada.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Instrucciones vigentes de la campaña para el asistente.</summary>
    Task<AssistantPromptSettings> GetPromptAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sustituye las instrucciones vigentes y añade una entrada al historial (nunca se
    /// sobrescribe una entrada anterior). Lanza <see cref="InvalidOperationException"/>
    /// si la campaña está cerrada.
    /// </summary>
    Task<PromptUpdateResult> UpdatePromptAsync(
        Guid id, AssistantPromptSettings settings, string publishedBy,
        CancellationToken cancellationToken = default);

    /// <summary>Historial de instrucciones de la campaña, más reciente primero.</summary>
    Task<IReadOnlyList<PromptVersion>> ListPromptVersionsAsync(
        Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Vuelve a aplicar las instrucciones de una versión pasada del historial. Esto
    /// crea una nueva entrada de historial (una restauración es un cambio como
    /// cualquier otro), nunca borra ni reescribe las que ya existían. Lanza
    /// <see cref="KeyNotFoundException"/> si la versión no existe o no es de esta
    /// campaña, e <see cref="InvalidOperationException"/> si la campaña está cerrada.
    /// </summary>
    Task<PromptUpdateResult> RestorePromptVersionAsync(
        Guid id, Guid versionId, string publishedBy, CancellationToken cancellationToken = default);
}
