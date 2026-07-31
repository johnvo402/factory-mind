using System.Net.Http.Headers;
using System.Net.Http.Json;
using FactoryMind.Application.Features.Knowledge;
using FactoryMind.Shared.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactoryMind.Infrastructure.AI;

public sealed class OpenAiEmbeddingClient : IEmbeddingClient {
    private readonly HttpClient _httpClient;
    private readonly OpenAiSettings _settings;
    private readonly ILogger<OpenAiEmbeddingClient> _logger;

    public OpenAiEmbeddingClient(
        HttpClient httpClient,
        IOptions<OpenAiSettings> options,
        ILogger<OpenAiEmbeddingClient> logger) {
        _httpClient = httpClient;
        _settings = options.Value;
        _logger = logger;
        var baseUrl = _settings.BaseUrl.EndsWith('/') ? _settings.BaseUrl : $"{_settings.BaseUrl}/";
        _httpClient.BaseAddress = new Uri(baseUrl, UriKind.Absolute);

        if (!string.IsNullOrWhiteSpace(_settings.ApiKey)) {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                _settings.ApiKey);
        }
    }

    public async Task<EmbeddingBatch> CreateAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken) {
        EnsureConfigured();
        if (inputs.Count == 0) {
            return new EmbeddingBatch(_settings.EmbeddingModel, []);
        }

        _logger.LogInformation(
            "Creating {InputCount} embeddings with model {Model}",
            inputs.Count,
            _settings.EmbeddingModel);
        HttpResponseMessage response;
        try {
            response = await _httpClient.PostAsJsonAsync(
                "embeddings",
                new EmbeddingRequest(
                    _settings.EmbeddingModel,
                    inputs,
                    DocumentEmbeddingConstraints.Dimensions),
                cancellationToken);
        } catch (HttpRequestException exception) {
            throw new AiProviderException("AI service is temporarily unavailable.", exception);
        }

        using (response) {
            if (!response.IsSuccessStatusCode) {
                _logger.LogWarning(
                    "Embedding provider returned status code {StatusCode}",
                    (int)response.StatusCode);
                throw new AiProviderException("AI service is temporarily unavailable.");
            }

            EmbeddingResponse? payload;
            try {
                payload = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken);
            } catch (Exception exception) when (exception is not OperationCanceledException) {
                throw new AiProviderException("AI service returned an invalid embedding response.", exception);
            }

            if (payload?.Data is null || payload.Data.Count != inputs.Count) {
                throw new AiProviderException("AI service returned an invalid embedding response.");
            }

            var ordered = payload.Data.OrderBy(item => item.Index).ToList();
            for (var index = 0; index < ordered.Count; index++) {
                if (ordered[index].Index != index
                    || ordered[index].Embedding.Length != DocumentEmbeddingConstraints.Dimensions) {
                    throw new AiProviderException("AI service returned an invalid embedding response.");
                }
            }

            return new EmbeddingBatch(
                string.IsNullOrWhiteSpace(payload.Model) ? _settings.EmbeddingModel : payload.Model,
                ordered.Select(item => item.Embedding).ToList());
        }
    }

    private void EnsureConfigured() {
        if (string.IsNullOrWhiteSpace(_settings.EmbeddingModel)
            || _settings.EmbeddingModel == "your-embedding-model-name") {
            throw new AiProviderException("AI embedding model is not configured.");
        }
    }

    private sealed record EmbeddingRequest(
        string Model,
        IReadOnlyList<string> Input,
        int Dimensions);

    private sealed record EmbeddingResponse(
        string? Model,
        IReadOnlyList<EmbeddingData> Data);

    private sealed record EmbeddingData(int Index, float[] Embedding);
}
