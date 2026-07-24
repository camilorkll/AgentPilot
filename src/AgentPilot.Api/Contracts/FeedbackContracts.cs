namespace AgentPilot.Api.Contracts;

/// <summary>Cuerpo de POST /feedback (esquema FeedbackRequest del OpenAPI).</summary>
public record FeedbackRequest(Guid MessageId, string Rating, string? Comment);
