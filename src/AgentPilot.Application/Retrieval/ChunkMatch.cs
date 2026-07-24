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
    public double Score { get; init; }
}
