using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Inventories.DeleteInventory;

public sealed record DeleteInventoryCommand(Guid InventoryId)
    : IRequest<Result>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
