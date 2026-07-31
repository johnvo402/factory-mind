using System.Net;
using System.Text;
using FactoryMind.Application.Features.Knowledge;
using FactoryMind.Infrastructure.AI;
using FactoryMind.Shared.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FactoryMind.Tests;

public sealed class OpenAiEmbeddingClientTests {
    [Fact]
    public async Task Client_orders_vectors_by_provider_index() {
        var first = VectorJson(1f);
        var second = VectorJson(2f);
        var response = $$"""
            {
              "model": "test-embedding-model",
              "data": [
                { "index": 1, "embedding": {{second}} },
                { "index": 0, "embedding": {{first}} }
              ]
            }
            """;
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, response);
        var client = CreateClient(handler);

        var result = await client.CreateAsync(["first", "second"], CancellationToken.None);

        Assert.Equal("test-embedding-model", result.Model);
        Assert.Equal(1f, result.Vectors[0][0]);
        Assert.Equal(2f, result.Vectors[1][0]);
        Assert.Contains("\"dimensions\":1536", handler.RequestBody);
        Assert.Contains("\"input\":[\"first\",\"second\"]", handler.RequestBody);
    }

    [Fact]
    public async Task Client_rejects_vectors_with_the_wrong_dimensions() {
        const string response = """
            { "model": "test-embedding-model", "data": [{ "index": 0, "embedding": [1, 2, 3] }] }
            """;
        var client = CreateClient(new StubHttpMessageHandler(HttpStatusCode.OK, response));

        var exception = await Assert.ThrowsAsync<AiProviderException>(() =>
            client.CreateAsync(["content"], CancellationToken.None));

        Assert.Equal("AI service returned an invalid embedding response.", exception.Message);
    }

    private static OpenAiEmbeddingClient CreateClient(HttpMessageHandler handler) {
        var settings = Options.Create(new OpenAiSettings {
            BaseUrl = "https://provider.example/v1/",
            ApiKey = "test-key",
            EmbeddingModel = "test-embedding-model"
        });
        return new OpenAiEmbeddingClient(
            new HttpClient(handler),
            settings,
            NullLogger<OpenAiEmbeddingClient>.Instance);
    }

    private static string VectorJson(float value) =>
        $"[{string.Join(',', Enumerable.Repeat(value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), DocumentEmbeddingConstraints.Dimensions))}]";

    private sealed class StubHttpMessageHandler(
        HttpStatusCode statusCode,
        string responseBody) : HttpMessageHandler {
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode) {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
