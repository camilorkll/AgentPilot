using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentPilot.Application.Abstractions;
using AgentPilot.Application.Chat;
using AgentPilot.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace AgentPilot.Infrastructure.Ai;

/// <summary>
/// Implementación de IChatCompletionService contra la API nativa de Ollama
/// (POST /api/chat, streaming NDJSON: una línea de JSON por trozo). No se usa el
/// endpoint compatible con OpenAI porque ese no expone <c>num_ctx</c>, y fijarlo
/// explícitamente es obligatorio: sin ello Ollama trunca en silencio el contexto
/// (por defecto 2048 tokens) y el síntoma es desconcertante —el asistente se
/// abstiene o responde mal aunque las fuentes aparezcan bien en pantalla—.
///
/// Solo para uso local (comparativa medida frente a OpenAI, ver
/// evals/COMPARATIVA-MODELOS.md); Ollama no va a producción.
/// </summary>
public class OllamaChatCompletionService : IChatCompletionService
{
    private readonly HttpClient _http;
    private readonly ChatOptions _options;

    public OllamaChatCompletionService(HttpClient http, IOptions<ChatOptions> options)
    {
        _options = options.Value;
        _http = http;
        _http.BaseAddress = new Uri(_options.OllamaBaseUrl);
    }

    public string ModelName => _options.OllamaModel;

    public async IAsyncEnumerable<ChatCompletionChunk> StreamAsync(
        IReadOnlyList<PromptMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = new OllamaChatRequest(
            _options.OllamaModel,
            messages.Select(m => new OllamaChatMessage(RoleName(m.Role), m.Content)).ToList(),
            Stream: true,
            new OllamaChatRequestOptions(_options.OllamaNumCtx));

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = JsonContent.Create(request),
        };

        using var response = await _http.SendAsync(
            httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        // NDJSON: una línea de JSON completa por trozo, no un único documento.
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line)) continue;

            var trozo = JsonSerializer.Deserialize<OllamaChatStreamLine>(line)
                ?? throw new InvalidOperationException("Respuesta de Ollama sin formato reconocible.");

            if (trozo.Message?.Content is { Length: > 0 } texto)
                yield return new ChatCompletionChunk(texto, Usage: null);

            // El último trozo (done=true) trae el recuento de tokens nativo de Ollama:
            // no es directamente comparable con el de OpenAI (tokenizador distinto),
            // pero permite calcular tokens/segundo para la comparativa de rendimiento.
            if (trozo.Done)
                yield return new ChatCompletionChunk(
                    TextDelta: null,
                    new ChatUsage(trozo.PromptEvalCount ?? 0, trozo.EvalCount ?? 0));
        }
    }

    private static string RoleName(PromptRole role) => role switch
    {
        PromptRole.System => "system",
        PromptRole.Assistant => "assistant",
        _ => "user",
    };

    // Ollama usa claves en minúscula (snake_case en algunas): las mapeamos explícitamente.
    private sealed record OllamaChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<OllamaChatMessage> Messages,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("options")] OllamaChatRequestOptions Options);

    private sealed record OllamaChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record OllamaChatRequestOptions(
        [property: JsonPropertyName("num_ctx")] int NumCtx);

    private sealed record OllamaChatStreamLine(
        [property: JsonPropertyName("message")] OllamaResponseMessage? Message,
        [property: JsonPropertyName("done")] bool Done,
        [property: JsonPropertyName("prompt_eval_count")] int? PromptEvalCount,
        [property: JsonPropertyName("eval_count")] int? EvalCount);

    private sealed record OllamaResponseMessage(
        [property: JsonPropertyName("content")] string Content);
}
