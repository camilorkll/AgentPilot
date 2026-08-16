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

    /// <summary>Similitud del coseno: parecido de significado con la pregunta.</summary>
    public double Score { get; private set; }

    /// <summary>
    /// Puntuación con la que se ordenaron las fuentes (similitud vectorial + solape
    /// léxico). Es la que explica por qué esta cita va donde va.
    ///
    /// Anulable porque las citas guardadas antes de registrarla no la tienen: se
    /// serializan como JSON, así que aparecen sin el campo. Null significa "no se
    /// registró", que no es lo mismo que cero — cero sería "ninguna relevancia".
    /// </summary>
    public double? Relevance { get; private set; }

    private Citation() { } // EF

    /// <param name="relevance">
    /// Sin valor por defecto a propósito: quien crea una cita conoce el orden en que la
    /// puso, y dejar que se omitiera devolvería justo el problema que esto corrige.
    /// </param>
    public Citation(
        Guid documentId, string documentTitle, Guid chunkId, string snippet,
        double score, double? relevance)
    {
        DocumentId = documentId;
        DocumentTitle = documentTitle;
        ChunkId = chunkId;
        Snippet = snippet;
        Score = score;
        Relevance = relevance;
    }
}
