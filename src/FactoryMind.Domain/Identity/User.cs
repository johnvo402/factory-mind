using FactoryMind.Domain.Chat;
using FactoryMind.Domain.Knowledge;

namespace FactoryMind.Domain.Identity;

public sealed class User {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<Conversation> Conversations { get; set; } = [];
    public ICollection<KnowledgeDocument> UploadedDocuments { get; set; } = [];
}
