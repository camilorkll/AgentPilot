using AgentPilot.Domain.Conversations;

namespace AgentPilot.Application.Abstractions;

/// <summary>Persistencia de conversaciones. Implementado con EF Core en Infrastructure.</summary>
public interface IConversationRepository
{
    Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default);

    Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
