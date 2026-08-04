using AgentPilot.Application.Abstractions;
using AgentPilot.Domain.Campaigns;

namespace AgentPilot.Application.Campaigns;

/// <summary>
/// Casos de uso de administración de campañas. Las reglas de qué transición es válida
/// viven en <see cref="Campaña"/>; aquí solo se traduce "a qué estado se quiere ir" a
/// "qué método de intención hay que llamar", y se comprueba la unicidad del nombre antes
/// de escribir para poder devolver un error legible.
/// </summary>
public class CampaignService(ICampaignRepository campaigns) : ICampaignService
{
    public Task<IReadOnlyList<CampaignWithCounts>> ListAsync(CancellationToken cancellationToken = default)
        => campaigns.ListWithCountsAsync(cancellationToken);

    public Task<IReadOnlyList<CampaignWithCounts>> ListActiveAsync(CancellationToken cancellationToken = default)
        => campaigns.ListActiveWithCountsAsync(cancellationToken);

    public async Task<CampaignWithCounts> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => await ConCuentaAsync(await BuscarAsync(id, cancellationToken), cancellationToken);

    public async Task<CampaignWithCounts> CreateAsync(
        string name, string? assistantInstructions, CancellationToken cancellationToken = default)
    {
        if (await campaigns.ExistsByNameAsync(name, cancellationToken: cancellationToken))
            throw new DuplicateCampaignNameException(name);

        var campaign = new Campaña(name, assistantInstructions);
        await campaigns.AddAsync(campaign, cancellationToken);
        await campaigns.SaveChangesAsync(cancellationToken);

        // Nace sin documentos: no hace falta ir a contar, ahorra una consulta.
        return new CampaignWithCounts(campaign, 0, 0);
    }

    public async Task<CampaignWithCounts> UpdateAsync(
        Guid id, string name, string? assistantInstructions, CancellationToken cancellationToken = default)
    {
        var campaign = await BuscarAsync(id, cancellationToken);

        if (await campaigns.ExistsByNameAsync(name, id, cancellationToken))
            throw new DuplicateCampaignNameException(name);

        // Renombrar() y CambiarInstrucciones() ya rechazan una campaña cerrada; no se
        // duplica esa comprobación aquí.
        campaign.Renombrar(name);
        campaign.CambiarInstrucciones(assistantInstructions);

        await campaigns.SaveChangesAsync(cancellationToken);
        return await ConCuentaAsync(campaign, cancellationToken);
    }

    public async Task<CampaignWithCounts> SetStatusAsync(
        Guid id, EstadoCampaña status, CancellationToken cancellationToken = default)
    {
        var campaign = await BuscarAsync(id, cancellationToken);

        // "Inactiva" es el destino de dos transiciones distintas (desactivar una activa,
        // reabrir una cerrada); el resto tiene un único método de intención. Las
        // transiciones imposibles las rechaza la propia campaña con su mensaje.
        switch (status)
        {
            case EstadoCampaña.Activa:
                campaign.Activar();
                break;
            case EstadoCampaña.Inactiva when campaign.Status == EstadoCampaña.Cerrada:
                campaign.Reabrir();
                break;
            case EstadoCampaña.Inactiva:
                campaign.Desactivar();
                break;
            case EstadoCampaña.Cerrada:
                campaign.Cerrar();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, "Estado de campaña desconocido.");
        }

        await campaigns.SaveChangesAsync(cancellationToken);
        return await ConCuentaAsync(campaign, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var campaign = await BuscarAsync(id, cancellationToken);

        // Lanza si no está cerrada; el mensaje ya explica los pasos previos.
        campaign.ExigirEliminable();

        campaigns.Delete(campaign); // se lleva documentos y fragmentos por la cascada
        await campaigns.SaveChangesAsync(cancellationToken);
    }

    private async Task<Campaña> BuscarAsync(Guid id, CancellationToken cancellationToken)
        => await campaigns.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"La campaña {id} no existe.");

    private async Task<CampaignWithCounts> ConCuentaAsync(Campaña campaign, CancellationToken cancellationToken)
    {
        var (total, active) = await campaigns.CountDocumentsAsync(campaign.Id, cancellationToken);
        return new CampaignWithCounts(campaign, total, active);
    }
}
