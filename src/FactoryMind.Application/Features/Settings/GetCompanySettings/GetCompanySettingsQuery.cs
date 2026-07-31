using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Settings.GetCompanySettings;

public sealed record GetCompanySettingsQuery
    : IRequest<Result<CompanySettingsResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Admin;
}
