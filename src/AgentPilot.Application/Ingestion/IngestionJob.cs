namespace AgentPilot.Application.Ingestion;

/// <summary>
/// Trabajo de ingesta encolado. Lleva los bytes del fichero para que el worker
/// pueda procesarlo después de que la petición HTTP haya terminado.
///
/// Con <see cref="Content"/> a null es un **reindexado**: no hay fichero porque el
/// texto ya está guardado en el documento (ADR-012), y el worker vuelve a trocear y
/// vectorizar a partir de él. Se modela como el mismo trabajo y no como uno aparte
/// porque el trabajo pesado —trocear, vectorizar, indexar— es idéntico; lo único que
/// cambia es de dónde sale el texto.
/// </summary>
public sealed record IngestionJob(Guid DocumentId, string FileName, byte[]? Content)
{
    /// <summary>Trabajo de reindexado: sin fichero, el texto sale del propio documento.</summary>
    public static IngestionJob Reindexado(Guid documentId, string fileName)
        => new(documentId, fileName, null);

    public bool EsReindexado => Content is null;
}
