using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Dashboard.GetDashboardSummary;

public sealed record GetDashboardSummaryQuery
    : IRequest<Result<DashboardSummary>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Authenticated;
}
