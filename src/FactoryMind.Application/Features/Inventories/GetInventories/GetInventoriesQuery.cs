using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Inventories.GetInventories;

public sealed record GetInventoriesQuery(string? Search)
    : IRequest<Result<IReadOnlyList<InventoryResponse>>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
