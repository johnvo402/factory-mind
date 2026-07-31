using System.Net;
using FactoryMind.Shared.AI;
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
        ILogger logger,
        CancellationToken cancellationToken) {
        for (var attempt = 1; attempt <= MaximumAttempts; attempt++) {
            using var request = requestFactory();
            HttpResponseMessage response;

            try {
                response = await httpClient.SendAsync(request, completionOption, cancellationToken);
            } catch (HttpRequestException exception) when (attempt < MaximumAttempts) {
                logger.LogWarning(exception, "Gemini request failed; retrying once");
                await Task.Delay(DefaultRetryDelay, cancellationToken);
                continue;
            } catch (HttpRequestException exception) {
                throw new AiProviderException("AI service is temporarily unavailable.", exception);
            }

            if (response.IsSuccessStatusCode) {
                return response;
            }

            var statusCode = response.StatusCode;
            if (statusCode == HttpStatusCode.TooManyRequests) {
                response.Dispose();
                throw new AiProviderException(
                    "AI free-tier quota is temporarily exhausted. Please try again later.");
            }

            if (attempt < MaximumAttempts && IsTransient(statusCode)) {
                var delay = response.Headers.RetryAfter?.Delta ?? DefaultRetryDelay;
                response.Dispose();
                logger.LogWarning(
                    "Gemini returned status code {StatusCode}; retrying once",
                    (int)statusCode);
                await Task.Delay(delay > MaximumRetryDelay ? MaximumRetryDelay : delay, cancellationToken);
                continue;
            }

            logger.LogWarning("Gemini returned status code {StatusCode}", (int)statusCode);
            response.Dispose();
            throw new AiProviderException("AI service is temporarily unavailable.");
        }

        throw new AiProviderException("AI service is temporarily unavailable.");
    }

    private static bool IsTransient(HttpStatusCode statusCode) => statusCode is
        HttpStatusCode.RequestTimeout
        or HttpStatusCode.BadGateway
        or HttpStatusCode.ServiceUnavailable
        or HttpStatusCode.GatewayTimeout;
}
