using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.ProductionOrders.CancelProductionOrder;

public sealed record CancelProductionOrderCommand(Guid ProductionOrderId)
    : IRequest<Result<ProductionOrderResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
