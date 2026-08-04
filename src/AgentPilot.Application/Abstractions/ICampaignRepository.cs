using AgentPilot.Domain.Campaigns;

namespace AgentPilot.Application.Abstractions;

/// <summary>Persistencia de campañas. Implementado con EF Core en Infrastructure.</summary>
public interface ICampaignRepository
{
    Task<Campaña?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
