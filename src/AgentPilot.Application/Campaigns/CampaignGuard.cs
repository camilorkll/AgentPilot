using AgentPilot.Application.Abstractions;
using AgentPilot.Domain.Campaigns;

namespace AgentPilot.Application.Campaigns;

/// <summary>
/// Guarda única para los comandos que tocan documentación.
///
/// «No se modifica la documentación de una campaña cerrada» es un invariante que
/// cruza dos agregados: Documento no conoce el estado de su campaña, así que la
/// regla no puede vivir dentro de él. Vive aquí, en un solo sitio, y no repartida
/// por cada endpoint: repartida, tarde o temprano falta en uno, y el que falta no
/// da error — deja pasar el cambio.
/// </summary>
public class CampaignGuard(ICampaignRepository campaigns)
{
    /// <summary>
    /// Devuelve la campaña si existe y admite cambios en su documentación.
    /// Lanza <see cref="KeyNotFoundException"/> si no existe y
    /// <see cref="CampaignClosedException"/> si está cerrada.
    /// </summary>
    public async Task<Campaña> ExigirEditableAsync(
        Guid campaignId, CancellationToken cancellationToken = default)
    {
        var campaign = await BuscarAsync(campaignId, cancellationToken);

        if (!campaign.AdmiteCambiosEnDocumentacion)
            throw new CampaignClosedException(campaign.Id, campaign.Name);

        return campaign;
    }

    /// <summary>
    /// Devuelve la campaña si existe y está activa. Lanza
    /// <see cref="KeyNotFoundException"/> si no existe y
    /// <see cref="CampaignNotActiveException"/> si no admite consultas.
    /// </summary>
    public async Task<Campaña> ExigirActivaAsync(
        Guid campaignId, CancellationToken cancellationToken = default)
    {
        var campaign = await BuscarAsync(campaignId, cancellationToken);

        if (!campaign.AdmiteConsultas)
            throw new CampaignNotActiveException(campaign.Id, campaign.Name);

        return campaign;
    }

    private async Task<Campaña> BuscarAsync(Guid campaignId, CancellationToken cancellationToken)
        => await campaigns.GetByIdAsync(campaignId, cancellationToken)
            ?? throw new KeyNotFoundException($"La campaña {campaignId} no existe.");
}
