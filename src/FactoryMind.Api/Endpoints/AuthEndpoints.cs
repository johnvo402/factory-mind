using FactoryMind.Api.Auth;
using FactoryMind.Application.Features.Auth;
using FactoryMind.Application.Features.Auth.Login;
using FactoryMind.Application.Features.Auth.Logout;
using FactoryMind.Application.Features.Auth.Refresh;
using FactoryMind.Api.Routing;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Api.Endpoints;

public static class AuthEndpoints {
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup(ApiRoutes.Auth.Group);

        group.MapPost(ApiRoutes.Auth.Login, async (
            LoginCommand command,
            ISender sender,
            RefreshTokenCookie refreshTokenCookie,
            HttpResponse response,
            CancellationToken cancellationToken) => {
                var result = await sender.Send(command, cancellationToken);
                return CompleteSession(result, refreshTokenCookie, response);
            }).WithRequestValidation<LoginCommand>();

        group.MapPost(ApiRoutes.Auth.Refresh, async (
            ISender sender,
            RefreshTokenCookie refreshTokenCookie,
            HttpRequest request,
            HttpResponse response,
            CancellationToken cancellationToken) => {
                var command = new RefreshTokenCommand(refreshTokenCookie.Read(request) ?? string.Empty);
                var result = await sender.Send(command, cancellationToken);
                return CompleteSession(result, refreshTokenCookie, response);
            });

        group.MapPost(ApiRoutes.Auth.Logout, async (
            ISender sender,
            RefreshTokenCookie refreshTokenCookie,
            HttpRequest request,
            HttpResponse response,
            CancellationToken cancellationToken) => {
                var command = new LogoutCommand(refreshTokenCookie.Read(request) ?? string.Empty);
                var result = await sender.Send(command, cancellationToken);
                refreshTokenCookie.Delete(response);
                return result.ToHttpResult();
            });

        return endpoints;
    }

    private static IResult CompleteSession(
        Result<AuthSession> result,
        RefreshTokenCookie refreshTokenCookie,
        HttpResponse response) {
        if (result.IsFailure) {
            return result.ToHttpResult();
        }

        var session = result.Value!;
        refreshTokenCookie.Write(response, session.RefreshToken, session.RefreshTokenExpiresAt);
        var authResponse = new AuthResponse(session.AccessToken, session.User);
        return Results.Ok(new ApiResponse<AuthResponse>(true, "OK", authResponse));
    }
}
