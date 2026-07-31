namespace FactoryMind.Domain.Chat;

public sealed class ChatBusinessEvidence {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MessageId { get; set; }
    public ChatMessage? Message { get; set; }
    public int ReferenceNumber { get; set; }
    public Guid EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
