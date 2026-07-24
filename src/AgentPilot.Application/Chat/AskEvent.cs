using AgentPilot.Domain.Conversations;

namespace AgentPilot.Application.Chat;

/// <summary>
/// Eventos que emite la orquestación RAG según avanza, para que el endpoint SSE
/// los reenvíe al cliente: primero los tokens, luego las citas, el uso y el fin.
/// </summary>
public abstract record AskEvent;

/// <summary>Un fragmento de texto de la respuesta según lo genera el modelo.</summary>
public sealed record TokenEvent(string Text) : AskEvent;

/// <summary>Las fuentes usadas (chunks recuperados). Se emite una vez.</summary>
public sealed record CitationsEvent(IReadOnlyList<Citation> Citations) : AskEvent;

/// <summary>Tokens consumidos, coste estimado y latencia de la llamada al LLM.</summary>
public sealed record UsageEvent(
    string Model, int PromptTokens, int CompletionTokens, double EstimatedCostUsd, long LatencyMs) : AskEvent;

/// <summary>Fin del stream; lleva el id de la conversación (útil si era nueva).</summary>
public sealed record DoneEvent(Guid ConversationId) : AskEvent;
