using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Auth.Refresh;

public sealed class RefreshTokenCommandHandler(
    IAuthRepository repository,
    ICredentialHasher credentialHasher,
    AuthSessionIssuer sessionIssuer) : IRequestHandler<RefreshTokenCommand, Result<AuthSession>> {
    public async ValueTask<Result<AuthSession>> Handle(RefreshTokenCommand command, CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(command.RefreshToken)) {
            return InvalidRefreshToken();
        }

        var token = await repository.GetRefreshTokenWithUserAsync(
            credentialHasher.HashToken(command.RefreshToken), cancellationToken);
        if (token?.User is null || token.RevokedAt is not null || token.ExpiresAt <= DateTime.UtcNow || !token.User.IsActive) {
            return InvalidRefreshToken();
        }

        token.RevokedAt = DateTime.UtcNow;
        return Result<AuthSession>.Success(await sessionIssuer.IssueAsync(token.User, cancellationToken));
    }

    private static Result<AuthSession> InvalidRefreshToken() =>
        Result<AuthSession>.Failure(new Error("auth.invalid_refresh_token", "Refresh token is invalid or expired.", 401));
}
