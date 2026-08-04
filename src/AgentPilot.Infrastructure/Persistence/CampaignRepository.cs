using AgentPilot.Application.Abstractions;
using AgentPilot.Domain.Campaigns;
using Microsoft.EntityFrameworkCore;

namespace AgentPilot.Infrastructure.Persistence;

public class CampaignRepository(AgentPilotDbContext db) : ICampaignRepository
{
    public Task<Campaña?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.Campañas.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
}
