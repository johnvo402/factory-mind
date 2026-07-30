using FactoryMind.Domain.Identity;

namespace FactoryMind.Application.Features.Auth;

public sealed class AuthSessionIssuer(IAuthRepository repository, ICredentialHasher credentialHasher, ITokenService tokenService)
{
    public async Task<AuthSession> IssueAsync(User user, CancellationToken cancellationToken)
    {
        var refreshToken = tokenService.CreateRefreshToken();
        var expiresAt = tokenService.GetRefreshTokenExpiry();

        repository.AddRefreshToken(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = credentialHasher.HashToken(refreshToken),
            ExpiresAt = expiresAt
        });
        await repository.SaveChangesAsync(cancellationToken);

        return new AuthSession(
            tokenService.CreateAccessToken(user),
            refreshToken,
            expiresAt,
            new UserProfile(user.Id, user.Name, user.Email, user.Role, user.CompanyId));
    }
}
