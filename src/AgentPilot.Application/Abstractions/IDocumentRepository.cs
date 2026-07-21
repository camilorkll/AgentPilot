using AgentPilot.Domain.Documents;

namespace AgentPilot.Application.Abstractions;

/// <summary>Persistencia de documentos. Implementado con EF Core en Infrastructure.</summary>
public interface IDocumentRepository
{
    Task AddAsync(Documento document, CancellationToken cancellationToken = default);

    Task<Documento?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Documento>> ListAsync(
        EstadoIngesta? status = null, CancellationToken cancellationToken = default);

    void Delete(Documento document);

    /// <summary>Confirma los cambios pendientes (patrón unidad de trabajo).</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
