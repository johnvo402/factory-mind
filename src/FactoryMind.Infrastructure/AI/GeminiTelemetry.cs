using System.Diagnostics;
using FactoryMind.Shared.Observability;

namespace FactoryMind.Infrastructure.AI;

internal static class GeminiTelemetry {
    public const string Success = "success";
    public const string Error = "error";
    public const string Cancelled = "cancelled";
    public const string Timeout = "timeout";
    public const string Quota = "quota";
    public const string InvalidResponse = "invalid_response";

    public static void RecordRequest(
        string operation,
        string model,
        string outcome,
        long startedTimestamp) {
        var tags = FactoryMindTelemetry.Tags(
            ("operation", operation),
            ("model", model),
            ("outcome", outcome));
        FactoryMindTelemetry.AiRequests.Add(1, tags);
        FactoryMindTelemetry.AiRequestDuration.Record(
            Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds,
            tags);
    }

    public static void RecordUsage(
        string operation,
        string model,
        GeminiUsageMetadata usage) {
        var tags = FactoryMindTelemetry.Tags(
            ("operation", operation),
            ("model", model));
        if (usage.PromptTokenCount.HasValue) {
            FactoryMindTelemetry.AiInputTokens.Add(usage.PromptTokenCount.Value, tags);
        }

        if (usage.CandidatesTokenCount.HasValue) {
            FactoryMindTelemetry.AiOutputTokens.Add(usage.CandidatesTokenCount.Value, tags);
        }

        if (usage.TotalTokenCount.HasValue) {
            FactoryMindTelemetry.AiTotalTokens.Add(usage.TotalTokenCount.Value, tags);
        }
    }

    public static Exception TranslateCancellation(
        OperationCanceledException exception,
        CancellationToken callerToken,
        CancellationToken timeoutToken,
        out string outcome) {
        if (callerToken.IsCancellationRequested) {
            outcome = Cancelled;
            return exception;
        }

        if (timeoutToken.IsCancellationRequested) {
            outcome = Timeout;
            return new FactoryMind.Shared.AI.AiProviderException("AI service request timed out.", exception);
        }

        outcome = Error;
        return exception;
    }
}

internal sealed record GeminiUsageMetadata(
    long? PromptTokenCount,
    long? CandidatesTokenCount,
    long? TotalTokenCount);

internal sealed class GeminiTransportException(
    string message,
    string outcome,
    Exception? innerException = null) : Exception(message, innerException) {
    public string Outcome { get; } = outcome;
}
