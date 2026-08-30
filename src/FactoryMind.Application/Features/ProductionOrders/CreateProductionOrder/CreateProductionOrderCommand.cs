using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.ProductionOrders.CreateProductionOrder;

public sealed record CreateProductionOrderCommand(
    string Number,
    Guid ProductId,
    decimal Quantity) : IRequest<Result<ProductionOrderResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
