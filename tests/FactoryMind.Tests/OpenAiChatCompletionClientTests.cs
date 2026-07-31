using System.Net;
using System.Text;
using FactoryMind.Application.Features.Chat;
using FactoryMind.Infrastructure.AI;
using FactoryMind.Shared.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FactoryMind.Tests;

public sealed class OpenAiChatCompletionClientTests {
    [Fact]
    public async Task Stream_reads_content_tokens_from_an_OpenAI_compatible_response() {
        const string responseBody = """
            data: {"choices":[{"delta":{"content":"Machine"}}]}

            data: {"choices":[{"delta":{"content":" A"}}]}

            data: [DONE]

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
        Assert.Contains("\"stream\":true", handler.RequestBody);
        Assert.Contains("\"model\":\"test-model\"", handler.RequestBody);
    }

    [Fact]
    public async Task Provider_failure_throws_a_safe_AI_exception() {
        var client = CreateClient(new StubHttpMessageHandler(HttpStatusCode.ServiceUnavailable, "unavailable"));

        var exception = await Assert.ThrowsAsync<AiProviderException>(async () => {
            await foreach (var _ in client.StreamAsync(
                [new ChatPromptMessage("user", "Question")],
                CancellationToken.None)) {
            }
        });

        Assert.Equal("AI service is temporarily unavailable.", exception.Message);
    }

    private static OpenAiChatCompletionClient CreateClient(HttpMessageHandler handler) {
        var settings = Options.Create(new OpenAiSettings {
            BaseUrl = "https://provider.example/v1/",
            ApiKey = "test-key",
            Model = "test-model"
        });
        return new OpenAiChatCompletionClient(
            new HttpClient(handler),
            settings,
            NullLogger<OpenAiChatCompletionClient>.Instance);
    }

    private sealed class StubHttpMessageHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler {
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode) {
                Content = new StringContent(responseBody, Encoding.UTF8, "text/event-stream")
            };
        }
    }
}
