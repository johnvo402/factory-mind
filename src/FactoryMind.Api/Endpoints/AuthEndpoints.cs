using FactoryMind.Application.Common.Authorization;
using FactoryMind.Application.Features.Auth.Login;
using FactoryMind.Application.Features.Auth.Logout;
using FactoryMind.Application.Features.Auth.Refresh;
using Mediator;

namespace FactoryMind.Api.Endpoints;

public static class AuthEndpoints {
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup("/api/auth");

        group.MapPost("/login", async (LoginCommand command, ISender sender, CancellationToken cancellationToken) => {
            return (await sender.Send(command, cancellationToken)).ToHttpResult();
        }).WithRequestValidation<LoginCommand>();

        group.MapPost("/refresh", async (RefreshTokenCommand command, ISender sender, CancellationToken cancellationToken) => {
            return (await sender.Send(command, cancellationToken)).ToHttpResult();
        }).WithRequestValidation<RefreshTokenCommand>();

        group.MapPost("/logout", async (LogoutCommand command, ISender sender, CancellationToken cancellationToken) => {
            return (await sender.Send(command, cancellationToken)).ToHttpResult();
        })
            .WithRequestValidation<LogoutCommand>()
            .RequireAuthorization(AuthorizationPolicies.Authenticated);

        return endpoints;
    }
}
