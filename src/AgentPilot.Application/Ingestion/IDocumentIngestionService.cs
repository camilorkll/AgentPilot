using AgentPilot.Domain.Documents;

namespace AgentPilot.Application.Ingestion;

public interface IDocumentIngestionService
{
    /// <summary>
    /// Fase síncrona: crea el documento (Pending) en la campaña indicada, lo guarda y
    /// encola su ingesta. La campaña es obligatoria y no tiene valor por defecto: es
    /// la frontera que impide que el asistente use documentación de otra campaña.
    ///
    /// Lanza <see cref="KeyNotFoundException"/> si la campaña no existe y
    /// <see cref="Campaigns.CampaignClosedException"/> si está cerrada. Si esa campaña
    /// ya tiene un fichero con el mismo nombre lanza
    /// <see cref="DuplicateDocumentException"/>; con <paramref name="replaceExisting"/>
    /// se elimina el anterior (y sus fragmentos) antes de ingerir el nuevo.
    /// </summary>
    Task<Documento> SubmitAsync(
        Guid campaignId, string fileName, string? title, Stream content,
        bool replaceExisting = false, CancellationToken cancellationToken = default);

    /// <summary>Fase de fondo: extrae, trocea, vectoriza e indexa el documento.</summary>
    Task ProcessAsync(IngestionJob job, CancellationToken cancellationToken = default);
}
