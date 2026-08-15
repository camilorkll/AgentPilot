using AgentPilot.Application.Abstractions;
using AgentPilot.Domain.Conversations;

namespace AgentPilot.Application.Feedback;

public interface IFeedbackService
{
    /// <summary>
    /// Registra la valoración de un mensaje, o rectifica la que ya tuviera: un mensaje
    /// tiene como mucho una. Lanza KeyNotFoundException si el mensaje no existe.
    /// </summary>
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

        // Upsert, no alta siempre: una segunda valoración del mismo mensaje es una
        // rectificación, no una valoración más. Insertarla contaría esa respuesta dos
        // veces en el porcentaje de respuestas útiles.
        var existente = await repository.GetByMessageAsync(messageId, cancellationToken);
        if (existente is not null)
            existente.Actualizar(rating, comment, createdBy);
        else
            await repository.AddAsync(
                new Domain.Conversations.Feedback(messageId, rating, comment, createdBy), cancellationToken);

        await repository.SaveChangesAsync(cancellationToken);
    }
}
