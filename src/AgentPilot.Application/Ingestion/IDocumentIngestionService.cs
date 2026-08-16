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

    /// <summary>
    /// Encola el reindexado de los documentos de una campaña: vuelve a trocear y
    /// vectorizar a partir del texto ya guardado, sin necesitar los ficheros (ADR-012).
    /// Es lo que hay que hacer tras cambiar el troceado o el modelo de embeddings.
    ///
    /// No reindexa los documentos que no tengan texto guardado (ingeridos antes de que
    /// se persistiera) ni los que no estén indexados todavía: se devuelven aparte para
    /// que quien lo pide sepa exactamente qué se ha quedado fuera y por qué.
    ///
    /// Lanza <see cref="KeyNotFoundException"/> si la campaña no existe y
    /// <see cref="Campaigns.CampaignClosedException"/> si está cerrada.
    /// </summary>
    Task<ReindexResult> ReindexCampaignAsync(
        Guid campaignId, CancellationToken cancellationToken = default);
}
