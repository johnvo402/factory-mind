using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.ProductInventories.GetProductInventoryBalances;

public sealed record GetProductInventoryBalancesQuery(
    Guid? WarehouseId,
    Guid? ProductId,
    string? Search) : IRequest<Result<IReadOnlyList<ProductInventoryBalanceResponse>>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
