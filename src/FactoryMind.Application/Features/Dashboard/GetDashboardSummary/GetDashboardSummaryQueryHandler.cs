using FactoryMind.Application.Common.Identity;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Dashboard.GetDashboardSummary;

public sealed class GetDashboardSummaryQueryHandler(
    IDashboardRepository repository,
    ICurrentUser currentUser) : IRequestHandler<GetDashboardSummaryQuery, Result<DashboardSummary>> {
    public async ValueTask<Result<DashboardSummary>> Handle(
        GetDashboardSummaryQuery query,
        CancellationToken cancellationToken) {
        var summary = await repository.GetSummaryAsync(currentUser.CompanyId, cancellationToken);
        return Result<DashboardSummary>.Success(summary);
    }
}
