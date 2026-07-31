using System.Net;
using System.Text;
using FactoryMind.Application.Features.Knowledge;
using FactoryMind.Infrastructure.AI;
using FactoryMind.Shared.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FactoryMind.Tests;

public sealed class GeminiEmbeddingClientTests {
    [Fact]
    public async Task Client_preserves_vector_order_and_sends_document_task_type() {
        var first = VectorJson(1f);
        var second = VectorJson(2f);
        var response = $$"""
            {
              "embeddings": [
                { "values": {{first}} },
                { "values": {{second}} }
              ]
            }
            """;
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, response);
        var client = CreateClient(handler);

        var result = await client.CreateAsync(
            ["first", "second"],
            EmbeddingPurpose.Document,
            CancellationToken.None);

        Assert.Equal("gemini-embedding-2", result.Model);
        Assert.Equal(1f, result.Vectors[0][0]);
        Assert.Equal(2f, result.Vectors[1][0]);
        Assert.Equal(
            "/v1beta/models/gemini-embedding-2:batchEmbedContents",
            handler.RequestUri?.AbsolutePath);
        Assert.Equal("test-key", handler.ApiKey);
        Assert.Contains("\"taskType\":\"RETRIEVAL_DOCUMENT\"", handler.RequestBody);
        Assert.Contains("\"outputDimensionality\":1536", handler.RequestBody);
    }

    [Fact]
    public async Task Client_sends_query_task_type_for_search_embeddings() {
        var response = $$"""{ "embeddings": [{ "values": {{VectorJson(1f)}} }] }""";
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, response);
        var client = CreateClient(handler);

        await client.CreateAsync(
            ["question"],
            EmbeddingPurpose.Query,
            CancellationToken.None);

        Assert.Contains("\"taskType\":\"RETRIEVAL_QUERY\"", handler.RequestBody);
    }

    [Fact]
    public async Task Client_rejects_vectors_with_the_wrong_dimensions() {
        const string response = """
            { "embeddings": [{ "values": [1, 2, 3] }] }
            """;
        var client = CreateClient(new StubHttpMessageHandler(HttpStatusCode.OK, response));

        var exception = await Assert.ThrowsAsync<AiProviderException>(() =>
            client.CreateAsync(
                ["content"],
                EmbeddingPurpose.Document,
                CancellationToken.None));

        Assert.Equal("AI service returned an invalid embedding response.", exception.Message);
    }

    private static GeminiEmbeddingClient CreateClient(HttpMessageHandler handler) {
        var settings = Options.Create(new GeminiSettings {
            BaseUrl = "https://provider.example/v1beta/",
            ApiKey = "test-key",
            EmbeddingModel = "gemini-embedding-2"
        });
        return new GeminiEmbeddingClient(
            new HttpClient(handler),
            settings,
            NullLogger<GeminiEmbeddingClient>.Instance);
    }

    private static string VectorJson(float value) =>
        $"[{string.Join(',', Enumerable.Repeat(value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), DocumentEmbeddingConstraints.Dimensions))}]";

    private sealed class StubHttpMessageHandler(
        HttpStatusCode statusCode,
        string responseBody) : HttpMessageHandler {
        public string RequestBody { get; private set; } = string.Empty;
        public Uri? RequestUri { get; private set; }
        public string? ApiKey { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) {
            RequestUri = request.RequestUri;
            ApiKey = request.Headers.GetValues("x-goog-api-key").Single();
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode) {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
