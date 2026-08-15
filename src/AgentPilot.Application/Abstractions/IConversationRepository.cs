using AgentPilot.Domain.Conversations;
// 'Feedback' a secas colisionaría con el espacio de nombres AgentPilot.Application.Feedback
// (mismo motivo que en IFeedbackRepository).
using FeedbackEntity = AgentPilot.Domain.Conversations.Feedback;

namespace AgentPilot.Application.Abstractions;

/// <summary>Persistencia de conversaciones. Implementado con EF Core en Infrastructure.</summary>
public interface IConversationRepository
{
    Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default);

    Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Valoraciones de los mensajes de una conversación. Va aparte de
    /// <see cref="GetByIdAsync"/> a propósito: esa se llama en CADA pregunta del chat
    /// para recomponer el historial, y ahí las valoraciones no pintan nada; solo hacen
    /// falta al mostrar una conversación ya cerrada.
    /// </summary>
    Task<IReadOnlyList<FeedbackEntity>> ListFeedbackAsync(
        Guid conversationId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
