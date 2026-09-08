using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using FactoryMind.Domain.Manufacturing;
using Mediator;

namespace FactoryMind.Application.Features.ProductionOrders.UpdateProductionOrder;

public sealed record UpdateProductionOrderCommand(
    Guid ProductionOrderId,
    string Number,
    Guid ProductId,
    decimal Quantity,
    DateTime? DueDate = null,
    string Priority = ProductionOrderPriorities.Normal) : IRequest<Result<ProductionOrderResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
