using FactoryMind.Application.Common.Authorization;
using FactoryMind.Application.Features.Auth.Login;
using FactoryMind.Application.Features.Auth.Logout;
using FactoryMind.Application.Features.Auth.Refresh;
using FactoryMind.Api.Routing;
using Mediator;

namespace FactoryMind.Api.Endpoints;

public static class AuthEndpoints {
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup(ApiRoutes.Auth.Group);

        group.MapPost(ApiRoutes.Auth.Login, async (LoginCommand command, ISender sender, CancellationToken cancellationToken) => {
            return (await sender.Send(command, cancellationToken)).ToHttpResult();
        }).WithRequestValidation<LoginCommand>();

        group.MapPost(ApiRoutes.Auth.Refresh, async (RefreshTokenCommand command, ISender sender, CancellationToken cancellationToken) => {
            return (await sender.Send(command, cancellationToken)).ToHttpResult();
        }).WithRequestValidation<RefreshTokenCommand>();

        group.MapPost(ApiRoutes.Auth.Logout, async (LogoutCommand command, ISender sender, CancellationToken cancellationToken) => {
            return (await sender.Send(command, cancellationToken)).ToHttpResult();
        })
            .WithRequestValidation<LogoutCommand>()
            .RequireAuthorization(AuthorizationPolicies.Authenticated);

        return endpoints;
    }
}
