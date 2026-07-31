using FactoryMind.Application.Common.Authorization;
using FactoryMind.Application.Features.Products.CreateProduct;
using FactoryMind.Application.Features.Products.DeleteProduct;
using FactoryMind.Application.Features.Products.GetProducts;
using FactoryMind.Application.Features.Products.UpdateProduct;
using FactoryMind.Api.Routing;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace FactoryMind.Api.Endpoints;

public static class ProductEndpoints {
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup(ApiRoutes.Products.Group)
            .RequireAuthorization(AuthorizationPolicies.Manager);

        group.MapGet(ApiRoutes.Products.Root, async (
            [AsParameters] BusinessDataSearchRequest request,
            ISender sender,
            CancellationToken cancellationToken) => {
                return (await sender.Send(
                    new GetProductsQuery(request.Search),
                    cancellationToken)).ToHttpResult();
            })
            .WithRequestValidation<BusinessDataSearchRequest>();

        group.MapPost(ApiRoutes.Products.Root, async (
            [FromBody] ProductRequest request,
            ISender sender,
            CancellationToken cancellationToken) => {
                return (await sender.Send(
                    new CreateProductCommand(request.Code, request.Name),
                    cancellationToken)).ToHttpResult();
            })
            .WithRequestValidation<ProductRequest>();

        group.MapPut(ApiRoutes.Products.ById, async (
            Guid productId,
            [FromBody] ProductRequest request,
            ISender sender,
            CancellationToken cancellationToken) => {
                return (await sender.Send(
                    new UpdateProductCommand(productId, request.Code, request.Name),
                    cancellationToken)).ToHttpResult();
            })
            .WithRequestValidation<ProductRequest>();

        group.MapDelete(ApiRoutes.Products.ById, async (
            Guid productId,
            ISender sender,
            CancellationToken cancellationToken) => {
                return (await sender.Send(
                    new DeleteProductCommand(productId),
                    cancellationToken)).ToHttpResult();
            });

        return endpoints;
    }
}
