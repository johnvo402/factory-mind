using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Auth.Login;

public sealed class LoginCommandHandler(
    IAuthRepository repository,
    ICredentialHasher credentialHasher,
    AuthSessionIssuer sessionIssuer) : IRequestHandler<LoginCommand, Result<AuthSession>> {
    public async ValueTask<Result<AuthSession>> Handle(LoginCommand command, CancellationToken cancellationToken) {
        var email = command.Email.Trim().ToLowerInvariant();
        var user = await repository.GetActiveUserByEmailAsync(email, cancellationToken);

        if (user is null || !credentialHasher.VerifyPassword(command.Password, user.PasswordHash)) {
            return Result<AuthSession>.Failure(new Error("auth.invalid_credentials", "Email or password is incorrect.", 401));
        }

        return Result<AuthSession>.Success(await sessionIssuer.IssueAsync(user, cancellationToken));
    }
}
