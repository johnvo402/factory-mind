using FactoryMind.Api.Routing;
using FactoryMind.Application.Common.Authorization;
using FactoryMind.Application.Features.ProductionOrders;
using FactoryMind.Application.Features.ProductionOrders.CancelProductionOrder;
using FactoryMind.Application.Features.ProductionOrders.CompleteProductionOrder;
using FactoryMind.Application.Features.ProductionOrders.CreateProductionOrder;
using FactoryMind.Application.Features.ProductionOrders.DeleteProductionOrder;
using FactoryMind.Application.Features.ProductionOrders.GetProductionOrders;
using FactoryMind.Application.Features.ProductionOrders.ReleaseProductionOrder;
using FactoryMind.Application.Features.ProductionOrders.StartProductionOrder;
using FactoryMind.Application.Features.ProductionOrders.UpdateProductionOrder;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace FactoryMind.Api.Endpoints;

public static class ProductionOrderEndpoints {
    public static IEndpointRouteBuilder MapProductionOrderEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup(ApiRoutes.ProductionOrders.Group)
            .RequireAuthorization(AuthorizationPolicies.Manager);

        group.MapGet(ApiRoutes.ProductionOrders.Root, async (
            [AsParameters] BusinessDataSearchRequest request,
            ISender sender,
            CancellationToken cancellationToken) => {
                return (await sender.Send(
                    new GetProductionOrdersQuery(request.Search),
                    cancellationToken)).ToHttpResult();
            })
            .WithRequestValidation<BusinessDataSearchRequest>();

        group.MapPost(ApiRoutes.ProductionOrders.Root, async (
            [FromBody] ProductionOrderRequest request,
            ISender sender,
            CancellationToken cancellationToken) => {
                return (await sender.Send(
                    new CreateProductionOrderCommand(
                        request.Number,
                        request.ProductId,
                        request.Quantity),
                    cancellationToken)).ToHttpResult();
            })
            .WithRequestValidation<ProductionOrderRequest>();

        group.MapPut(ApiRoutes.ProductionOrders.ById, async (
            Guid productionOrderId,
            [FromBody] ProductionOrderRequest request,
            ISender sender,
            CancellationToken cancellationToken) => {
                return (await sender.Send(
                    new UpdateProductionOrderCommand(
                        productionOrderId,
                        request.Number,
                        request.ProductId,
                        request.Quantity),
                    cancellationToken)).ToHttpResult();
            })
            .WithRequestValidation<ProductionOrderRequest>();

        group.MapPost(ApiRoutes.ProductionOrders.Release, async (
            Guid productionOrderId,
            ISender sender,
            CancellationToken cancellationToken) => {
                return (await sender.Send(
                    new ReleaseProductionOrderCommand(productionOrderId),
                    cancellationToken)).ToHttpResult();
            });

        group.MapPost(ApiRoutes.ProductionOrders.Start, async (
            Guid productionOrderId,
            [FromBody] StartProductionOrderRequest request,
            ISender sender,
            CancellationToken cancellationToken) => {
                return (await sender.Send(
                    new StartProductionOrderCommand(
                        productionOrderId,
                        request.Allocations.Select(allocation => new ProductionMaterialAllocation(
                            allocation.MaterialId,
                            allocation.WarehouseId,
                            allocation.Quantity)).ToList()),
                    cancellationToken)).ToHttpResult();
            })
            .WithRequestValidation<StartProductionOrderRequest>();

        group.MapPost(ApiRoutes.ProductionOrders.Complete, async (
            Guid productionOrderId,
            [FromBody] CompleteProductionOrderRequest request,
            ISender sender,
            CancellationToken cancellationToken) => {
                return (await sender.Send(
                    new CompleteProductionOrderCommand(productionOrderId, request.WarehouseId),
                    cancellationToken)).ToHttpResult();
            })
            .WithRequestValidation<CompleteProductionOrderRequest>();

        group.MapPost(ApiRoutes.ProductionOrders.Cancel, async (
            Guid productionOrderId,
            ISender sender,
            CancellationToken cancellationToken) => {
                return (await sender.Send(
                    new CancelProductionOrderCommand(productionOrderId),
                    cancellationToken)).ToHttpResult();
            });

        group.MapDelete(ApiRoutes.ProductionOrders.ById, async (
            Guid productionOrderId,
            ISender sender,
            CancellationToken cancellationToken) => {
                return (await sender.Send(
                    new DeleteProductionOrderCommand(productionOrderId),
                    cancellationToken)).ToHttpResult();
            });

        return endpoints;
    }
}
