using AgentPilot.Application.Chat;

namespace AgentPilot.Application.Abstractions;

/// <summary>
/// Puerto de generación de texto con un LLM en streaming. La orquestación RAG
/// depende de esta abstracción; la implementación (OpenAI) vive en Infrastructure.
/// </summary>
public interface IChatCompletionService
{
    string ModelName { get; }

    /// <summary>
    /// Genera la respuesta a los mensajes dados, emitiendo trozos según llegan:
    /// primero los deltas de texto y, al final, un trozo con el uso de tokens.
    /// </summary>
    IAsyncEnumerable<ChatCompletionChunk> StreamAsync(
        IReadOnlyList<PromptMessage> messages, CancellationToken cancellationToken = default);
}
