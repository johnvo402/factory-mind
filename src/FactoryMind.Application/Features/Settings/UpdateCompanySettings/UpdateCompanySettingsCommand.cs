using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Settings.UpdateCompanySettings;

public sealed record UpdateCompanySettingsCommand(string Name)
    : IRequest<Result<CompanySettingsResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Admin;
}
