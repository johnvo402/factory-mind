using FactoryMind.Application.Common.Authorization;
using FactoryMind.Application.Features.Machines.CreateMachine;
using FactoryMind.Application.Features.Machines.DeleteMachine;
using FactoryMind.Application.Features.Machines.GetMachines;
using FactoryMind.Application.Features.Machines.UpdateMachine;
using FactoryMind.Api.Routing;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace FactoryMind.Api.Endpoints;

public static class MachineEndpoints {
    public static IEndpointRouteBuilder MapMachineEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup(ApiRoutes.Machines.Group)
            .RequireAuthorization(AuthorizationPolicies.Manager);

        group.MapGet(ApiRoutes.Machines.Root, async (
            [AsParameters] BusinessDataSearchRequest request,
            ISender sender,
            CancellationToken cancellationToken) => {
                return (await sender.Send(
                    new GetMachinesQuery(request.Search),
                    cancellationToken)).ToHttpResult();
            })
            .WithRequestValidation<BusinessDataSearchRequest>();

        group.MapPost(ApiRoutes.Machines.Root, async (
            [FromBody] MachineRequest request,
            ISender sender,
            CancellationToken cancellationToken) => {
                return (await sender.Send(
                    new CreateMachineCommand(request.Code, request.Name, request.Status),
                    cancellationToken)).ToHttpResult();
            })
            .WithRequestValidation<MachineRequest>();

        group.MapPut(ApiRoutes.Machines.ById, async (
            Guid machineId,
            [FromBody] MachineRequest request,
            ISender sender,
            CancellationToken cancellationToken) => {
                return (await sender.Send(
                    new UpdateMachineCommand(machineId, request.Code, request.Name, request.Status),
                    cancellationToken)).ToHttpResult();
            })
            .WithRequestValidation<MachineRequest>();

        group.MapDelete(ApiRoutes.Machines.ById, async (
            Guid machineId,
            ISender sender,
            CancellationToken cancellationToken) => {
                return (await sender.Send(
                    new DeleteMachineCommand(machineId),
                    cancellationToken)).ToHttpResult();
            });

        return endpoints;
    }
}
