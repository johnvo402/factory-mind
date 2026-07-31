using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Inventories.CreateInventory;

public sealed record CreateInventoryCommand(Guid MaterialId, string Warehouse, decimal Quantity)
    : IRequest<Result<InventoryResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
