using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AgentPilot.Application.Abstractions;
using AgentPilot.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace AgentPilot.Infrastructure.Ai;

/// <summary>
/// Implementación de IEmbeddingService contra un servidor Ollama local, vía su
/// API REST (POST /api/embeddings). Mismo contrato que OpenAI: el resto de la
/// aplicación no distingue cuál está activa.
/// </summary>
public class OllamaEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _http;
    private readonly EmbeddingsOptions _options;

    public OllamaEmbeddingService(HttpClient http, IOptions<EmbeddingsOptions> options)
    {
        _options = options.Value;
        _http = http;
        _http.BaseAddress = new Uri(_options.OllamaBaseUrl);
    }

    public string ModelName => _options.OllamaModel;

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var request = new OllamaEmbeddingRequest(_options.OllamaModel, text);
        var response = await _http.PostAsJsonAsync("/api/embeddings", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content
            .ReadFromJsonAsync<OllamaEmbeddingResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Respuesta vacía de Ollama.");

        return payload.Embedding;
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        // La API clásica de Ollama genera un embedding por petición: iteramos.
        var result = new List<float[]>(texts.Count);
        foreach (var text in texts)
            result.Add(await EmbedAsync(text, cancellationToken));
        return result;
    }

    // Ollama usa claves en minúscula: las mapeamos explícitamente.
    private record OllamaEmbeddingRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("prompt")] string Prompt);

    private record OllamaEmbeddingResponse(
        [property: JsonPropertyName("embedding")] float[] Embedding);
}
