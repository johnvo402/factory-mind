using FactoryMind.Application.Common.Identity;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.ProductInventories.GetProductInventoryTransactions;

public sealed class GetProductInventoryTransactionsQueryHandler(
    IProductInventoryRepository repository,
    ICurrentUser currentUser)
    : IRequestHandler<GetProductInventoryTransactionsQuery, Result<ProductInventoryTransactionPageResponse>> {
    public async ValueTask<Result<ProductInventoryTransactionPageResponse>> Handle(
        GetProductInventoryTransactionsQuery query,
        CancellationToken cancellationToken) {
        var (items, totalCount) = await repository.GetTransactionsAsync(
            currentUser.CompanyId,
            query.WarehouseId,
            query.ProductId,
            query.TransactionType,
            query.From,
            query.To,
            query.Page,
            query.PageSize,
            cancellationToken);
        return Result<ProductInventoryTransactionPageResponse>.Success(new(
            items.Select(ProductInventoryTransactionResponse.From).ToList(),
            query.Page,
            query.PageSize,
            totalCount));
    }
}
