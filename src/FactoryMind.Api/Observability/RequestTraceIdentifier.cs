using System.Diagnostics;

namespace FactoryMind.Api.Observability;

public static class RequestTraceIdentifier {
    public static string Get(HttpContext context) {
        if (Activity.Current is { } activity) {
            return activity.TraceId.ToString();
        }

        var parts = context.TraceIdentifier.Split('-');
        if (parts.Length == 4
            && parts[1].Length == 32
            && parts[1].All(Uri.IsHexDigit)) {
            return parts[1].ToLowerInvariant();
        }

        return context.TraceIdentifier;
    }
}
