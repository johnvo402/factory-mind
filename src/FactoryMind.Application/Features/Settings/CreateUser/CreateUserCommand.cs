using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Settings.CreateUser;

public sealed record CreateUserCommand(
    string Name,
    string Email,
    string Password,
    string Role) : IRequest<Result<UserSettingsResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Admin;
}
