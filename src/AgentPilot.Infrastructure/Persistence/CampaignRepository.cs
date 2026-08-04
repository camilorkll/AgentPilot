using AgentPilot.Application.Abstractions;
using AgentPilot.Application.Campaigns;
using AgentPilot.Domain.Campaigns;
using AgentPilot.Domain.Documents;
using Microsoft.EntityFrameworkCore;

namespace AgentPilot.Infrastructure.Persistence;

public class CampaignRepository(AgentPilotDbContext db) : ICampaignRepository
{
    public Task<Campaña?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.Campañas.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<CampaignWithCounts>> ListWithCountsAsync(
        CancellationToken cancellationToken = default)
        // El orden se aplica ANTES de proyectar: una vez que ConCuenta añade las
        // subconsultas de recuento, EF Core ya no puede traducir un OrderBy sobre el
        // resultado (falla con "could not be translated").
        => await ConCuenta(db.Campañas.OrderBy(c => c.Name)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CampaignWithCounts>> ListActiveWithCountsAsync(
        CancellationToken cancellationToken = default)
        => await ConCuenta(db.Campañas.Where(c => c.Status == EstadoCampaña.Activa).OrderBy(c => c.Name))
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsByNameAsync(
        string name, Guid? excludingId = null, CancellationToken cancellationToken = default)
    {
        var normalizado = name.Trim().ToLowerInvariant();
        var query = db.Campañas.Where(c => c.Name.ToLower() == normalizado);
        if (excludingId is Guid id) query = query.Where(c => c.Id != id);
        return query.AnyAsync(cancellationToken);
    }

    public async Task<(int Total, int Active)> CountDocumentsAsync(
        Guid campaignId, CancellationToken cancellationToken = default)
    {
        var total = await db.Documentos.CountAsync(d => d.CampaignId == campaignId, cancellationToken);
        var active = await db.Documentos.CountAsync(
            d => d.CampaignId == campaignId && d.IsActive && d.Status == EstadoIngesta.Ready,
            cancellationToken);
        return (total, active);
    }

    public async Task AddAsync(Campaña campaign, CancellationToken cancellationToken = default)
        => await db.Campañas.AddAsync(campaign, cancellationToken);

    public void Delete(Campaña campaign) => db.Campañas.Remove(campaign);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);

    /// <summary>
    /// Proyecta el volumen del corpus sin cargar los documentos: contarlos no requiere
    /// traerlos, y traerlos traería también sus fragmentos.
    /// </summary>
    private IQueryable<CampaignWithCounts> ConCuenta(IQueryable<Campaña> campañas) =>
        from c in campañas
        select new CampaignWithCounts(
            c,
            db.Documentos.Count(d => d.CampaignId == c.Id),
            db.Documentos.Count(d => d.CampaignId == c.Id
                && d.IsActive && d.Status == EstadoIngesta.Ready));
}
