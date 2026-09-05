using FactoryMind.Api.Routing;
using FactoryMind.Application.Common.Authorization;
using FactoryMind.Application.Features.Routings;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace FactoryMind.Api.Endpoints;

public static class RoutingEndpoints {
    public static IEndpointRouteBuilder MapRoutingEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup(ApiRoutes.Products.Group)
            .RequireAuthorization(AuthorizationPolicies.Manager);

        group.MapGet(ApiRoutes.Products.Routings, async (
            Guid productId,
            ISender sender,
            CancellationToken cancellationToken) => (await sender.Send(
                new GetRoutingsQuery(productId), cancellationToken)).ToHttpResult());
        group.MapGet(ApiRoutes.Products.RoutingById, async (
            Guid productId,
            Guid routingId,
            ISender sender,
            CancellationToken cancellationToken) => (await sender.Send(
                new GetRoutingQuery(productId, routingId), cancellationToken)).ToHttpResult());
        group.MapPost(ApiRoutes.Products.Routings, async (
            Guid productId,
            [FromBody] RoutingRequest request,
            ISender sender,
            CancellationToken cancellationToken) => (await sender.Send(
                new CreateRoutingCommand(productId, MapOperations(request)), cancellationToken)).ToHttpResult())
            .WithRequestValidation<RoutingRequest>();
        group.MapPut(ApiRoutes.Products.RoutingById, async (
            Guid productId,
            Guid routingId,
            [FromBody] RoutingRequest request,
            ISender sender,
            CancellationToken cancellationToken) => (await sender.Send(
                new UpdateRoutingCommand(productId, routingId, MapOperations(request)),
                cancellationToken)).ToHttpResult())
            .WithRequestValidation<RoutingRequest>();
        group.MapPost(ApiRoutes.Products.ActivateRouting, async (
            Guid productId,
            Guid routingId,
            ISender sender,
            CancellationToken cancellationToken) => (await sender.Send(
                new ActivateRoutingCommand(productId, routingId), cancellationToken)).ToHttpResult());
        return endpoints;
    }

    private static IReadOnlyList<RoutingOperationDefinition> MapOperations(RoutingRequest request) =>
        request.Operations.Select(operation => new RoutingOperationDefinition(
            operation.Sequence,
            operation.Name,
            operation.WorkCenterId,
            operation.SetupTimeMinutes,
            operation.RunTimeMinutes,
            operation.Description)).ToList();
}
