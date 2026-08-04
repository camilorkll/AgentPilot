namespace AgentPilot.Application.Campaigns;

/// <summary>
/// Se intentó modificar la documentación de una campaña cerrada. La API la traduce
/// a 409 con el código <c>campaign_closed</c>.
/// </summary>
public class CampaignClosedException(Guid campaignId, string campaignName)
    : InvalidOperationException(
        $"La campaña '{campaignName}' está cerrada: su documentación es de solo lectura.")
{
    public Guid CampaignId { get; } = campaignId;
    public string CampaignName { get; } = campaignName;
}
