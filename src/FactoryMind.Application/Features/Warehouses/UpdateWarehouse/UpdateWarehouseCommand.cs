using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Warehouses.UpdateWarehouse;

public sealed record UpdateWarehouseCommand(
    Guid WarehouseId,
    string Code,
    string Name,
    string? Description,
    bool IsActive) : IRequest<Result<WarehouseResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
