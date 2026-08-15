using AgentPilot.Application.Abstractions;
using AgentPilot.Domain.Conversations;
using Microsoft.EntityFrameworkCore;

namespace AgentPilot.Infrastructure.Persistence;

public class ConversationRepository(AgentPilotDbContext db) : IConversationRepository
{
    public async Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default)
        => await db.Conversations.AddAsync(conversation, cancellationToken);

    public Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.Conversations
             .Include(c => c.Messages)
             .AsSplitQuery()
             .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Feedback>> ListFeedbackAsync(
        Guid conversationId, CancellationToken cancellationToken = default)
        => await (from f in db.Feedback
                  join m in db.Set<Message>() on f.MessageId equals m.Id
                  where m.ConversationId == conversationId
                  select f)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);
}
