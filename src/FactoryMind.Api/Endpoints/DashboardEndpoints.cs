using FactoryMind.Application.Common.Authorization;
using FactoryMind.Application.Features.Dashboard.GetDashboardSummary;
using FactoryMind.Api.Routing;
using Mediator;

namespace FactoryMind.Api.Endpoints;

public static class DashboardEndpoints {
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup(ApiRoutes.Dashboard.Group)
            .RequireAuthorization(AuthorizationPolicies.Authenticated);

        group.MapGet(ApiRoutes.Dashboard.Summary, async (
            ISender sender,
            CancellationToken cancellationToken) => {
                return (await sender.Send(
                    new GetDashboardSummaryQuery(),
                    cancellationToken)).ToHttpResult();
            });

        return endpoints;
    }
}
