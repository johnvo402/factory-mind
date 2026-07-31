using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.ProductionOrders.GetProductionOrders;

public sealed record GetProductionOrdersQuery(string? Search)
    : IRequest<Result<IReadOnlyList<ProductionOrderResponse>>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
