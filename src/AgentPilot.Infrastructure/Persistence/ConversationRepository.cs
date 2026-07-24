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

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);
}
