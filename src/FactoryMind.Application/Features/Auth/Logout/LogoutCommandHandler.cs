namespace FactoryMind.Application.Features.Auth.Logout;

public sealed class LogoutCommandHandler(IAuthRepository repository, ICredentialHasher credentialHasher)
{
    public async Task HandleAsync(LogoutCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken)) return;

        var token = await repository.GetRefreshTokenAsync(credentialHasher.HashToken(command.RefreshToken), cancellationToken);
        if (token is null || token.RevokedAt is not null) return;

        token.RevokedAt = DateTime.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);
    }
}
