using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Warehouses.CreateWarehouse;

public sealed record CreateWarehouseCommand(string Code, string Name, string? Description)
    : IRequest<Result<WarehouseResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
