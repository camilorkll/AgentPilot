using AgentPilot.Domain.Campaigns;

namespace AgentPilot.Application.Campaigns;

/// <summary>
/// Campaña con el volumen de su corpus. Se proyecta en la consulta en lugar de cargar
/// los documentos: la lista de campañas solo necesita contarlos, y traerlos para
/// contarlos sería traer también sus fragmentos.
/// </summary>
/// <param name="Campaign">La campaña.</param>
/// <param name="DocumentCount">Documentos, en cualquier estado de ingesta.</param>
/// <param name="ActiveDocumentCount">
/// Documentos indexados y activos, es decir los que el asistente puede citar. A cero, la
/// campaña solo puede abstenerse, y conviene que se vea antes de preguntar.
/// </param>
public sealed record CampaignWithCounts(Campaña Campaign, int DocumentCount, int ActiveDocumentCount);
