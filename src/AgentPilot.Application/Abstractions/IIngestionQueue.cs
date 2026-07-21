using AgentPilot.Application.Ingestion;

namespace AgentPilot.Application.Abstractions;

/// <summary>
/// Cola de trabajos de ingesta. Desacopla la petición HTTP (que encola y
/// responde) del worker en segundo plano (que consume y procesa).
/// </summary>
public interface IIngestionQueue
{
    ValueTask EnqueueAsync(IngestionJob job, CancellationToken cancellationToken = default);

    /// <summary>Stream de trabajos para el worker (bloquea hasta que haya alguno).</summary>
    IAsyncEnumerable<IngestionJob> DequeueAllAsync(CancellationToken cancellationToken);
}
