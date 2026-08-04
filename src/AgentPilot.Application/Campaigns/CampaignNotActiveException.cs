namespace AgentPilot.Application.Campaigns;

/// <summary>
/// Se preguntó sobre una campaña que no está activa. La API la traduce a 409 con el
/// código <c>campaign_not_active</c>.
///
/// Se comprueba en cada pregunta y no solo al abrir el selector: una campaña
/// desactivada a media sesión debe dejar de responder, sin esperar a que el agente
/// recargue la página.
/// </summary>
public class CampaignNotActiveException(Guid campaignId, string campaignName)
    : InvalidOperationException(
        $"La campaña '{campaignName}' no está activa: no puede responder consultas.")
{
    public Guid CampaignId { get; } = campaignId;
    public string CampaignName { get; } = campaignName;
}
