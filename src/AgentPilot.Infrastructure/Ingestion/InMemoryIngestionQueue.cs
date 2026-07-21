using System.Threading.Channels;
using AgentPilot.Application.Abstractions;
using AgentPilot.Application.Ingestion;

namespace AgentPilot.Infrastructure.Ingestion;

/// <summary>
/// Cola de ingesta en memoria (System.Threading.Channels). Suficiente para el
/// MVP; en producción con varias instancias se sustituiría por una cola externa
/// (Redis, RabbitMQ, Azure Service Bus) sin tocar el resto de la aplicación.
/// </summary>
public class InMemoryIngestionQueue : IIngestionQueue
{
    private readonly Channel<IngestionJob> _channel =
        Channel.CreateUnbounded<IngestionJob>(new UnboundedChannelOptions { SingleReader = true });

    public ValueTask EnqueueAsync(IngestionJob job, CancellationToken cancellationToken = default)
        => _channel.Writer.WriteAsync(job, cancellationToken);

    public IAsyncEnumerable<IngestionJob> DequeueAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}
