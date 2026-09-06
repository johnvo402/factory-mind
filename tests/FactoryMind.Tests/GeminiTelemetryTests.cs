using System.Net;
using System.Text;
using FactoryMind.Application.Features.Chat;
using FactoryMind.Application.Features.Knowledge;
using FactoryMind.Infrastructure.AI;
using FactoryMind.Shared.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FactoryMind.Tests;

public sealed class GeminiTelemetryTests {
    [Fact]
    public async Task Streaming_records_exact_usage_and_timing_without_exposing_metadata_or_sensitive_tags() {
        const string responseBody = """
            data: {"candidates":[{"content":{"parts":[{"text":"Machine"}]}}]}

            data: {"candidates":[{"content":{"parts":[{"text":" A"}]}}]}

            data: {"usageMetadata":{"promptTokenCount":17,"candidatesTokenCount":5,"totalTokenCount":22}}

            """;
        var handler = new SequenceHandler(_ => Response(HttpStatusCode.OK, responseBody, "text/event-stream"));
        var client = CreateChatClient(handler, "chat-telemetry-success");
        using var telemetry = new TelemetryTestListener();
        var output = new StringBuilder();

        await foreach (var chunk in client.StreamAsync(
            [new ChatPromptMessage("user", "super-secret-question")],
            CancellationToken.None)) {
            output.Append(chunk);
        }

        Assert.Equal("Machine A", output.ToString());
        AssertMeasurement(telemetry, "factorymind.ai.requests", 1, "chat-telemetry-success", "success");
        AssertMeasurement(telemetry, "factorymind.ai.input.tokens", 17, "chat-telemetry-success");
        AssertMeasurement(telemetry, "factorymind.ai.output.tokens", 5, "chat-telemetry-success");
        AssertMeasurement(telemetry, "factorymind.ai.total.tokens", 22, "chat-telemetry-success");
        AssertMeasurement(telemetry, "factorymind.ai.chat.generated_chunks", 2, "chat-telemetry-success", "success");
        Assert.Contains(telemetry.Measurements, item =>
            item.Name == "factorymind.ai.chat.response_headers.duration"
            && item.HasTags(("model", "chat-telemetry-success")));
        Assert.Contains(telemetry.Measurements, item =>
            item.Name == "factorymind.ai.chat.time_to_first_token"
            && item.HasTags(("model", "chat-telemetry-success")));
        Assert.Contains(telemetry.Measurements, item =>
            item.Name == "factorymind.ai.chat.stream.duration"
            && item.HasTags(("model", "chat-telemetry-success"), ("outcome", "success")));
        var telemetryText = string.Join('|', telemetry.Measurements.SelectMany(item => item.Tags.Values));
        Assert.DoesNotContain("super-secret-question", telemetryText, StringComparison.Ordinal);
        Assert.DoesNotContain("test-key", telemetryText, StringComparison.Ordinal);
        Assert.DoesNotContain("Machine A", telemetryText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Transient_server_failures_retry_once_and_record_error(
        HttpStatusCode statusCode) {
        var handler = new SequenceHandler(_ => Response(statusCode, "failure"));
        var model = $"chat-transient-{(int)statusCode}";
        var client = CreateChatClient(handler, model);
        using var telemetry = new TelemetryTestListener();

        await Assert.ThrowsAsync<AiProviderException>(() => ConsumeAsync(client, CancellationToken.None));

        Assert.Equal(2, handler.RequestCount);
        AssertMeasurement(telemetry, "factorymind.ai.retries", 1, model);
        AssertMeasurement(telemetry, "factorymind.ai.requests", 1, model, "error");
    }

    [Fact]
    public async Task Quota_is_not_retried_and_is_classified_separately() {
        var handler = new SequenceHandler(_ => Response(HttpStatusCode.TooManyRequests, "quota"));
        var client = CreateChatClient(handler, "chat-quota");
        using var telemetry = new TelemetryTestListener();

        await Assert.ThrowsAsync<AiProviderException>(() => ConsumeAsync(client, CancellationToken.None));

        Assert.Equal(1, handler.RequestCount);
        AssertMeasurement(telemetry, "factorymind.ai.requests", 1, "chat-quota", "quota");
    }

    [Fact]
    public async Task Transport_exception_retries_once_and_records_error() {
        var handler = new SequenceHandler(_ => throw new HttpRequestException("network unavailable"));
        var client = CreateChatClient(handler, "chat-transport");
        using var telemetry = new TelemetryTestListener();

        await Assert.ThrowsAsync<AiProviderException>(() => ConsumeAsync(client, CancellationToken.None));

        Assert.Equal(2, handler.RequestCount);
        AssertMeasurement(telemetry, "factorymind.ai.retries", 1, "chat-transport");
        AssertMeasurement(telemetry, "factorymind.ai.requests", 1, "chat-transport", "error");
    }

    [Fact]
    public async Task Configured_timeout_is_distinct_from_caller_cancellation() {
        var timeoutHandler = new DelayedHandler();
        var timeoutClient = CreateChatClient(timeoutHandler, "chat-timeout", timeoutSeconds: 1);
        using var telemetry = new TelemetryTestListener();

        var timeout = await Assert.ThrowsAsync<AiProviderException>(() =>
            ConsumeAsync(timeoutClient, CancellationToken.None));

        Assert.Equal("AI service request timed out.", timeout.Message);
        AssertMeasurement(telemetry, "factorymind.ai.requests", 1, "chat-timeout", "timeout");

        var cancellationHandler = new DelayedHandler();
        var cancellationClient = CreateChatClient(cancellationHandler, "chat-cancelled");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ConsumeAsync(cancellationClient, cancellation.Token));

        AssertMeasurement(telemetry, "factorymind.ai.requests", 1, "chat-cancelled", "cancelled");
    }

    [Fact]
    public async Task Invalid_stream_after_output_is_not_retried() {
        const string responseBody = """
            data: {"candidates":[{"content":{"parts":[{"text":"first"}]}}]}

            data: {invalid-json}

            """;
        var handler = new SequenceHandler(_ => Response(HttpStatusCode.OK, responseBody, "text/event-stream"));
        var client = CreateChatClient(handler, "chat-stream-failure");
        using var telemetry = new TelemetryTestListener();
        await using var enumerator = client.StreamAsync(
            [new ChatPromptMessage("user", "question")],
            CancellationToken.None).GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal("first", enumerator.Current);
        await Assert.ThrowsAsync<AiProviderException>(async () => await enumerator.MoveNextAsync());

        Assert.Equal(1, handler.RequestCount);
        AssertMeasurement(
            telemetry,
            "factorymind.ai.requests",
            1,
            "chat-stream-failure",
            "invalid_response");
    }

    [Fact]
    public async Task Embedding_records_batch_timing_and_exact_provider_usage() {
        var responseBody = $$"""
            {
              "embeddings": [{ "values": {{VectorJson(1f)}} }],
              "usageMetadata": {
                "promptTokenCount": 9,
                "candidatesTokenCount": 0,
                "totalTokenCount": 9
              }
            }
            """;
        var handler = new SequenceHandler(_ => Response(HttpStatusCode.OK, responseBody));
        var client = CreateEmbeddingClient(handler, "embedding-telemetry");
        using var telemetry = new TelemetryTestListener();

        var result = await client.CreateAsync(
            ["private document content"],
            EmbeddingPurpose.Document,
            CancellationToken.None);

        Assert.Single(result.Vectors);
        AssertMeasurement(telemetry, "factorymind.ai.requests", 1, "embedding-telemetry", "success");
        AssertMeasurement(telemetry, "factorymind.ai.input.tokens", 9, "embedding-telemetry");
        AssertMeasurement(telemetry, "factorymind.ai.total.tokens", 9, "embedding-telemetry");
        Assert.Contains(telemetry.Measurements, item =>
            item.Name == "factorymind.ai.embedding.batch_size"
            && item.Value == 1
            && item.HasTags(
                ("model", "embedding-telemetry"),
                ("purpose", "document"),
                ("outcome", "success")));
        Assert.DoesNotContain(
            telemetry.Measurements.SelectMany(item => item.Tags.Values),
            value => string.Equals(Convert.ToString(value), "private document content", StringComparison.Ordinal));
    }

    private static void AssertMeasurement(
        TelemetryTestListener telemetry,
        string name,
        double value,
        string model,
        string? outcome = null) {
        var measurement = Assert.Single(telemetry.Measurements, item =>
            item.Name == name
            && item.HasTags(("model", model))
            && (outcome is null || item.HasTags(("outcome", outcome))));

        Assert.Equal(value, measurement.Value);
    }

    private static GeminiChatCompletionClient CreateChatClient(
        HttpMessageHandler handler,
        string model,
        int timeoutSeconds = 120) => new(
            new HttpClient(handler),
            Options.Create(new GeminiSettings {
                BaseUrl = "https://provider.example/v1beta/",
                ApiKey = "test-key",
                ChatModel = model,
                ChatTimeoutSeconds = timeoutSeconds
            }),
            NullLogger<GeminiChatCompletionClient>.Instance);

    private static GeminiEmbeddingClient CreateEmbeddingClient(
        HttpMessageHandler handler,
        string model) => new(
            new HttpClient(handler),
            Options.Create(new GeminiSettings {
                BaseUrl = "https://provider.example/v1beta/",
                ApiKey = "test-key",
                EmbeddingModel = model
            }),
            NullLogger<GeminiEmbeddingClient>.Instance);

    private static async Task ConsumeAsync(
        GeminiChatCompletionClient client,
        CancellationToken cancellationToken) {
        await foreach (var _ in client.StreamAsync(
            [new ChatPromptMessage("user", "question")],
            cancellationToken)) {
        }
    }

    private static HttpResponseMessage Response(
        HttpStatusCode statusCode,
        string body,
        string mediaType = "application/json") => new(statusCode) {
            Content = new StringContent(body, Encoding.UTF8, mediaType)
        };

    private static string VectorJson(float value) =>
        $"[{string.Join(',', Enumerable.Repeat(value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), DocumentEmbeddingConstraints.Dimensions))}]";

    private sealed class SequenceHandler(Func<int, HttpResponseMessage> responseFactory) : HttpMessageHandler {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) {
            RequestCount++;
            return Task.FromResult(responseFactory(RequestCount));
        }
    }

    private sealed class DelayedHandler : HttpMessageHandler {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Response(HttpStatusCode.OK, string.Empty);
        }
    }
}
