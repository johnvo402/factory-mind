using FactoryMind.Application.Common.Authorization;
using FactoryMind.Application.Features.Materials.CreateMaterial;
using FactoryMind.Application.Features.Materials.DeleteMaterial;
using FactoryMind.Application.Features.Materials.GetMaterials;
using FactoryMind.Application.Features.Materials.UpdateMaterial;
using FactoryMind.Api.Routing;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace FactoryMind.Api.Endpoints;

public static class MaterialEndpoints {
    public static IEndpointRouteBuilder MapMaterialEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup(ApiRoutes.Materials.Group)
            .RequireAuthorization(AuthorizationPolicies.Manager);

        group.MapGet(ApiRoutes.Materials.Root, async (
            [AsParameters] BusinessDataSearchRequest request,
            ISender sender,
            CancellationToken cancellationToken) => {
                return (await sender.Send(
                    new GetMaterialsQuery(request.Search),
                    cancellationToken)).ToHttpResult();
            })
            .WithRequestValidation<BusinessDataSearchRequest>();

        group.MapPost(ApiRoutes.Materials.Root, async (
            [FromBody] MaterialRequest request,
            ISender sender,
            CancellationToken cancellationToken) => {
                return (await sender.Send(
                    new CreateMaterialCommand(request.Code, request.Name, request.Unit),
                    cancellationToken)).ToHttpResult();
            })
            .WithRequestValidation<MaterialRequest>();

        group.MapPut(ApiRoutes.Materials.ById, async (
            Guid materialId,
            [FromBody] MaterialRequest request,
            ISender sender,
            CancellationToken cancellationToken) => {
                return (await sender.Send(
                    new UpdateMaterialCommand(materialId, request.Code, request.Name, request.Unit),
                    cancellationToken)).ToHttpResult();
            })
            .WithRequestValidation<MaterialRequest>();

        group.MapDelete(ApiRoutes.Materials.ById, async (
            Guid materialId,
            ISender sender,
            CancellationToken cancellationToken) => {
                return (await sender.Send(
                    new DeleteMaterialCommand(materialId),
                    cancellationToken)).ToHttpResult();
            });

        return endpoints;
    }
}
