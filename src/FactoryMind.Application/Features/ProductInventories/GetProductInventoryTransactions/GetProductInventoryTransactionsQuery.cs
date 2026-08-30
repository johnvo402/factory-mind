using FactoryMind.Application.Common.Authorization;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.ProductInventories.GetProductInventoryTransactions;

public sealed record GetProductInventoryTransactionsQuery(
    Guid? WarehouseId,
    Guid? ProductId,
    ProductInventoryTransactionType? TransactionType,
    DateTime? From,
    DateTime? To,
    int Page,
    int PageSize) : IRequest<Result<ProductInventoryTransactionPageResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
