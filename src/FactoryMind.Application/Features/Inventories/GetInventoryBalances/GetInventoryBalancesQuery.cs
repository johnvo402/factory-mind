using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Inventories.GetInventoryBalances;

public sealed record GetInventoryBalancesQuery(
    Guid? WarehouseId,
    Guid? MaterialId,
    string? Search) : IRequest<Result<IReadOnlyList<InventoryBalanceResponse>>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
