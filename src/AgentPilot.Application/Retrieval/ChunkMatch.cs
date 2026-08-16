namespace AgentPilot.Application.Retrieval;

/// <summary>
/// Resultado de una búsqueda por similitud: un chunk recuperado, su documento
/// de origen y el score de parecido con la consulta (1 = idéntico, 0 = sin relación).
/// </summary>
public sealed record ChunkMatch
{
    public Guid ChunkId { get; init; }
    public Guid DocumentId { get; init; }
    public string DocumentTitle { get; init; } = string.Empty;
    public int Ordinal { get; init; }
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Similitud del coseno entre la pregunta y el fragmento. Mide parecido de
    /// significado y nada más.
    /// </summary>
    public double Score { get; init; }

    /// <summary>
    /// Puntuación con la que <see cref="ChunkReranker"/> ordena los candidatos: combina
    /// la similitud vectorial con el solape léxico respecto a la pregunta.
    ///
    /// Existe aparte de <see cref="Score"/> porque son cosas distintas y confundirlas se
    /// notaba: la lista de fuentes salía ordenada por esta y etiquetada con aquella, así
    /// que el tercer resultado podía mostrar menos similitud que el cuarto y la
    /// recuperación parecía rota sin estarlo.
    ///
    /// Vale lo mismo que <see cref="Score"/> mientras no se reordene.
    /// </summary>
    public double Relevance { get; init; }
}
