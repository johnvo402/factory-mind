using FactoryMind.Application.Features.Auth;
using FactoryMind.Application.Features.Auth.Login;
using FactoryMind.Application.Features.Auth.Logout;
using FactoryMind.Application.Features.Auth.Refresh;
using FactoryMind.Shared.Contracts;

namespace FactoryMind.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth");

        group.MapPost("/login", async (LoginCommand command, LoginCommandHandler handler, CancellationToken cancellationToken) =>
        {
            var session = await handler.HandleAsync(command, cancellationToken);
            return session is null
                ? Results.Json(ApiResponse<AuthSession>.Failure("Email or password is incorrect."), statusCode: StatusCodes.Status401Unauthorized)
                : Results.Ok(ApiResponse<AuthSession>.Ok(session));
        });

        group.MapPost("/refresh", async (RefreshTokenCommand command, RefreshTokenCommandHandler handler, CancellationToken cancellationToken) =>
        {
            var session = await handler.HandleAsync(command, cancellationToken);
            return session is null
                ? Results.Json(ApiResponse<AuthSession>.Failure("Refresh token is invalid or expired."), statusCode: StatusCodes.Status401Unauthorized)
                : Results.Ok(ApiResponse<AuthSession>.Ok(session));
        });

        group.MapPost("/logout", async (LogoutCommand command, LogoutCommandHandler handler, CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(command, cancellationToken);
            return Results.Ok(ApiResponse<object>.Ok(new object()));
        });

        return endpoints;
    }
}
