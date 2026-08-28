using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Warehouses.GetWarehouses;

public sealed record GetWarehousesQuery(string? Search)
    : IRequest<Result<IReadOnlyList<WarehouseResponse>>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
