using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.ProductionOrders.CompleteProductionOrder;

public sealed record CompleteProductionOrderCommand(
    Guid ProductionOrderId,
    Guid WarehouseId) : IRequest<Result<ProductionOrderResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
