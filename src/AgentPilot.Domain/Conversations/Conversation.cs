namespace AgentPilot.Domain.Conversations;

/// <summary>
/// Raíz del agregado de conversación: una secuencia de mensajes usuario/asistente.
/// Los mensajes se añaden a través de métodos de intención, no manipulando la
/// lista desde fuera.
/// </summary>
public class Conversation
{
    private readonly List<Message> _messages = [];

    public Guid Id { get; private set; }

    /// <summary>
    /// Campaña sobre la que se conversa. Se fija al crear la conversación y no cambia
    /// nunca: el historial se reenvía al modelo en cada turno, así que cambiarla
    /// arrastraría contenido de la campaña anterior al contexto de la nueva.
    ///
    /// Es anulable solo por las conversaciones anteriores a la existencia de campañas,
    /// que por eso mismo no se pueden continuar.
    /// </summary>
    public Guid? CampaignId { get; private set; }

    public string? Title { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<Message> Messages => _messages.AsReadOnly();

    private Conversation() { } // EF

    public Conversation(Guid campaignId, string? title = null)
    {
        if (campaignId == Guid.Empty)
            throw new ArgumentException(
                "La conversación debe pertenecer a una campaña.", nameof(campaignId));

        Id = Guid.NewGuid();
        CampaignId = campaignId;
        CreatedAtUtc = DateTime.UtcNow;
        Title = title;
    }

    public Message AddUserMessage(string content)
    {
        var message = new Message(MessageRole.User, content);
        _messages.Add(message);

        // El título se deriva de la primera pregunta si no se fijó uno.
        if (string.IsNullOrWhiteSpace(Title))
            Title = content.Length <= 80 ? content : content[..80] + "…";

        return message;
    }

    public Message AddAssistantMessage(string content, IEnumerable<Citation> citations)
    {
        var message = new Message(MessageRole.Assistant, content, citations);
        _messages.Add(message);
        return message;
    }
}
