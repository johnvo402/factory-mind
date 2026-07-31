using FactoryMind.Application.Common.Authorization;
using FactoryMind.Application.Features.Chat;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FactoryMind.Api.Errors;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler {
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken) {
        var problemDetails = exception switch {
            AuthenticationRequiredException => CreateProblem(
                StatusCodes.Status401Unauthorized,
                "Authentication required",
                exception.Message,
                "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.2"),
            ForbiddenAccessException => CreateProblem(
                StatusCodes.Status403Forbidden,
                "Forbidden",
                exception.Message,
                "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.4"),
            AiProviderException => CreateProblem(
                StatusCodes.Status502BadGateway,
                "AI service unavailable",
                exception.Message,
                "https://www.rfc-editor.org/rfc/rfc9110#section-15.6.3"),
            _ => CreateProblem(
                StatusCodes.Status500InternalServerError,
                "Internal server error",
                "An unexpected error occurred.",
                "https://www.rfc-editor.org/rfc/rfc9110#section-15.6.1")
        };

        if (problemDetails.Status >= StatusCodes.Status500InternalServerError) {
            logger.LogError(exception, "Unhandled exception for request {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = problemDetails.Status!.Value;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        });
    }

    private static ProblemDetails CreateProblem(int status, string title, string detail, string type) =>
        new() { Status = status, Title = title, Detail = detail, Type = type };
}
