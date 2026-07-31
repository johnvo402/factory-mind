using FactoryMind.Application.Common.Identity;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Settings.GetUsers;

public sealed class GetUsersQueryHandler(
    ISettingsRepository repository,
    ICurrentUser currentUser) : IRequestHandler<GetUsersQuery, Result<IReadOnlyList<UserSettingsResponse>>> {
    public async ValueTask<Result<IReadOnlyList<UserSettingsResponse>>> Handle(
        GetUsersQuery query,
        CancellationToken cancellationToken) {
        var users = await repository.GetUsersAsync(currentUser.CompanyId, cancellationToken);
        return Result<IReadOnlyList<UserSettingsResponse>>.Success(
            users.Select(UserSettingsResponse.From).ToList());
    }
}
