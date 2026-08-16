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

    /// <summary>
    /// Añade una entrada al historial de prompts. Una entrada ya guardada nunca se
    /// actualiza, pero sí puede borrarse: por la purga al superar el límite de la
    /// campaña o a mano por un administrador (ver <see cref="DeletePromptVersion"/>).
    /// </summary>
    Task AddPromptVersionAsync(PromptVersion version, CancellationToken cancellationToken = default);

    /// <summary>Historial de una campaña, más reciente primero.</summary>
    Task<IReadOnlyList<PromptVersion>> ListPromptVersionsAsync(
        Guid campaignId, CancellationToken cancellationToken = default);

    /// <summary>Una versión concreta, o null si no existe o pertenece a otra campaña.</summary>
    Task<PromptVersion?> GetPromptVersionAsync(
        Guid campaignId, Guid versionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Borra una entrada del historial: por purga automática al superar el límite de la
    /// campaña, o por borrado manual de un administrador.
    /// </summary>
    void DeletePromptVersion(PromptVersion version);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
