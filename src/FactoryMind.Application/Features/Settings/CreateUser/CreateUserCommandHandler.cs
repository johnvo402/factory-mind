using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.Auth;
using FactoryMind.Domain.Identity;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Settings.CreateUser;

public sealed class CreateUserCommandHandler(
    ISettingsRepository repository,
    ICredentialHasher credentialHasher,
    ICurrentUser currentUser) : IRequestHandler<CreateUserCommand, Result<UserSettingsResponse>> {
    public async ValueTask<Result<UserSettingsResponse>> Handle(
        CreateUserCommand command,
        CancellationToken cancellationToken) {
        var email = command.Email.Trim().ToLowerInvariant();
        if (await repository.EmailExistsAsync(
            currentUser.CompanyId,
            email,
            null,
            cancellationToken)) {
            return Result<UserSettingsResponse>.Failure(SettingsErrors.EmailAlreadyExists);
        }

        var user = new User {
            CompanyId = currentUser.CompanyId,
            Name = command.Name.Trim(),
            Email = email,
            PasswordHash = credentialHasher.HashPassword(command.Password),
            Role = command.Role.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        repository.Add(user);
        await repository.SaveChangesAsync(cancellationToken);
        return Result<UserSettingsResponse>.Success(UserSettingsResponse.From(user));
    }
}
