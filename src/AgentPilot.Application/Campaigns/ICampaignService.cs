using AgentPilot.Domain.Campaigns;

namespace AgentPilot.Application.Campaigns;

public interface ICampaignService
{
    Task<IReadOnlyList<CampaignWithCounts>> ListAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CampaignWithCounts>> ListActiveAsync(CancellationToken cancellationToken = default);

    Task<CampaignWithCounts> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Crea una campaña activa. Lanza <see cref="DuplicateCampaignNameException"/> si el nombre ya existe.</summary>
    Task<CampaignWithCounts> CreateAsync(
        string name, string? assistantInstructions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renombra y/o cambia las instrucciones. Lanza <see cref="CampaignClosedException"/>
    /// si está cerrada y <see cref="DuplicateCampaignNameException"/> si el nuevo nombre
    /// ya lo usa otra campaña.
    /// </summary>
    Task<CampaignWithCounts> UpdateAsync(
        Guid id, string name, string? assistantInstructions, CancellationToken cancellationToken = default);

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
}
