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
    public string? Title { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<Message> Messages => _messages.AsReadOnly();

    private Conversation() { } // EF

    public Conversation(string? title = null)
    {
        Id = Guid.NewGuid();
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
