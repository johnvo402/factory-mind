namespace FactoryMind.Domain.Chat;

public sealed class ChatCitation {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MessageId { get; set; }
    public ChatMessage? Message { get; set; }
    public int ReferenceNumber { get; set; }
    public Guid DocumentId { get; set; }
    public Guid ChunkId { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int PageNumber { get; set; }
    public string Excerpt { get; set; } = string.Empty;
    public double Score { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
