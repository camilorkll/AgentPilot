using AgentPilot.Domain.Conversations;

namespace AgentPilot.Api.Contracts;

/// <summary>
/// Cuerpo de POST /chat/ask. <see cref="CampaignId"/> es obligatorio: sin valor por
/// defecto, para que un olvido del cliente sea un error visible y no una respuesta con
/// documentación de otra campaña.
/// </summary>
public record AskRequest(string Question, Guid? CampaignId, Guid? ConversationId);

/// <summary>
/// Cita emitida en el evento SSE 'citations' (esquema Citation del OpenAPI).
/// <see cref="Score"/> es la similitud del coseno; <see cref="Relevance"/>, la puntuación
/// que fijó el orden de la lista. Van las dos porque no coinciden y solo la segunda
/// explica por qué una cita va antes que otra.
/// </summary>
public record CitationDto(
    Guid DocumentId, string DocumentTitle, Guid ChunkId, string Snippet,
    double Score, double? Relevance);

/// <summary>Telemetría emitida en el evento SSE 'usage'.</summary>
public record UsageDto(
    string Model, int PromptTokens, int CompletionTokens, double EstimatedCostUsd, long LatencyMs);

/// <summary>Respuesta de GET /conversations/{id}.</summary>
public record ConversationResponse(
    Guid Id, Guid? CampaignId, string? Title,
    IReadOnlyList<MessageResponse> Messages, DateTime CreatedAtUtc);

public record MessageResponse(
    Guid Id, string Role, string Content, IReadOnlyList<CitationDto> Citations, DateTime CreatedAtUtc,
    FeedbackDto? Feedback);

/// <summary>
/// Valoración vigente de una respuesta. Null si nadie la ha valorado; nunca hay más de
/// una por mensaje.
/// </summary>
public record FeedbackDto(string Rating, string? Comment, string? CreatedBy, DateTime CreatedAtUtc);

public static class ChatMappings
{
    public static CitationDto ToDto(this Citation c) => new(
        c.DocumentId, c.DocumentTitle, c.ChunkId,
        c.Snippet.Length <= 300 ? c.Snippet : c.Snippet[..300] + "…",
        Math.Round(c.Score, 4),
        c.Relevance is null ? null : Math.Round(c.Relevance.Value, 4));

    public static FeedbackDto ToDto(this Feedback f) => new(
        f.Rating.ToString().ToLowerInvariant(), // positive/negative
        f.Comment, f.CreatedBy, f.CreatedAtUtc);

    /// <summary>
    /// Compone la conversación con las valoraciones de sus mensajes. Se pasan aparte
    /// porque el feedback es otro agregado: el mensaje no lo referencia, para que
    /// valorar no obligue a cargar (ni bloquear) la conversación entera.
    /// </summary>
    public static ConversationResponse ToResponse(
        this Conversation c, IReadOnlyList<Feedback>? feedback = null)
    {
        var porMensaje = feedback?.ToDictionary(f => f.MessageId) ?? [];

        return new ConversationResponse(
            c.Id, c.CampaignId, c.Title,
            c.Messages
                .OrderBy(m => m.CreatedAtUtc)
                .Select(m => new MessageResponse(
                    m.Id,
                    m.Role.ToString().ToLowerInvariant(), // user/assistant
                    m.Content,
                    m.Citations.Select(ToDto).ToList(),
                    m.CreatedAtUtc,
                    porMensaje.TryGetValue(m.Id, out var f) ? f.ToDto() : null))
                .ToList(),
            c.CreatedAtUtc);
    }
}
