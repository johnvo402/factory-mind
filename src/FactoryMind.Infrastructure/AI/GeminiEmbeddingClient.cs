using System.Diagnostics;
using System.Net.Http.Json;
using FactoryMind.Application.Features.Knowledge;
using FactoryMind.Shared.AI;
using FactoryMind.Shared.Observability;
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
        _httpClient.Timeout = Timeout.InfiniteTimeSpan;
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
        var purposeName = purpose == EmbeddingPurpose.Document ? "document" : "query";
        var startedTimestamp = Stopwatch.GetTimestamp();
        var outcome = GeminiTelemetry.Success;
        using var activity = FactoryMindTelemetry.ActivitySource.StartActivity(
            $"factorymind.ai.embedding.{purposeName}",
            ActivityKind.Client);
        activity?.SetTag("gen_ai.operation.name", "embedding");
        activity?.SetTag("gen_ai.request.model", _settings.EmbeddingModel);
        activity?.SetTag("factorymind.ai.embedding.purpose", purposeName);
        activity?.SetTag("factorymind.ai.embedding.batch_size", inputs.Count);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_settings.EmbeddingTimeoutSeconds));

        _logger.LogInformation(
            "Creating {InputCount} Gemini embeddings with model {Model} for {Purpose}",
            inputs.Count,
            _settings.EmbeddingModel,
            purpose);
        try {
            HttpResponseMessage response;
            try {
                response = await GeminiHttpResponse.SendAsync(
                    _httpClient,
                    () => CreateRequest(endpoint, payload),
                    HttpCompletionOption.ResponseContentRead,
                    "embedding",
                    _settings.EmbeddingModel,
                    _logger,
                    timeout.Token);
            } catch (GeminiTransportException exception) {
                outcome = exception.Outcome;
                throw new AiProviderException(exception.Message, exception);
            } catch (OperationCanceledException exception) {
                throw GeminiTelemetry.TranslateCancellation(
                    exception,
                    cancellationToken,
                    timeout.Token,
                    out outcome);
            }

            using (response) {
                GeminiBatchEmbeddingResponse? result;
                try {
                    result = await response.Content.ReadFromJsonAsync<GeminiBatchEmbeddingResponse>(timeout.Token);
                } catch (OperationCanceledException exception) {
                    throw GeminiTelemetry.TranslateCancellation(
                        exception,
                        cancellationToken,
                        timeout.Token,
                        out outcome);
                } catch (Exception exception) {
                    outcome = GeminiTelemetry.InvalidResponse;
                    throw new AiProviderException("AI service returned an invalid embedding response.", exception);
                }

                if (result?.Embeddings is null || result.Embeddings.Count != inputs.Count) {
                    outcome = GeminiTelemetry.InvalidResponse;
                    throw new AiProviderException("AI service returned an invalid embedding response.");
                }

                var vectors = result.Embeddings.Select(embedding => embedding.Values).ToList();
                if (vectors.Any(vector => vector.Length != DocumentEmbeddingConstraints.Dimensions)) {
                    outcome = GeminiTelemetry.InvalidResponse;
                    throw new AiProviderException("AI service returned an invalid embedding response.");
                }

                if (result.UsageMetadata is not null) {
                    GeminiTelemetry.RecordUsage("embedding", _settings.EmbeddingModel, result.UsageMetadata);
                }

                return new EmbeddingBatch(_settings.EmbeddingModel, vectors);
            }
        } catch when (outcome == GeminiTelemetry.Success) {
            outcome = GeminiTelemetry.Error;
            throw;
        } finally {
            var tags = FactoryMindTelemetry.Tags(
                ("model", _settings.EmbeddingModel),
                ("purpose", purposeName),
                ("outcome", outcome));
            FactoryMindTelemetry.AiEmbeddingBatchSize.Record(inputs.Count, tags);
            GeminiTelemetry.RecordRequest(
                "embedding",
                _settings.EmbeddingModel,
                outcome,
                startedTimestamp);
            activity?.SetTag("factorymind.outcome", outcome);
            if (outcome is not GeminiTelemetry.Success and not GeminiTelemetry.Cancelled) {
                activity?.SetStatus(ActivityStatusCode.Error);
            }

            if (outcome == GeminiTelemetry.Success) {
                _logger.LogInformation(
                    "Gemini embedding request completed for {Purpose} with {InputCount} inputs in {ElapsedMs} ms",
                    purposeName,
                    inputs.Count,
                    Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds);
            } else if (outcome == GeminiTelemetry.Cancelled) {
                _logger.LogDebug("Gemini embedding request was cancelled by the caller");
            } else {
                _logger.LogWarning("Gemini embedding request completed with outcome {Outcome}", outcome);
            }
        }
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

    private sealed record GeminiBatchEmbeddingResponse(
        IReadOnlyList<GeminiEmbedding> Embeddings,
        GeminiUsageMetadata? UsageMetadata);

    private sealed record GeminiEmbedding(float[] Values);
}
