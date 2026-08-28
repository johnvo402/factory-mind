using FactoryMind.Api.Routing;
using FactoryMind.Application.Common.Authorization;
using FactoryMind.Application.Features.Warehouses.CreateWarehouse;
using FactoryMind.Application.Features.Warehouses.DeactivateWarehouse;
using FactoryMind.Application.Features.Warehouses.GetWarehouse;
using FactoryMind.Application.Features.Warehouses.GetWarehouses;
using FactoryMind.Application.Features.Warehouses.UpdateWarehouse;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace FactoryMind.Api.Endpoints;

public static class WarehouseEndpoints {
    public static IEndpointRouteBuilder MapWarehouseEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup(ApiRoutes.Warehouses.Group)
            .RequireAuthorization(AuthorizationPolicies.Manager);

        group.MapGet(ApiRoutes.Warehouses.Root, async (
            [AsParameters] BusinessDataSearchRequest request,
            ISender sender,
            CancellationToken cancellationToken) => (await sender.Send(
                new GetWarehousesQuery(request.Search), cancellationToken)).ToHttpResult())
            .WithRequestValidation<BusinessDataSearchRequest>();

        group.MapGet(ApiRoutes.Warehouses.ById, async (
            Guid warehouseId,
            ISender sender,
            CancellationToken cancellationToken) => (await sender.Send(
                new GetWarehouseQuery(warehouseId), cancellationToken)).ToHttpResult());

        group.MapPost(ApiRoutes.Warehouses.Root, async (
            [FromBody] WarehouseCreateRequest request,
            ISender sender,
            CancellationToken cancellationToken) => (await sender.Send(
                new CreateWarehouseCommand(request.Code, request.Name, request.Description),
                cancellationToken)).ToHttpResult())
            .WithRequestValidation<WarehouseCreateRequest>();

        group.MapPut(ApiRoutes.Warehouses.ById, async (
            Guid warehouseId,
            [FromBody] WarehouseUpdateRequest request,
            ISender sender,
            CancellationToken cancellationToken) => (await sender.Send(
                new UpdateWarehouseCommand(
                    warehouseId,
                    request.Code,
                    request.Name,
                    request.Description,
                    request.IsActive),
                cancellationToken)).ToHttpResult())
            .WithRequestValidation<WarehouseUpdateRequest>();

        group.MapDelete(ApiRoutes.Warehouses.ById, async (
            Guid warehouseId,
            ISender sender,
            CancellationToken cancellationToken) => (await sender.Send(
                new DeactivateWarehouseCommand(warehouseId), cancellationToken)).ToHttpResult());

        return endpoints;
    }
}
