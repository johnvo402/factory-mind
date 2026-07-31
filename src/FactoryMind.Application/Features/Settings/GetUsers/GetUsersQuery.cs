using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Settings.GetUsers;

public sealed record GetUsersQuery
    : IRequest<Result<IReadOnlyList<UserSettingsResponse>>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Admin;
}
