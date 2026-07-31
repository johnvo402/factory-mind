using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Auth.Logout;

public sealed class LogoutCommandHandler(
    IAuthRepository repository,
    ICredentialHasher credentialHasher) : IRequestHandler<LogoutCommand, Result> {
    public async ValueTask<Result> Handle(LogoutCommand command, CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(command.RefreshToken)) {
            return Result.Success();
        }

        var token = await repository.GetRefreshTokenAsync(credentialHasher.HashToken(command.RefreshToken), cancellationToken);
        if (token is null || token.RevokedAt is not null) {
            return Result.Success();
        }

        token.RevokedAt = DateTime.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
