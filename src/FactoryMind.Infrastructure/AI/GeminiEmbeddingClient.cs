using System.Net.Http.Json;
using FactoryMind.Application.Features.Knowledge;
using FactoryMind.Shared.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactoryMind.Infrastructure.AI;

public sealed class GeminiEmbeddingClient : IEmbeddingClient {
    private readonly HttpClient _httpClient;
    private readonly GeminiSettings _settings;
    private readonly ILogger<GeminiEmbeddingClient> _logger;

    public GeminiEmbeddingClient(
        HttpClient httpClient,
        IOptions<GeminiSettings> options,
        ILogger<GeminiEmbeddingClient> logger) {
        _httpClient = httpClient;
        _settings = options.Value;
        _logger = logger;
        var baseUrl = _settings.BaseUrl.EndsWith('/') ? _settings.BaseUrl : $"{_settings.BaseUrl}/";
        _httpClient.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
    }

    public async Task<EmbeddingBatch> CreateAsync(
        IReadOnlyList<string> inputs,
        EmbeddingPurpose purpose,
        CancellationToken cancellationToken) {
        EnsureConfigured();
        if (inputs.Count == 0) {
            return new EmbeddingBatch(_settings.EmbeddingModel, []);
        }

        var modelName = $"models/{_settings.EmbeddingModel}";
        var taskType = purpose == EmbeddingPurpose.Document
            ? "RETRIEVAL_DOCUMENT"
            : "RETRIEVAL_QUERY";
        var payload = new GeminiBatchEmbeddingRequest(inputs
            .Select(input => new GeminiEmbeddingRequest(
                modelName,
                new GeminiEmbeddingContent([new GeminiEmbeddingPart(input)]),
                new GeminiEmbeddingConfig(taskType, DocumentEmbeddingConstraints.Dimensions)))
            .ToList());
        var endpoint = $"models/{Uri.EscapeDataString(_settings.EmbeddingModel)}:batchEmbedContents";

        _logger.LogInformation(
            "Creating {InputCount} Gemini embeddings with model {Model} for {Purpose}",
            inputs.Count,
            _settings.EmbeddingModel,
            purpose);
        using var response = await GeminiHttpResponse.SendAsync(
            _httpClient,
            () => CreateRequest(endpoint, payload),
            HttpCompletionOption.ResponseContentRead,
            _logger,
            cancellationToken);

        GeminiBatchEmbeddingResponse? result;
        try {
            result = await response.Content.ReadFromJsonAsync<GeminiBatchEmbeddingResponse>(cancellationToken);
        } catch (Exception exception) when (exception is not OperationCanceledException) {
            throw new AiProviderException("AI service returned an invalid embedding response.", exception);
        }

        if (result?.Embeddings is null || result.Embeddings.Count != inputs.Count) {
            throw new AiProviderException("AI service returned an invalid embedding response.");
        }

        var vectors = result.Embeddings.Select(embedding => embedding.Values).ToList();
        if (vectors.Any(vector => vector.Length != DocumentEmbeddingConstraints.Dimensions)) {
            throw new AiProviderException("AI service returned an invalid embedding response.");
        }

        return new EmbeddingBatch(_settings.EmbeddingModel, vectors);
    }

    private HttpRequestMessage CreateRequest(string endpoint, GeminiBatchEmbeddingRequest payload) {
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint) {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("x-goog-api-key", _settings.ApiKey);
        return request;
    }

    private void EnsureConfigured() {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey)) {
            throw new AiProviderException("AI API key is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_settings.EmbeddingModel)) {
            throw new AiProviderException("AI embedding model is not configured.");
        }
    }

    private sealed record GeminiBatchEmbeddingRequest(IReadOnlyList<GeminiEmbeddingRequest> Requests);

    private sealed record GeminiEmbeddingRequest(
        string Model,
        GeminiEmbeddingContent Content,
        GeminiEmbeddingConfig EmbedContentConfig);

    private sealed record GeminiEmbeddingContent(IReadOnlyList<GeminiEmbeddingPart> Parts);

    private sealed record GeminiEmbeddingPart(string Text);

    private sealed record GeminiEmbeddingConfig(string TaskType, int OutputDimensionality);

    private sealed record GeminiBatchEmbeddingResponse(IReadOnlyList<GeminiEmbedding> Embeddings);

    private sealed record GeminiEmbedding(float[] Values);
}
