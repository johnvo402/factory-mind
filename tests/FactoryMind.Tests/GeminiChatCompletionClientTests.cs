using System.Net;
using System.Text;
using FactoryMind.Application.Features.Chat;
using FactoryMind.Infrastructure.AI;
using FactoryMind.Shared.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FactoryMind.Tests;

public sealed class GeminiChatCompletionClientTests {
    [Fact]
    public async Task Stream_reads_content_tokens_from_a_Gemini_SSE_response() {
        const string responseBody = """
            data: {"candidates":[{"content":{"role":"model","parts":[{"text":"Machine"}]}}]}

            data: {"candidates":[{"content":{"role":"model","parts":[{"text":" A"},{"text":"hidden","thought":true}]}}]}

            """;
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, responseBody);
        var client = CreateClient(handler);
        var tokens = new List<string>();

        await foreach (var token in client.StreamAsync(
            [new ChatPromptMessage("user", "Which machine?")],
            CancellationToken.None)) {
            tokens.Add(token);
        }

        Assert.Equal(["Machine", " A"], tokens);
        Assert.Equal(
            "/v1beta/models/gemini-3.5-flash-lite:streamGenerateContent?alt=sse",
            handler.RequestUri?.PathAndQuery);
        Assert.Equal("test-key", handler.ApiKey);
        Assert.Contains("\"systemInstruction\"", handler.RequestBody);
        Assert.Contains("\"role\":\"user\"", handler.RequestBody);
        Assert.DoesNotContain("temperature", handler.RequestBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Quota_failure_throws_a_safe_AI_exception_without_retrying() {
        var handler = new StubHttpMessageHandler(HttpStatusCode.TooManyRequests, "quota");
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<AiProviderException>(async () => {
            await foreach (var _ in client.StreamAsync(
                [new ChatPromptMessage("user", "Question")],
                CancellationToken.None)) {
            }
        });

        Assert.Equal(
            "AI free-tier quota is temporarily exhausted. Please try again later.",
            exception.Message);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Transient_provider_failure_is_retried_once_then_throws_a_safe_exception() {
        var handler = new StubHttpMessageHandler(HttpStatusCode.ServiceUnavailable, "unavailable");
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<AiProviderException>(async () => {
            await foreach (var _ in client.StreamAsync(
                [new ChatPromptMessage("user", "Question")],
                CancellationToken.None)) {
            }
        });

        Assert.Equal("AI service is temporarily unavailable.", exception.Message);
        Assert.Equal(2, handler.RequestCount);
    }

    private static GeminiChatCompletionClient CreateClient(HttpMessageHandler handler) {
        var settings = Options.Create(new GeminiSettings {
            BaseUrl = "https://provider.example/v1beta/",
            ApiKey = "test-key",
            ChatModel = "gemini-3.5-flash-lite"
        });
        return new GeminiChatCompletionClient(
            new HttpClient(handler),
            settings,
            NullLogger<GeminiChatCompletionClient>.Instance);
    }

    private sealed class StubHttpMessageHandler(
        HttpStatusCode statusCode,
        string responseBody) : HttpMessageHandler {
        public string RequestBody { get; private set; } = string.Empty;
        public Uri? RequestUri { get; private set; }
        public string? ApiKey { get; private set; }
        public int RequestCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) {
            RequestCount++;
            RequestUri = request.RequestUri;
            ApiKey = request.Headers.GetValues("x-goog-api-key").Single();
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode) {
                Content = new StringContent(responseBody, Encoding.UTF8, "text/event-stream")
            };
        }
    }
}
