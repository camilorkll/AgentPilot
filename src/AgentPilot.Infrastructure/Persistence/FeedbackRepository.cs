using AgentPilot.Application.Abstractions;
using AgentPilot.Domain.Conversations;
using Microsoft.EntityFrameworkCore;

namespace AgentPilot.Infrastructure.Persistence;

public class FeedbackRepository(AgentPilotDbContext db) : IFeedbackRepository
{
    public Task<bool> MessageExistsAsync(Guid messageId, CancellationToken cancellationToken = default)
        => db.Set<Message>().AnyAsync(m => m.Id == messageId, cancellationToken);

    public async Task AddAsync(Feedback feedback, CancellationToken cancellationToken = default)
        => await db.Feedback.AddAsync(feedback, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);
}
