using AgentPilot.Application.Abstractions;
using AgentPilot.Application.Feedback;
using AgentPilot.Domain.Conversations;
using Microsoft.EntityFrameworkCore;

namespace AgentPilot.Infrastructure.Persistence;

public class FeedbackRepository(AgentPilotDbContext db) : IFeedbackRepository
{
    public Task<bool> MessageExistsAsync(Guid messageId, CancellationToken cancellationToken = default)
        => db.Set<Message>().AnyAsync(m => m.Id == messageId, cancellationToken);

    public Task<Feedback?> GetByMessageAsync(Guid messageId, CancellationToken cancellationToken = default)
        => db.Feedback.FirstOrDefaultAsync(f => f.MessageId == messageId, cancellationToken);

    public async Task AddAsync(Feedback feedback, CancellationToken cancellationToken = default)
        => await db.Feedback.AddAsync(feedback, cancellationToken);

    public async Task<IReadOnlyList<RatedAnswer>> ListRatedAnswersAsync(
        RatedAnswerFilter filter, CancellationToken cancellationToken = default)
    {
        var query = from f in db.Feedback
                    join m in db.Set<Message>() on f.MessageId equals m.Id
                    join c in db.Conversations on m.ConversationId equals c.Id
                    select new { f, m, c };

        if (filter.Rating is FeedbackRating rating)
            query = query.Where(x => x.f.Rating == rating);

        if (filter.CampaignId is Guid campaignId)
            query = query.Where(x => x.c.CampaignId == campaignId);

        return await query
            .OrderByDescending(x => x.f.CreatedAtUtc)
            .Take(filter.Limit)
            .Select(x => new RatedAnswer(
                x.f.MessageId,
                x.m.ConversationId,
                x.c.CampaignId,
                // El nombre de la campaña se resuelve aquí y no se guarda copiado: si la
                // campaña se borró, la conversación conserva el hilo con CampaignId a null
                // y la revisión sigue teniendo sentido sin nombre.
                db.Campañas.Where(k => k.Id == x.c.CampaignId).Select(k => k.Name).FirstOrDefault(),
                // La pregunta que provocó la respuesta: el último mensaje del agente
                // anterior a ella dentro del mismo hilo.
                db.Set<Message>()
                    .Where(q => q.ConversationId == x.m.ConversationId
                             && q.Role == MessageRole.User
                             && q.CreatedAtUtc < x.m.CreatedAtUtc)
                    .OrderByDescending(q => q.CreatedAtUtc)
                    .Select(q => q.Content)
                    .FirstOrDefault(),
                x.m.Content,
                x.f.Rating,
                x.f.Comment,
                x.f.CreatedBy,
                x.f.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);
}
