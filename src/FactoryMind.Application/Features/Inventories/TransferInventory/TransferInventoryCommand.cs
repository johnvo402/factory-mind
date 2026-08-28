using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Inventories.TransferInventory;

public sealed record TransferInventoryCommand(
    Guid SourceWarehouseId,
    Guid DestinationWarehouseId,
    Guid MaterialId,
    decimal Quantity,
    string? Note,
    string? ReferenceType) : IRequest<Result<IReadOnlyList<InventoryTransactionResponse>>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
