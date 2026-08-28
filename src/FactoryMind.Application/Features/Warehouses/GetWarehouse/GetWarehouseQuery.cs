using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Warehouses.GetWarehouse;

public sealed record GetWarehouseQuery(Guid WarehouseId)
    : IRequest<Result<WarehouseResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
