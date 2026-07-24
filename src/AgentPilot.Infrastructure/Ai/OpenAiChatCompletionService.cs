using System.Runtime.CompilerServices;
using AgentPilot.Application.Abstractions;
using AgentPilot.Application.Chat;
using AgentPilot.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace AgentPilot.Infrastructure.Ai;

/// <summary>Implementación de IChatCompletionService con la API de chat de OpenAI.</summary>
public class OpenAiChatCompletionService : IChatCompletionService
{
    private readonly OpenAiOptions _options;
    private readonly Lazy<ChatClient> _client;

    public OpenAiChatCompletionService(IOptions<OpenAiOptions> options)
    {
        _options = options.Value;
        _client = new Lazy<ChatClient>(() =>
        {
            if (string.IsNullOrWhiteSpace(_options.ApiKey))
                throw new InvalidOperationException("Falta 'OpenAI:ApiKey' para el chat.");
            return new ChatClient(_options.ChatModel, _options.ApiKey);
        });
    }

    public string ModelName => _options.ChatModel;

    public async IAsyncEnumerable<ChatCompletionChunk> StreamAsync(
        IReadOnlyList<PromptMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Traducimos nuestros mensajes neutros a los tipos de OpenAI.
        var openAiMessages = messages.Select<PromptMessage, ChatMessage>(m => m.Role switch
        {
            PromptRole.System => new SystemChatMessage(m.Content),
            PromptRole.Assistant => new AssistantChatMessage(m.Content),
            _ => new UserChatMessage(m.Content),
        }).ToList();

        var updates = _client.Value.CompleteChatStreamingAsync(
            openAiMessages, cancellationToken: cancellationToken);

        await foreach (var update in updates.WithCancellation(cancellationToken))
        {
            // Deltas de texto según los va generando el modelo.
            foreach (var part in update.ContentUpdate)
                if (!string.IsNullOrEmpty(part.Text))
                    yield return new ChatCompletionChunk(part.Text, Usage: null);

            // El trozo final trae el recuento de tokens (para el coste).
            if (update.Usage is not null)
                yield return new ChatCompletionChunk(
                    TextDelta: null,
                    new ChatUsage(update.Usage.InputTokenCount, update.Usage.OutputTokenCount));
        }
    }
}
