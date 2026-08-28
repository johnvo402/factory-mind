using FactoryMind.Application.Common.Authorization;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Inventories.AdjustInventory;

public sealed record AdjustInventoryCommand(
    Guid WarehouseId,
    Guid MaterialId,
    InventoryAdjustmentDirection Direction,
    decimal Quantity,
    string Note,
    string? ReferenceType,
    Guid? ReferenceId) : IRequest<Result<InventoryTransactionResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
