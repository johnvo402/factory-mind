using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.ProductionOrders.DeleteProductionOrder;

public sealed record DeleteProductionOrderCommand(Guid ProductionOrderId)
    : IRequest<Result>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
