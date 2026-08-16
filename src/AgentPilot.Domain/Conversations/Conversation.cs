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

    /// <summary>
    /// Operador que mantuvo la conversación. Se fija al crearla y no cambia: es quien
    /// preguntó, no quien la consulte después.
    ///
    /// Anulable por dos motivos distintos: las conversaciones anteriores a que se
    /// registrara el operador (rellenadas desde llm_call_logs donde se pudo), y las
    /// acciones sin usuario asociado. No se hace obligatorio en la base de datos para
    /// no inventar un valor en el histórico que no consta.
    /// </summary>
    public string? UserName { get; private set; }

    public string? Title { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<Message> Messages => _messages.AsReadOnly();

    private Conversation() { } // EF

    /// <param name="userName">
    /// Operador que pregunta. Es un parámetro explícito y sin valor por defecto a
    /// propósito: quien crea una conversación tiene que decidir qué identidad registra,
    /// aunque la decisión sea "ninguna". Un valor por defecto convertiría un olvido en
    /// una conversación anónima que luego no se puede atribuir.
    /// </param>
    public Conversation(Guid campaignId, string? userName, string? title = null)
    {
        if (campaignId == Guid.Empty)
            throw new ArgumentException(
                "La conversación debe pertenecer a una campaña.", nameof(campaignId));

        Id = Guid.NewGuid();
        CampaignId = campaignId;
        UserName = string.IsNullOrWhiteSpace(userName) ? null : userName.Trim();
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
