using FactoryMind.Domain.Identity;
using FactoryMind.Shared.Contracts;

namespace FactoryMind.Application.Features.Settings;

public static class SettingsConstraints {
    public const int MaximumCompanyNameLength = 200;
    public const int MaximumUserNameLength = 200;
    public const int MaximumEmailLength = 320;
    public const int MinimumPasswordLength = 8;
}

public sealed record CompanySettingsResponse(Guid Id, string Name, DateTime CreatedAt) {
    public static CompanySettingsResponse From(Company company) =>
        new(company.Id, company.Name, company.CreatedAt);
}

public sealed record UserSettingsResponse(
    Guid Id,
    string Name,
    string Email,
    string Role,
    bool IsActive,
    DateTime CreatedAt) {
    public static UserSettingsResponse From(User user) => new(
        user.Id,
        user.Name,
        user.Email,
        user.Role,
        user.IsActive,
        user.CreatedAt);
}

public sealed record AiSettingsResponse(
    string Provider,
    string ChatModel,
    string EmbeddingModel,
    int EmbeddingDimensions,
    int MaximumOutputTokens,
    bool ApiKeyConfigured);

public interface ISettingsRepository {
    Task<Company?> GetCompanyAsync(Guid companyId, CancellationToken cancellationToken);
    Task<IReadOnlyList<User>> GetUsersAsync(Guid companyId, CancellationToken cancellationToken);
    Task<User?> GetUserAsync(Guid userId, Guid companyId, CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(
        Guid companyId,
        string email,
        Guid? excludedUserId,
        CancellationToken cancellationToken);
    void Add(User user);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IAiSettingsReader {
    AiSettingsResponse Get();
}

public static class SettingsErrors {
    public static readonly Error CompanyNotFound = new(
        "settings.company_not_found",
        "Company was not found.",
        404);
    public static readonly Error UserNotFound = new(
        "settings.user_not_found",
        "User was not found.",
        404);
    public static readonly Error EmailAlreadyExists = new(
        "settings.email_already_exists",
        "A user with this email already exists.",
        409);
    public static readonly Error SelfAdminChangeForbidden = new(
        "settings.self_admin_change_forbidden",
        "You cannot deactivate or demote your own Admin account.",
        409);
}
