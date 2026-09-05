using FactoryMind.Api.Routing;
using FactoryMind.Application.Common.Authorization;
using FactoryMind.Application.Features.WorkCenters;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace FactoryMind.Api.Endpoints;

public static class WorkCenterEndpoints {
    public static IEndpointRouteBuilder MapWorkCenterEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup(ApiRoutes.WorkCenters.Group)
            .RequireAuthorization(AuthorizationPolicies.Manager);

        group.MapGet(ApiRoutes.WorkCenters.Root, async (
            [AsParameters] BusinessDataSearchRequest request,
            ISender sender,
            CancellationToken cancellationToken) => (await sender.Send(
                new GetWorkCentersQuery(request.Search), cancellationToken)).ToHttpResult())
            .WithRequestValidation<BusinessDataSearchRequest>();
        group.MapGet(ApiRoutes.WorkCenters.ById, async (
            Guid workCenterId,
            ISender sender,
            CancellationToken cancellationToken) => (await sender.Send(
                new GetWorkCenterQuery(workCenterId), cancellationToken)).ToHttpResult());
        group.MapPost(ApiRoutes.WorkCenters.Root, async (
            [FromBody] WorkCenterCreateRequest request,
            ISender sender,
            CancellationToken cancellationToken) => (await sender.Send(
                new CreateWorkCenterCommand(request.Code, request.Name, request.Description),
                cancellationToken)).ToHttpResult())
            .WithRequestValidation<WorkCenterCreateRequest>();
        group.MapPut(ApiRoutes.WorkCenters.ById, async (
            Guid workCenterId,
            [FromBody] WorkCenterUpdateRequest request,
            ISender sender,
            CancellationToken cancellationToken) => (await sender.Send(
                new UpdateWorkCenterCommand(
                    workCenterId, request.Code, request.Name, request.Description),
                cancellationToken)).ToHttpResult())
            .WithRequestValidation<WorkCenterUpdateRequest>();
        group.MapPost(ApiRoutes.WorkCenters.Deactivate, async (
            Guid workCenterId,
            ISender sender,
            CancellationToken cancellationToken) => (await sender.Send(
                new DeactivateWorkCenterCommand(workCenterId), cancellationToken)).ToHttpResult());
        return endpoints;
    }
}
