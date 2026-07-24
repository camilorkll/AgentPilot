using AgentPilot.Domain.Conversations;

namespace AgentPilot.Api.Contracts;

/// <summary>Cuerpo de POST /chat/ask.</summary>
public record AskRequest(string Question, Guid? ConversationId);

/// <summary>Cita emitida en el evento SSE 'citations' (esquema Citation del OpenAPI).</summary>
public record CitationDto(
    Guid DocumentId, string DocumentTitle, Guid ChunkId, string Snippet, double Score);

/// <summary>Telemetría emitida en el evento SSE 'usage'.</summary>
public record UsageDto(
    string Model, int PromptTokens, int CompletionTokens, double EstimatedCostUsd, long LatencyMs);

/// <summary>Respuesta de GET /conversations/{id}.</summary>
public record ConversationResponse(
    Guid Id, string? Title, IReadOnlyList<MessageResponse> Messages, DateTime CreatedAtUtc);

public record MessageResponse(
    Guid Id, string Role, string Content, IReadOnlyList<CitationDto> Citations, DateTime CreatedAtUtc);

public static class ChatMappings
{
    public static CitationDto ToDto(this Citation c) => new(
        c.DocumentId, c.DocumentTitle, c.ChunkId,
        c.Snippet.Length <= 300 ? c.Snippet : c.Snippet[..300] + "…",
        Math.Round(c.Score, 4));

    public static ConversationResponse ToResponse(this Conversation c) => new(
        c.Id, c.Title,
        c.Messages
            .OrderBy(m => m.CreatedAtUtc)
            .Select(m => new MessageResponse(
                m.Id,
                m.Role.ToString().ToLowerInvariant(), // user/assistant
                m.Content,
                m.Citations.Select(ToDto).ToList(),
                m.CreatedAtUtc))
            .ToList(),
        c.CreatedAtUtc);
}
