using FactoryMind.Api.Routing;
using FactoryMind.Application.Common.Authorization;
using FactoryMind.Application.Features.Inventories.CreateInventory;
using FactoryMind.Application.Features.Inventories.DeleteInventory;
using FactoryMind.Application.Features.Inventories.GetInventories;
using FactoryMind.Application.Features.Inventories.UpdateInventory;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace FactoryMind.Api.Endpoints;

public static class InventoryEndpoints {
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup(ApiRoutes.Inventories.Group)
            .RequireAuthorization(AuthorizationPolicies.Manager);

        group.MapGet(ApiRoutes.Inventories.Root, async (
            [AsParameters] BusinessDataSearchRequest request,
            ISender sender,
            CancellationToken cancellationToken) => {
                return (await sender.Send(
                    new GetInventoriesQuery(request.Search),
                    cancellationToken)).ToHttpResult();
            })
            .WithRequestValidation<BusinessDataSearchRequest>();

        group.MapPost(ApiRoutes.Inventories.Root, async (
            [FromBody] InventoryRequest request,
            ISender sender,
            CancellationToken cancellationToken) => {
                return (await sender.Send(
                    new CreateInventoryCommand(request.MaterialId, request.Warehouse, request.Quantity),
                    cancellationToken)).ToHttpResult();
            })
            .WithRequestValidation<InventoryRequest>();

        group.MapPut(ApiRoutes.Inventories.ById, async (
            Guid inventoryId,
            [FromBody] InventoryRequest request,
            ISender sender,
            CancellationToken cancellationToken) => {
                return (await sender.Send(
                    new UpdateInventoryCommand(
                        inventoryId,
                        request.MaterialId,
                        request.Warehouse,
                        request.Quantity),
                    cancellationToken)).ToHttpResult();
            })
            .WithRequestValidation<InventoryRequest>();

        group.MapDelete(ApiRoutes.Inventories.ById, async (
            Guid inventoryId,
            ISender sender,
            CancellationToken cancellationToken) => {
                return (await sender.Send(
                    new DeleteInventoryCommand(inventoryId),
                    cancellationToken)).ToHttpResult();
            });

        return endpoints;
    }
}
