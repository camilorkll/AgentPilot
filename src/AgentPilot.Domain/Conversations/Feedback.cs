namespace AgentPilot.Domain.Conversations;

/// <summary>Valoración de una respuesta: pulgar arriba o abajo.</summary>
public enum FeedbackRating { Positive, Negative }

/// <summary>
/// Valoración que un agente da a una respuesta del asistente (un mensaje).
/// Alimenta el dashboard de calidad y el dataset de evaluación.
/// </summary>
public class Feedback
{
    public Guid Id { get; private set; }
    public Guid MessageId { get; private set; }
    public FeedbackRating Rating { get; private set; }
    public string? Comment { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private Feedback() { } // EF

    public Feedback(Guid messageId, FeedbackRating rating, string? comment, string? createdBy)
    {
        Id = Guid.NewGuid();
        MessageId = messageId;
        Rating = rating;
        Comment = comment;
        CreatedBy = createdBy;
        CreatedAtUtc = DateTime.UtcNow;
    }
}
