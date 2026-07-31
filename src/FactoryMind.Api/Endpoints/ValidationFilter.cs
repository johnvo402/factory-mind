using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace FactoryMind.Api.Endpoints;

public sealed class ValidationFilter<TRequest>(IValidator<TRequest> validator) : IEndpointFilter
    where TRequest : class {
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next) {
        var request = context.Arguments.OfType<TRequest>().Single();
        var validationResult = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);

        if (validationResult.IsValid) {
            return await next(context);
        }

        var errors = validationResult.Errors
            .GroupBy(failure => failure.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).Distinct().ToArray());

        var problemDetails = new HttpValidationProblemDetails(errors) {
            Type = "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.1",
            Title = "Validation failed",
            Status = StatusCodes.Status400BadRequest,
            Detail = "One or more request fields are invalid.",
            Instance = context.HttpContext.Request.Path
        };
        problemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

        return TypedResults.Problem(problemDetails);
    }
}

public static class EndpointValidationExtensions {
    public static RouteHandlerBuilder WithRequestValidation<TRequest>(this RouteHandlerBuilder builder)
        where TRequest : class {
        return builder
            .AddEndpointFilter<ValidationFilter<TRequest>>()
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);
    }
}
