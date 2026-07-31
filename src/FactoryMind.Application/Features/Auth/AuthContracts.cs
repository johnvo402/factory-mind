using FactoryMind.Domain.Identity;

namespace FactoryMind.Application.Features.Auth;

public sealed record AuthSession(string AccessToken, string RefreshToken, DateTime RefreshTokenExpiresAt, UserProfile User);
public sealed record UserProfile(Guid Id, string Name, string Email, string Role, Guid CompanyId);

public interface IAuthRepository {
    Task<User?> GetActiveUserByEmailAsync(string email, CancellationToken cancellationToken);
    Task<RefreshToken?> GetRefreshTokenWithUserAsync(string tokenHash, CancellationToken cancellationToken);
    Task<RefreshToken?> GetRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken);
    void AddRefreshToken(RefreshToken refreshToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface ICredentialHasher {
    bool VerifyPassword(string password, string passwordHash);
    string HashPassword(string password);
    string HashToken(string token);
}

public interface ITokenService {
    string CreateAccessToken(User user);
    string CreateRefreshToken();
    DateTime GetRefreshTokenExpiry();
}
