namespace FactoryMind.Application.Features.Auth.Refresh;

public sealed class RefreshTokenCommandHandler(IAuthRepository repository, ICredentialHasher credentialHasher, AuthSessionIssuer sessionIssuer)
{
    public async Task<AuthSession?> HandleAsync(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken)) return null;

        var token = await repository.GetRefreshTokenWithUserAsync(
            credentialHasher.HashToken(command.RefreshToken), cancellationToken);
        if (token?.User is null || token.RevokedAt is not null || token.ExpiresAt <= DateTime.UtcNow || !token.User.IsActive) return null;

        token.RevokedAt = DateTime.UtcNow;
        return await sessionIssuer.IssueAsync(token.User, cancellationToken);
    }
}
