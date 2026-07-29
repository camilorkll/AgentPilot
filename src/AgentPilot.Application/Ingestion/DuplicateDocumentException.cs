namespace AgentPilot.Application.Ingestion;

/// <summary>
/// Se lanza al intentar ingerir un fichero que ya está en la base de conocimiento.
/// Lleva el identificador del documento existente para que el cliente pueda ofrecer
/// reemplazarlo.
/// </summary>
public class DuplicateDocumentException(Guid existingDocumentId, string fileName)
    : Exception($"El documento '{fileName}' ya está en la base de conocimiento.")
{
    public Guid ExistingDocumentId { get; } = existingDocumentId;
    public string FileName { get; } = fileName;
}
