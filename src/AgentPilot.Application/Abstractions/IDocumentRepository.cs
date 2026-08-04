using AgentPilot.Domain.Documents;

namespace AgentPilot.Application.Abstractions;

/// <summary>Persistencia de documentos. Implementado con EF Core en Infrastructure.</summary>
public interface IDocumentRepository
{
    Task AddAsync(Documento document, CancellationToken cancellationToken = default);

    Task<Documento?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca un documento por nombre de fichero **dentro de una campaña**. Se usa para
    /// detectar duplicados: reingerir el mismo fichero duplicaría sus fragmentos en el
    /// índice vectorial y ensuciaría las citas. El mismo nombre en otra campaña no es
    /// un duplicado, porque son corpus independientes.
    /// </summary>
    Task<Documento?> GetByFileNameAsync(
        Guid campaignId, string fileName, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Documento>> ListAsync(
        Guid? campaignId = null, EstadoIngesta? status = null,
        CancellationToken cancellationToken = default);

    void Delete(Documento document);

    /// <summary>Confirma los cambios pendientes (patrón unidad de trabajo).</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
