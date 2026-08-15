// Tipo del dominio cualificado con alias: 'Feedback' a secas colisionaría con
// el espacio de nombres AgentPilot.Application.Feedback.
using FeedbackEntity = AgentPilot.Domain.Conversations.Feedback;

namespace AgentPilot.Application.Abstractions;

public interface IFeedbackRepository
{
    /// <summary>¿Existe el mensaje al que se quiere asociar el feedback?</summary>
    Task<bool> MessageExistsAsync(Guid messageId, CancellationToken cancellationToken = default);

    /// <summary>Valoración vigente de un mensaje, o null si todavía no se ha valorado.</summary>
    Task<FeedbackEntity?> GetByMessageAsync(Guid messageId, CancellationToken cancellationToken = default);

    Task AddAsync(FeedbackEntity feedback, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
