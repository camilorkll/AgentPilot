using AgentPilot.Application.Retrieval;

namespace AgentPilot.Application.Abstractions;

/// <summary>
/// Búsqueda por similitud: dado el vector de una consulta, devuelve los chunks
/// más cercanos (mayor similitud coseno) de documentos ya indexados.
/// </summary>
public interface IChunkSearchService
{
    Task<IReadOnlyList<ChunkMatch>> SearchAsync(
        float[] queryEmbedding, int topK = 5, CancellationToken cancellationToken = default);
}
