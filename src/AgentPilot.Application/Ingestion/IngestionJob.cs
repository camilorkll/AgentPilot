namespace AgentPilot.Application.Ingestion;

/// <summary>
/// Trabajo de ingesta encolado. Lleva los bytes del fichero para que el worker
/// pueda procesarlo después de que la petición HTTP haya terminado.
/// </summary>
public sealed record IngestionJob(Guid DocumentId, string FileName, byte[] Content);
