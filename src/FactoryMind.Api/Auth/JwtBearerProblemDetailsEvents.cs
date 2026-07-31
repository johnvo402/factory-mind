using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;

namespace FactoryMind.Api.Auth;

public sealed class JwtBearerProblemDetailsEvents(IProblemDetailsService problemDetailsService) : JwtBearerEvents {
    public override async Task Challenge(JwtBearerChallengeContext context) {
        context.HandleResponse();
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await WriteProblemAsync(
            context.HttpContext,
            StatusCodes.Status401Unauthorized,
            "Unauthorized",
            "Authentication is required.",
            "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.2");
    }

    public override async Task Forbidden(ForbiddenContext context) {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await WriteProblemAsync(
            context.HttpContext,
            StatusCodes.Status403Forbidden,
            "Forbidden",
            "You do not have permission to perform this action.",
            "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.4");
    }

    private Task<bool> WriteProblemAsync(
        HttpContext httpContext,
        int status,
        string title,
        string detail,
        string type) {
        return problemDetailsService.TryWriteAsync(new ProblemDetailsContext {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails {
                Status = status,
                Title = title,
                Detail = detail,
                Type = type
            }
        }).AsTask();
    }
}
