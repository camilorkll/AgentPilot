namespace AgentPilot.Domain.Conversations;

/// <summary>
/// Referencia a la fuente usada en una respuesta: el chunk concreto de un
/// documento, con el fragmento citado y su score de similitud. Es un value
/// object: se guarda embebido en el mensaje del asistente.
/// </summary>
public class Citation
{
    public Guid DocumentId { get; private set; }
    public string DocumentTitle { get; private set; } = string.Empty;
    public Guid ChunkId { get; private set; }
    public string Snippet { get; private set; } = string.Empty;
    public double Score { get; private set; }

    private Citation() { } // EF

    public Citation(Guid documentId, string documentTitle, Guid chunkId, string snippet, double score)
    {
        DocumentId = documentId;
        DocumentTitle = documentTitle;
        ChunkId = chunkId;
        Snippet = snippet;
        Score = score;
    }
}
