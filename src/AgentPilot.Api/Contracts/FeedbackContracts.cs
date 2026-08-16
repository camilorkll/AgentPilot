using AgentPilot.Application.Feedback;

namespace AgentPilot.Api.Contracts;

/// <summary>Cuerpo de POST /feedback (esquema FeedbackRequest del OpenAPI).</summary>
public record FeedbackRequest(Guid MessageId, string Rating, string? Comment);

/// <summary>
/// Una respuesta valorada con su contexto mínimo, para el listado de revisión
/// (esquema RatedAnswer del OpenAPI). No incluye la conversación completa a propósito.
/// </summary>
public record RatedAnswerResponse(
    Guid MessageId,
    Guid ConversationId,
    Guid? CampaignId,
    string? CampaignName,
    string? Question,
    string Answer,
    string Rating,
    string? Comment,
    string? RatedBy,
    DateTime RatedAtUtc);

public static class FeedbackMappings
{
    public static RatedAnswerResponse ToResponse(this RatedAnswer a) => new(
        a.MessageId, a.ConversationId, a.CampaignId, a.CampaignName,
        a.Question, a.Answer,
        a.Rating.ToString().ToLowerInvariant(), // positive/negative
        a.Comment, a.RatedBy, a.RatedAtUtc);
}
