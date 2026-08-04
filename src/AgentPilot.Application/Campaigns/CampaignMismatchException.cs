namespace AgentPilot.Application.Campaigns;

/// <summary>
/// Se intentó continuar una conversación con una campaña distinta de la suya. La API
/// la traduce a 409 con el código <c>campaign_mismatch</c>.
///
/// No es una restricción cosmética: el historial de la conversación se reenvía al
/// modelo en cada turno, así que responder con otra campaña metería en el contexto
/// contenido de la campaña anterior. Cambiar de campaña exige empezar otra
/// conversación, y eso hay que decirlo, no resolverlo por nuestra cuenta.
/// </summary>
public class CampaignMismatchException(Guid conversationId, Guid? conversationCampaignId, Guid requestedCampaignId)
    : InvalidOperationException(
        conversationCampaignId is null
            ? $"La conversación {conversationId} es anterior a las campañas y no se puede continuar. " +
              "Empieza una conversación nueva."
            : $"La conversación {conversationId} pertenece a otra campaña. " +
              "Para cambiar de campaña hay que empezar una conversación nueva.")
{
    public Guid ConversationId { get; } = conversationId;
    public Guid? ConversationCampaignId { get; } = conversationCampaignId;
    public Guid RequestedCampaignId { get; } = requestedCampaignId;
}
