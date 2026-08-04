using AgentPilot.Application.Abstractions;
using AgentPilot.Domain.Documents;
using Microsoft.EntityFrameworkCore;

namespace AgentPilot.Infrastructure.Persistence;

public class DocumentRepository(AgentPilotDbContext db) : IDocumentRepository
{
    public async Task AddAsync(Documento document, CancellationToken cancellationToken = default)
        => await db.Documentos.AddAsync(document, cancellationToken);

    public Task<Documento?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.Documentos.Include(d => d.Chunks)
             .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<Documento?> GetByFileNameAsync(
        Guid campaignId, string fileName, CancellationToken cancellationToken = default)
        => db.Documentos.FirstOrDefaultAsync(
            d => d.CampaignId == campaignId && d.FileName == fileName, cancellationToken);

    public async Task<IReadOnlyList<Documento>> ListAsync(
        Guid? campaignId = null, EstadoIngesta? status = null,
        CancellationToken cancellationToken = default)
    {
        // No cargamos los chunks (pueden ser muchos); ChunkCount ya vive en Documento.
        var query = db.Documentos.AsQueryable();
        if (campaignId is not null)
            query = query.Where(d => d.CampaignId == campaignId);
        if (status is not null)
            query = query.Where(d => d.Status == status);
        return await query
            .OrderByDescending(d => d.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public void Delete(Documento document) => db.Documentos.Remove(document);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);
}
