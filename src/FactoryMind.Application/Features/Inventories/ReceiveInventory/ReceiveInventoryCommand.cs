using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Inventories.ReceiveInventory;

public sealed record ReceiveInventoryCommand(
    Guid WarehouseId,
    Guid MaterialId,
    decimal Quantity,
    string? Note,
    string? ReferenceType,
    Guid? ReferenceId) : IRequest<Result<InventoryTransactionResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
