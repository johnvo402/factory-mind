using FactoryMind.Application.Common.Identity;
using FactoryMind.Domain.Identity;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Settings.UpdateUser;

public sealed class UpdateUserCommandHandler(
    ISettingsRepository repository,
    ICurrentUser currentUser) : IRequestHandler<UpdateUserCommand, Result<UserSettingsResponse>> {
    public async ValueTask<Result<UserSettingsResponse>> Handle(
        UpdateUserCommand command,
        CancellationToken cancellationToken) {
        var user = await repository.GetUserAsync(
            command.UserId,
            currentUser.CompanyId,
            cancellationToken);
        if (user is null) {
            return Result<UserSettingsResponse>.Failure(SettingsErrors.UserNotFound);
        }

        var role = command.Role.Trim();
        if (user.Id == currentUser.UserId
            && (!command.IsActive || role != UserRoles.Admin)) {
            return Result<UserSettingsResponse>.Failure(SettingsErrors.SelfAdminChangeForbidden);
        }

        var email = command.Email.Trim().ToLowerInvariant();
        if (await repository.EmailExistsAsync(
            currentUser.CompanyId,
            email,
            user.Id,
            cancellationToken)) {
            return Result<UserSettingsResponse>.Failure(SettingsErrors.EmailAlreadyExists);
        }

        user.Name = command.Name.Trim();
        user.Email = email;
        user.Role = role;
        user.IsActive = command.IsActive;
        await repository.SaveChangesAsync(cancellationToken);
        return Result<UserSettingsResponse>.Success(UserSettingsResponse.From(user));
    }
}
