using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.ProductionOrders.StartProductionOrder;

public sealed record StartProductionOrderCommand(
    Guid ProductionOrderId,
    IReadOnlyList<ProductionMaterialAllocation> Allocations)
    : IRequest<Result<ProductionOrderResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
