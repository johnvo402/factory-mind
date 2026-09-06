using System.Diagnostics;
using System.Text.RegularExpressions;

namespace FactoryMind.Api.Observability;

public sealed partial class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger) {
    public const string HeaderName = "X-Correlation-ID";
    public const int MaximumLength = 128;

    public async Task InvokeAsync(HttpContext context) {
        var supplied = context.Request.Headers[HeaderName].FirstOrDefault();
        var correlationId = IsValid(supplied)
            ? supplied!
            : Guid.NewGuid().ToString("N");
        context.Response.Headers[HeaderName] = correlationId;

        var activity = Activity.Current;
        using (logger.BeginScope(new Dictionary<string, object> {
            ["CorrelationId"] = correlationId,
            ["TraceId"] = activity?.TraceId.ToString() ?? context.TraceIdentifier,
            ["SpanId"] = activity?.SpanId.ToString() ?? string.Empty
        })) {
            await next(context);
        }
    }

    public static bool IsValid(string? value) => value is not null
        && value.Length is > 0 and <= MaximumLength
        && SafeCorrelationId().IsMatch(value);

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._:/-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeCorrelationId();
}
