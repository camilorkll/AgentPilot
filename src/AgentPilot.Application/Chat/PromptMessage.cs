namespace AgentPilot.Application.Chat;

/// <summary>Rol de un mensaje del prompt enviado al LLM.</summary>
public enum PromptRole { System, User, Assistant }

/// <summary>Un mensaje del prompt (rol + contenido). Neutro respecto al proveedor.</summary>
public sealed record PromptMessage(PromptRole Role, string Content);

/// <summary>Tokens consumidos por una llamada al LLM (para telemetría y coste).</summary>
public sealed record ChatUsage(int PromptTokens, int CompletionTokens);

/// <summary>
/// Un trozo del stream del LLM: o un delta de texto (TextDelta), o la
/// información de uso final (Usage). Nunca los dos a la vez.
/// </summary>
public sealed record ChatCompletionChunk(string? TextDelta, ChatUsage? Usage);
