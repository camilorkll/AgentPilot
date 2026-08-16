using AgentPilot.Domain.Conversations;

namespace AgentPilot.Application.Feedback;

/// <summary>
/// Una respuesta del asistente que alguien valoró, con el contexto mínimo para
/// entenderla sin abrir la conversación entera: la pregunta que la provocó y la
/// campaña en la que se hizo.
///
/// Es deliberadamente un intercambio suelto y no la conversación completa. Quien
/// revisa quiere saber por qué falló ESA respuesta; el resto del hilo puede contener
/// datos del cliente que no hacen falta para eso, así que se consulta aparte y solo
/// si se pide (ver SECURITY.md).
/// </summary>
public sealed record RatedAnswer(
    Guid MessageId,
    Guid ConversationId,
    Guid? CampaignId,
    string? CampaignName,
    /// <summary>Pregunta del agente que precede a la respuesta. Null si no se encuentra (histórico raro).</summary>
    string? Question,
    string Answer,
    FeedbackRating Rating,
    string? Comment,
    /// <summary>Operador que valoró.</summary>
    string? RatedBy,
    DateTime RatedAtUtc);

/// <summary>Filtros del listado de respuestas valoradas.</summary>
/// <param name="Rating">Solo las valoradas así; null para ambas.</param>
/// <param name="CampaignId">Solo las de esa campaña; null para todas.</param>
/// <param name="Limit">Cuántas devolver como máximo, más recientes primero.</param>
public sealed record RatedAnswerFilter(
    FeedbackRating? Rating = null,
    Guid? CampaignId = null,
    int Limit = 50);
