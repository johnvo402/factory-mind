using FactoryMind.Domain.Identity;

namespace FactoryMind.Domain.Chat;

public sealed class Conversation {
    public const string DefaultTitle = "New conversation";

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string Title { get; set; } = DefaultTitle;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<ChatMessage> Messages { get; set; } = [];
}
