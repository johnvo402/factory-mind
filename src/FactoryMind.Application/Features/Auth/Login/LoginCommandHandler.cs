namespace FactoryMind.Application.Features.Auth.Login;

public sealed class LoginCommandHandler(IAuthRepository repository, ICredentialHasher credentialHasher, AuthSessionIssuer sessionIssuer)
{
    public async Task<AuthSession?> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var email = command.Email.Trim().ToLowerInvariant();
        var user = await repository.GetActiveUserByEmailAsync(email, cancellationToken);

        return user is null || !credentialHasher.VerifyPassword(command.Password, user.PasswordHash)
            ? null
            : await sessionIssuer.IssueAsync(user, cancellationToken);
    }
}
