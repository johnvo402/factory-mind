using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.ProductionOrders.UpdateProductionOrder;

public sealed record UpdateProductionOrderCommand(
    Guid ProductionOrderId,
    string Number,
    Guid ProductId,
    decimal Quantity,
    string Status) : IRequest<Result<ProductionOrderResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
