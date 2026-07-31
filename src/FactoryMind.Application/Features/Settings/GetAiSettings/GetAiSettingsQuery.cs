using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Settings.GetAiSettings;

public sealed record GetAiSettingsQuery
    : IRequest<Result<AiSettingsResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Admin;
}
