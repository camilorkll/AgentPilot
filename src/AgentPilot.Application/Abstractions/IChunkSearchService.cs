using AgentPilot.Application.Retrieval;

namespace AgentPilot.Application.Abstractions;

/// <summary>
/// Búsqueda por similitud: dado el vector de una consulta, devuelve los chunks
/// más cercanos (mayor similitud coseno) de documentos ya indexados.
/// </summary>
public interface IChunkSearchService
{
    /// <summary>
    /// Busca dentro de una campaña. <paramref name="campaignId"/> es obligatorio y
    /// **no hay sobrecarga que permita buscar en todas**: el aislamiento entre
    /// campañas es un requisito de seguridad, y con un filtro opcional un olvido no
    /// daría error, devolvería documentación de otro cliente.
    /// </summary>
    Task<IReadOnlyList<ChunkMatch>> SearchAsync(
        float[] queryEmbedding, Guid campaignId, int topK = 5,
        CancellationToken cancellationToken = default);
}
