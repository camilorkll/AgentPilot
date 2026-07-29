using AgentPilot.Domain.Documents;

namespace AgentPilot.Application.Ingestion;

public interface IDocumentIngestionService
{
    /// <summary>
    /// Fase síncrona: crea el documento (Pending), lo guarda y encola su ingesta.
    /// Si ya existe un documento con el mismo nombre de fichero, lanza
    /// <see cref="DuplicateDocumentException"/>; con <paramref name="replaceExisting"/>
    /// se elimina el anterior (y sus fragmentos) antes de ingerir el nuevo.
    /// </summary>
    Task<Documento> SubmitAsync(
        string fileName, string? title, Stream content,
        bool replaceExisting = false, CancellationToken cancellationToken = default);

    /// <summary>Fase de fondo: extrae, trocea, vectoriza e indexa el documento.</summary>
    Task ProcessAsync(IngestionJob job, CancellationToken cancellationToken = default);
}
