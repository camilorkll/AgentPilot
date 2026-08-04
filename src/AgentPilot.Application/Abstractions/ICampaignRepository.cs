using AgentPilot.Application.Campaigns;
using AgentPilot.Domain.Campaigns;

namespace AgentPilot.Application.Abstractions;

/// <summary>Persistencia de campañas. Implementado con EF Core en Infrastructure.</summary>
public interface ICampaignRepository
{
    Task<Campaña?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Todas las campañas con el volumen de su corpus, para el mantenimiento del
    /// administrador.
    /// </summary>
    Task<IReadOnlyList<CampaignWithCounts>> ListWithCountsAsync(CancellationToken cancellationToken = default);

    /// <summary>Solo las activas, para el selector del agente.</summary>
    Task<IReadOnlyList<CampaignWithCounts>> ListActiveWithCountsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Volumen del corpus de una campaña. Se usa para componer la respuesta tras
    /// crearla, renombrarla o cambiar su estado, sin tener que repetir la proyección de
    /// la lista para un único elemento.
    /// </summary>
    Task<(int Total, int Active)> CountDocumentsAsync(
        Guid campaignId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Comprueba si ya existe una campaña con ese nombre (sin distinguir mayúsculas),
    /// excluyendo opcionalmente una campaña (para validar un renombrado contra las
    /// demás). Se consulta antes de escribir para poder devolver un 409 legible; el
    /// índice único de la base de datos sigue siendo la garantía de fondo.
    /// </summary>
    Task<bool> ExistsByNameAsync(
        string name, Guid? excludingId = null, CancellationToken cancellationToken = default);

    Task AddAsync(Campaña campaign, CancellationToken cancellationToken = default);

    void Delete(Campaña campaign);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
