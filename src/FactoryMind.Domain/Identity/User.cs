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
    public string Role { get; set; } = UserRoles.User;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<Conversation> Conversations { get; set; } = [];
    public ICollection<KnowledgeDocument> UploadedDocuments { get; set; } = [];
}

public static class UserRoles {
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string User = "User";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) {
        Admin,
        Manager,
        User
    };
}
