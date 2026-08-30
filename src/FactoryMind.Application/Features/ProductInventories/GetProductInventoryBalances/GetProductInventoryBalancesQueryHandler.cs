using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.BusinessData;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.ProductInventories.GetProductInventoryBalances;

public sealed class GetProductInventoryBalancesQueryHandler(
    IProductInventoryRepository repository,
    ICurrentUser currentUser)
    : IRequestHandler<GetProductInventoryBalancesQuery, Result<IReadOnlyList<ProductInventoryBalanceResponse>>> {
    public async ValueTask<Result<IReadOnlyList<ProductInventoryBalanceResponse>>> Handle(
        GetProductInventoryBalancesQuery query,
        CancellationToken cancellationToken) {
        var balances = await repository.GetBalancesAsync(
            currentUser.CompanyId,
            query.WarehouseId,
            query.ProductId,
            BusinessDataNormalization.Search(query.Search),
            cancellationToken);
        return Result<IReadOnlyList<ProductInventoryBalanceResponse>>.Success(
            balances.Select(ProductInventoryBalanceResponse.From).ToList());
    }
}
