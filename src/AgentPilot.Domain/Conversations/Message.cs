namespace AgentPilot.Domain.Conversations;

/// <summary>Un mensaje de la conversación. Los del asistente pueden llevar citas.</summary>
public class Message
{
    private readonly List<Citation> _citations = [];

    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public MessageRole Role { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<Citation> Citations => _citations.AsReadOnly();

    private Message() { } // EF

    public Message(MessageRole role, string content, IEnumerable<Citation>? citations = null)
    {
        Id = Guid.NewGuid();
        Role = role;
        Content = content;
        CreatedAtUtc = DateTime.UtcNow;
        if (citations is not null)
            _citations.AddRange(citations);
    }
}
