using FactoryMind.Application.Common.Authorization;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Inventories.GetInventoryTransactions;

public sealed record GetInventoryTransactionsQuery(
    Guid? WarehouseId,
    Guid? MaterialId,
    InventoryTransactionType? TransactionType,
    DateTime? From,
    DateTime? To,
    int Page,
    int PageSize) : IRequest<Result<InventoryTransactionPageResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
