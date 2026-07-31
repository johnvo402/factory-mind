using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Settings.UpdateUser;

public sealed record UpdateUserCommand(
    Guid UserId,
    string Name,
    string Email,
    string Role,
    bool IsActive) : IRequest<Result<UserSettingsResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Admin;
}
