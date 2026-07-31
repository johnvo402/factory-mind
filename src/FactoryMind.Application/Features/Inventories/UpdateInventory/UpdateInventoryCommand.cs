using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Inventories.UpdateInventory;

public sealed record UpdateInventoryCommand(
    Guid InventoryId,
    Guid MaterialId,
    string Warehouse,
    decimal Quantity) : IRequest<Result<InventoryResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
