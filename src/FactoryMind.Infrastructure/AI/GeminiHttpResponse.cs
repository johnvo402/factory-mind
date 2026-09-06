using System.Net;
using FactoryMind.Shared.AI;
using FactoryMind.Shared.Observability;
using Microsoft.Extensions.Logging;

namespace FactoryMind.Infrastructure.AI;

internal static class GeminiHttpResponse {
    private const int MaximumAttempts = 2;
    private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan MaximumRetryDelay = TimeSpan.FromSeconds(2);

    public static async Task<HttpResponseMessage> SendAsync(
        HttpClient httpClient,
        Func<HttpRequestMessage> requestFactory,
        HttpCompletionOption completionOption,
        string operation,
        string model,
        ILogger logger,
        CancellationToken cancellationToken) {
        for (var attempt = 1; attempt <= MaximumAttempts; attempt++) {
            using var request = requestFactory();
            HttpResponseMessage response;

            try {
                response = await httpClient.SendAsync(request, completionOption, cancellationToken);
            } catch (HttpRequestException) when (attempt < MaximumAttempts) {
                FactoryMindTelemetry.AiRetries.Add(1, FactoryMindTelemetry.Tags(
                    ("operation", operation),
                    ("model", model),
                    ("reason", "transport")));
                logger.LogWarning("Gemini {Operation} transport failed; retrying once", operation);
                await Task.Delay(DefaultRetryDelay, cancellationToken);
                continue;
            } catch (HttpRequestException exception) {
                throw new GeminiTransportException(
                    "AI service is temporarily unavailable.",
                    GeminiTelemetry.Error,
                    exception);
            }

            if (response.IsSuccessStatusCode) {
                return response;
            }

            var statusCode = response.StatusCode;
            if (statusCode == HttpStatusCode.TooManyRequests) {
                response.Dispose();
                throw new GeminiTransportException(
                    "AI free-tier quota is temporarily exhausted. Please try again later.",
                    GeminiTelemetry.Quota);
            }

            if (attempt < MaximumAttempts && IsTransient(statusCode)) {
                var delay = response.Headers.RetryAfter?.Delta ?? DefaultRetryDelay;
                response.Dispose();
                FactoryMindTelemetry.AiRetries.Add(1, FactoryMindTelemetry.Tags(
                    ("operation", operation),
                    ("model", model),
                    ("reason", "server")));
                logger.LogWarning(
                    "Gemini {Operation} returned status code {StatusCode}; retrying once",
                    operation,
                    (int)statusCode);
                await Task.Delay(delay > MaximumRetryDelay ? MaximumRetryDelay : delay, cancellationToken);
                continue;
            }

            logger.LogWarning(
                "Gemini {Operation} returned status code {StatusCode}",
                operation,
                (int)statusCode);
            response.Dispose();
            throw new GeminiTransportException(
                "AI service is temporarily unavailable.",
                GeminiTelemetry.Error);
        }

        throw new GeminiTransportException(
            "AI service is temporarily unavailable.",
            GeminiTelemetry.Error);
    }

    private static bool IsTransient(HttpStatusCode statusCode) => statusCode is
        HttpStatusCode.InternalServerError
        or HttpStatusCode.RequestTimeout
        or HttpStatusCode.BadGateway
        or HttpStatusCode.ServiceUnavailable
        or HttpStatusCode.GatewayTimeout;
}
