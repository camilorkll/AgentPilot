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
        string name, CancellationToken cancellationToken = default)
    {
        if (await campaigns.ExistsByNameAsync(name, cancellationToken: cancellationToken))
            throw new DuplicateCampaignNameException(name);

        var campaign = new Campaña(name);
        await campaigns.AddAsync(campaign, cancellationToken);
        await campaigns.SaveChangesAsync(cancellationToken);

        // Nace sin documentos: no hace falta ir a contar, ahorra una consulta.
        return new CampaignWithCounts(campaign, 0, 0);
    }

    public async Task<CampaignWithCounts> UpdateAsync(
        Guid id, string name, CancellationToken cancellationToken = default)
    {
        var campaign = await BuscarAsync(id, cancellationToken);

        if (await campaigns.ExistsByNameAsync(name, id, cancellationToken))
            throw new DuplicateCampaignNameException(name);

        // Renombrar() ya rechaza una campaña cerrada; no se duplica esa comprobación aquí.
        campaign.Renombrar(name);

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

    public async Task<AssistantPromptSettings> GetPromptAsync(Guid id, CancellationToken cancellationToken = default)
        => (await BuscarAsync(id, cancellationToken)).AssistantPrompt;

    public async Task<PromptUpdateResult> UpdatePromptAsync(
        Guid id, AssistantPromptSettings settings, string publishedBy,
        CancellationToken cancellationToken = default)
    {
        var campaign = await BuscarAsync(id, cancellationToken);
        return await AplicarPromptAsync(campaign, settings, publishedBy, cancellationToken);
    }

    public async Task<IReadOnlyList<PromptVersion>> ListPromptVersionsAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        await BuscarAsync(id, cancellationToken); // 404 legible si la campaña no existe
        return await campaigns.ListPromptVersionsAsync(id, cancellationToken);
    }

    public async Task<PromptUpdateResult> RestorePromptVersionAsync(
        Guid id, Guid versionId, string publishedBy, CancellationToken cancellationToken = default)
    {
        var campaign = await BuscarAsync(id, cancellationToken);

        var version = await campaigns.GetPromptVersionAsync(id, versionId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"La versión {versionId} no existe o no pertenece a la campaña {id}.");

        // Restaurar es publicar de nuevo: crea una entrada de historial propia, no
        // reescribe ni borra la versión de la que parte.
        return await AplicarPromptAsync(campaign, version.ToSettings(), publishedBy, cancellationToken);
    }

    private async Task<PromptUpdateResult> AplicarPromptAsync(
        Campaña campaign, AssistantPromptSettings settings, string publishedBy,
        CancellationToken cancellationToken)
    {
        // CambiarInstruccionesDelAsistente ya rechaza una campaña cerrada.
        campaign.CambiarInstruccionesDelAsistente(settings);

        var version = new PromptVersion(campaign.Id, settings, publishedBy);
        await campaigns.AddPromptVersionAsync(version, cancellationToken);
        await campaigns.SaveChangesAsync(cancellationToken);

        return new PromptUpdateResult(settings, settings.AdviertePatronesSospechosos(), version.Id, version.CreatedAtUtc);
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
