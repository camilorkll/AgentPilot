using AgentPilot.Application.Abstractions;
using AgentPilot.Domain.Conversations;

namespace AgentPilot.Application.Feedback;

public interface IFeedbackService
{
    /// <summary>Registra una valoración; lanza KeyNotFoundException si el mensaje no existe.</summary>
    Task SubmitAsync(
        Guid messageId, FeedbackRating rating, string? comment, string? createdBy,
        CancellationToken cancellationToken = default);
}

public class FeedbackService(IFeedbackRepository repository) : IFeedbackService
{
    public async Task SubmitAsync(
        Guid messageId, FeedbackRating rating, string? comment, string? createdBy,
        CancellationToken cancellationToken = default)
    {
        if (!await repository.MessageExistsAsync(messageId, cancellationToken))
            throw new KeyNotFoundException($"El mensaje {messageId} no existe.");

        await repository.AddAsync(
            new Domain.Conversations.Feedback(messageId, rating, comment, createdBy), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
