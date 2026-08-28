using FactoryMind.Api.Routing;
using FactoryMind.Application.Common.Authorization;
using FactoryMind.Application.Features.Boms;
using FactoryMind.Application.Features.Boms.ActivateBom;
using FactoryMind.Application.Features.Boms.ArchiveBom;
using FactoryMind.Application.Features.Boms.CalculateMaterialRequirements;
using FactoryMind.Application.Features.Boms.CreateBom;
using FactoryMind.Application.Features.Boms.GetBom;
using FactoryMind.Application.Features.Boms.GetBoms;
using FactoryMind.Application.Features.Boms.UpdateBom;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace FactoryMind.Api.Endpoints;

public static class BomEndpoints {
    public static IEndpointRouteBuilder MapBomEndpoints(this IEndpointRouteBuilder endpoints) {
        var productGroup = endpoints.MapGroup(ApiRoutes.Products.Group)
            .RequireAuthorization(AuthorizationPolicies.Manager);

        productGroup.MapGet(ApiRoutes.Products.Boms, async (
            Guid productId,
            ISender sender,
            CancellationToken cancellationToken) => {
                return (await sender.Send(new GetBomsQuery(productId), cancellationToken)).ToHttpResult();
            });

        productGroup.MapGet(ApiRoutes.Products.BomById, async (
            Guid productId,
            Guid bomId,
            ISender sender,
            CancellationToken cancellationToken) => {
                return (await sender.Send(new GetBomQuery(productId, bomId), cancellationToken)).ToHttpResult();
            });

        productGroup.MapPost(ApiRoutes.Products.Boms, async (
            Guid productId,
            [FromBody] BomRequest request,
            ISender sender,
            CancellationToken cancellationToken) => {
                return (await sender.Send(
                    new CreateBomCommand(productId, request.OutputQuantity, MapItems(request)),
                    cancellationToken)).ToHttpResult();
            })
            .WithRequestValidation<BomRequest>();

        productGroup.MapPut(ApiRoutes.Products.BomById, async (
            Guid productId,
            Guid bomId,
            [FromBody] BomRequest request,
            ISender sender,
            CancellationToken cancellationToken) => {
                return (await sender.Send(
                    new UpdateBomCommand(productId, bomId, request.OutputQuantity, MapItems(request)),
                    cancellationToken)).ToHttpResult();
            })
            .WithRequestValidation<BomRequest>();

        productGroup.MapPost(ApiRoutes.Products.ActivateBom, async (
            Guid productId,
            Guid bomId,
            ISender sender,
            CancellationToken cancellationToken) => {
                return (await sender.Send(
                    new ActivateBomCommand(productId, bomId),
                    cancellationToken)).ToHttpResult();
            });

        productGroup.MapPost(ApiRoutes.Products.ArchiveBom, async (
            Guid productId,
            Guid bomId,
            ISender sender,
            CancellationToken cancellationToken) => {
                return (await sender.Send(
                    new ArchiveBomCommand(productId, bomId),
                    cancellationToken)).ToHttpResult();
            });

        productGroup.MapGet(ApiRoutes.Products.MaterialRequirements, async (
            Guid productId,
            [AsParameters] MaterialRequirementRequest request,
            ISender sender,
            CancellationToken cancellationToken) => {
                return (await sender.Send(
                    new GetProductMaterialRequirementsQuery(productId, request.Quantity),
                    cancellationToken)).ToHttpResult();
            })
            .WithRequestValidation<MaterialRequirementRequest>();

        var orderGroup = endpoints.MapGroup(ApiRoutes.ProductionOrders.Group)
            .RequireAuthorization(AuthorizationPolicies.Manager);
        orderGroup.MapGet(ApiRoutes.ProductionOrders.MaterialRequirements, async (
            Guid productionOrderId,
            ISender sender,
            CancellationToken cancellationToken) => {
                return (await sender.Send(
                    new GetProductionOrderMaterialRequirementsQuery(productionOrderId),
                    cancellationToken)).ToHttpResult();
            });

        return endpoints;
    }

    private static IReadOnlyList<BomItemDefinition> MapItems(BomRequest request) => request.Items
        .Select(item => new BomItemDefinition(
            item.MaterialId,
            item.Quantity,
            item.ScrapPercentage))
        .ToList();
}
