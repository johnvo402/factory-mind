using FactoryMind.Application.Common.Identity;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Inventories.GetInventoryTransactions;

public sealed class GetInventoryTransactionsQueryHandler(
    IInventoryRepository repository,
    ICurrentUser currentUser)
    : IRequestHandler<GetInventoryTransactionsQuery, Result<InventoryTransactionPageResponse>> {
    public async ValueTask<Result<InventoryTransactionPageResponse>> Handle(
        GetInventoryTransactionsQuery query,
        CancellationToken cancellationToken) {
        var (items, totalCount) = await repository.GetTransactionsAsync(
            currentUser.CompanyId,
            query.WarehouseId,
            query.MaterialId,
            query.TransactionType,
            query.From,
            query.To,
            query.Page,
            query.PageSize,
            cancellationToken);
        return Result<InventoryTransactionPageResponse>.Success(new(
            items.Select(InventoryTransactionResponse.From).ToList(),
            query.Page,
            query.PageSize,
            totalCount));
    }
}
