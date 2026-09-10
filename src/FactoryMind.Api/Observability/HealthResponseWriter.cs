using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;

namespace FactoryMind.Api.Observability;

public static class HealthResponseWriter {
    public static Task WriteAsync(HttpContext context, HealthReport report) {
        context.Response.ContentType = "application/json";
        var releaseSha = NormalizeReleaseSha(
            context.RequestServices.GetRequiredService<IConfiguration>()["Release:Sha"]);
        var response = new {
            status = report.Status.ToString(),
            releaseSha,
            checks = report.Entries
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => new {
                    name = entry.Key,
                    status = entry.Value.Status.ToString(),
                    durationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 3)
                })
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }

    private static string NormalizeReleaseSha(string? value) {
        var candidate = value?.Trim();
        return candidate is { Length: 40 } && candidate.All(Uri.IsHexDigit)
            ? candidate.ToLowerInvariant()
            : "unknown";
    }
}
