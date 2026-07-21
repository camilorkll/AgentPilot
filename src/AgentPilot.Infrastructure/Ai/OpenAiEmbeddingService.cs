using AgentPilot.Application.Abstractions;
using AgentPilot.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using OpenAI.Embeddings;

namespace AgentPilot.Infrastructure.Ai;

/// <summary>Implementación de IEmbeddingService contra la API de OpenAI.</summary>
public class OpenAiEmbeddingService : IEmbeddingService
{
    private readonly OpenAiOptions _options;
    private readonly Lazy<EmbeddingClient> _client;

    public OpenAiEmbeddingService(IOptions<OpenAiOptions> options)
    {
        _options = options.Value;

        // Creación perezosa: la app arranca sin clave; solo se exige al usarla.
        _client = new Lazy<EmbeddingClient>(() =>
        {
            if (string.IsNullOrWhiteSpace(_options.ApiKey))
                throw new InvalidOperationException(
                    "Falta la clave 'OpenAI:ApiKey' para generar embeddings.");
            return new EmbeddingClient(_options.EmbeddingModel, _options.ApiKey);
        });
    }

    public string ModelName => _options.EmbeddingModel;

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var result = await _client.Value.GenerateEmbeddingAsync(text, cancellationToken: cancellationToken);
        return result.Value.ToFloats().ToArray();
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        // OpenAI acepta muchos textos en una sola petición: 1 llamada para N chunks.
        var result = await _client.Value.GenerateEmbeddingsAsync(texts, cancellationToken: cancellationToken);
        return result.Value.Select(e => e.ToFloats().ToArray()).ToList();
    }
}
