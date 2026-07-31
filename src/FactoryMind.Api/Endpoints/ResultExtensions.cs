using FactoryMind.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace FactoryMind.Api.Endpoints;

public static class ResultExtensions {
    public static IResult ToHttpResult<T>(this Result<T> result) {
        return result.IsSuccess
            ? Results.Ok(new ApiResponse<T>(true, "OK", result.Value))
            : ToProblem(result.Error!);
    }

    public static IResult ToHttpResult(this Result result) {
        return result.IsSuccess
            ? Results.Ok(new ApiResponse<object>(true, "OK", new object()))
            : ToProblem(result.Error!);
    }

    private static IResult ToProblem(Error error) {
        var problemDetails = new ProblemDetails {
            Type = ProblemTypeFor(error.StatusCode),
            Title = TitleFor(error.StatusCode),
            Status = error.StatusCode,
            Detail = error.Message
        };
        problemDetails.Extensions["code"] = error.Code;
        return TypedResults.Problem(problemDetails);
    }

    private static string TitleFor(int statusCode) => statusCode switch {
        StatusCodes.Status400BadRequest => "Bad request",
        StatusCodes.Status401Unauthorized => "Unauthorized",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "Not found",
        StatusCodes.Status409Conflict => "Conflict",
        StatusCodes.Status502BadGateway => "Bad gateway",
        StatusCodes.Status503ServiceUnavailable => "Service unavailable",
        _ => "Request failed"
    };

    private static string ProblemTypeFor(int statusCode) => statusCode switch {
        StatusCodes.Status400BadRequest => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.1",
        StatusCodes.Status401Unauthorized => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.2",
        StatusCodes.Status403Forbidden => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.4",
        StatusCodes.Status404NotFound => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.5",
        StatusCodes.Status409Conflict => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.10",
        StatusCodes.Status502BadGateway => "https://www.rfc-editor.org/rfc/rfc9110#section-15.6.3",
        StatusCodes.Status503ServiceUnavailable => "https://www.rfc-editor.org/rfc/rfc9110#section-15.6.4",
        _ => "about:blank"
    };
}
