using FactoryMind.Application.Common.Authorization;
using FactoryMind.Application.Features.Settings.CreateUser;
using FactoryMind.Application.Features.Settings.GetAiSettings;
using FactoryMind.Application.Features.Settings.GetCompanySettings;
using FactoryMind.Application.Features.Settings.GetUsers;
using FactoryMind.Application.Features.Settings.UpdateCompanySettings;
using FactoryMind.Application.Features.Settings.UpdateUser;
using FactoryMind.Api.Routing;
using Mediator;

namespace FactoryMind.Api.Endpoints;

public static class SettingsEndpoints {
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup(ApiRoutes.Settings.Group)
            .RequireAuthorization(AuthorizationPolicies.Admin);

        group.MapGet(ApiRoutes.Settings.Company, async (
            ISender sender,
            CancellationToken cancellationToken) =>
            (await sender.Send(new GetCompanySettingsQuery(), cancellationToken)).ToHttpResult());
        group.MapPut(ApiRoutes.Settings.Company, async (
            UpdateCompanySettingsRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
            (await sender.Send(
                new UpdateCompanySettingsCommand(request.Name),
                cancellationToken)).ToHttpResult())
            .WithRequestValidation<UpdateCompanySettingsRequest>();

        group.MapGet(ApiRoutes.Settings.Users, async (
            ISender sender,
            CancellationToken cancellationToken) =>
            (await sender.Send(new GetUsersQuery(), cancellationToken)).ToHttpResult());
        group.MapPost(ApiRoutes.Settings.Users, async (
            CreateUserRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
            (await sender.Send(new CreateUserCommand(
                request.Name,
                request.Email,
                request.Password,
                request.Role), cancellationToken)).ToHttpResult())
            .WithRequestValidation<CreateUserRequest>();
        group.MapPut(ApiRoutes.Settings.UserById, async (
            Guid userId,
            UpdateUserRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
            (await sender.Send(new UpdateUserCommand(
                userId,
                request.Name,
                request.Email,
                request.Role,
                request.IsActive), cancellationToken)).ToHttpResult())
            .WithRequestValidation<UpdateUserRequest>();

        group.MapGet(ApiRoutes.Settings.Ai, async (
            ISender sender,
            CancellationToken cancellationToken) =>
            (await sender.Send(new GetAiSettingsQuery(), cancellationToken)).ToHttpResult());

        return endpoints;
    }
}
