using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.BusinessData;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Inventories.GetInventoryBalances;

public sealed class GetInventoryBalancesQueryHandler(
    IInventoryRepository repository,
    ICurrentUser currentUser)
    : IRequestHandler<GetInventoryBalancesQuery, Result<IReadOnlyList<InventoryBalanceResponse>>> {
    public async ValueTask<Result<IReadOnlyList<InventoryBalanceResponse>>> Handle(
        GetInventoryBalancesQuery query,
        CancellationToken cancellationToken) {
        var balances = await repository.GetBalancesAsync(
            currentUser.CompanyId,
            query.WarehouseId,
            query.MaterialId,
            BusinessDataNormalization.Search(query.Search),
            cancellationToken);
        return Result<IReadOnlyList<InventoryBalanceResponse>>.Success(
            balances.Select(InventoryBalanceResponse.From).ToList());
    }
}
