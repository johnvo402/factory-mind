namespace FactoryMind.Domain.Chat;

public sealed class ChatMessage {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public Conversation? Conversation { get; set; }
    public string Role { get; set; } = ChatRoles.User;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<ChatCitation> Citations { get; set; } = [];
    public ICollection<ChatBusinessEvidence> BusinessEvidence { get; set; } = [];
}

public static class ChatRoles {
    public const string System = "system";
    public const string User = "user";
    public const string Assistant = "assistant";
}
