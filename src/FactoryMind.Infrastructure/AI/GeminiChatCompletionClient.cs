using System.Diagnostics;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using FactoryMind.Application.Features.Chat;
using FactoryMind.Domain.Chat;
using FactoryMind.Shared.AI;
using FactoryMind.Shared.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactoryMind.Infrastructure.AI;

public sealed class GeminiChatCompletionClient : IChatCompletionClient {
    private readonly HttpClient _httpClient;
    private readonly GeminiSettings _settings;
    private readonly ILogger<GeminiChatCompletionClient> _logger;

    public GeminiChatCompletionClient(
        HttpClient httpClient,
        IOptions<GeminiSettings> options,
        ILogger<GeminiChatCompletionClient> logger) {
        _httpClient = httpClient;
        _settings = options.Value;
        _logger = logger;
        var baseUrl = _settings.BaseUrl.EndsWith('/') ? _settings.BaseUrl : $"{_settings.BaseUrl}/";
        _httpClient.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
        _httpClient.Timeout = Timeout.InfiniteTimeSpan;
    }

    public async IAsyncEnumerable<string> StreamAsync(
        IReadOnlyList<ChatPromptMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken) {
        EnsureConfigured();
        var systemInstructions = messages
            .Where(message => message.Role == ChatRoles.System)
            .Select(message => message.Content)
            .Prepend(_settings.SystemPrompt)
            .Where(instruction => !string.IsNullOrWhiteSpace(instruction));
        var contents = messages
            .Where(message => message.Role != ChatRoles.System)
            .Select(message => new GeminiContent(
                message.Role == ChatRoles.Assistant ? "model" : "user",
                [new GeminiPart(message.Content)]))
            .ToList();
        var payload = new GeminiGenerateContentRequest(
            new GeminiContent(null, [new GeminiPart(string.Join("\n\n", systemInstructions))]),
            contents,
            new GeminiGenerationConfig(_settings.MaximumOutputTokens));
        var endpoint = $"models/{Uri.EscapeDataString(_settings.ChatModel)}:streamGenerateContent?alt=sse";
        var startedTimestamp = Stopwatch.GetTimestamp();
        var streamStartedTimestamp = 0L;
        var outcome = GeminiTelemetry.Success;
        var generatedChunks = 0L;
        var completed = false;
        var firstTokenRecorded = false;
        GeminiUsageMetadata? usage = null;
        using var activity = FactoryMindTelemetry.ActivitySource.StartActivity(
            "factorymind.ai.chat",
            ActivityKind.Client);
        activity?.SetTag("gen_ai.operation.name", "chat");
        activity?.SetTag("gen_ai.request.model", _settings.ChatModel);
        activity?.SetTag("factorymind.ai.message_count", contents.Count);
        activity?.SetTag("factorymind.ai.input_character_count", messages.Sum(message => message.Content.Length));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_settings.ChatTimeoutSeconds));

        _logger.LogInformation(
            "Starting Gemini chat request using model {Model} with {MessageCount} messages and {InputCharacterCount} input characters",
            _settings.ChatModel,
            contents.Count,
            messages.Sum(message => message.Content.Length));
        try {
            HttpResponseMessage response;
            try {
                response = await GeminiHttpResponse.SendAsync(
                    _httpClient,
                    () => CreateRequest(endpoint, payload),
                    HttpCompletionOption.ResponseHeadersRead,
                    "chat",
                    _settings.ChatModel,
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
                var responseHeadersMs = Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds;
                FactoryMindTelemetry.AiChatResponseHeadersDuration.Record(
                    responseHeadersMs,
                    FactoryMindTelemetry.Tags(("model", _settings.ChatModel)));
                streamStartedTimestamp = Stopwatch.GetTimestamp();
                Stream stream;
                try {
                    stream = await response.Content.ReadAsStreamAsync(timeout.Token);
                } catch (OperationCanceledException exception) {
                    throw GeminiTelemetry.TranslateCancellation(
                        exception,
                        cancellationToken,
                        timeout.Token,
                        out outcome);
                } catch (Exception exception) {
                    outcome = GeminiTelemetry.Error;
                    throw new AiProviderException(
                        "AI service response stream failed.",
                        exception);
                }

                await using (stream) {
                    using var reader = new StreamReader(stream);
                    while (true) {
                        string? line;
                        try {
                            line = await reader.ReadLineAsync(timeout.Token);
                        } catch (OperationCanceledException exception) {
                            throw GeminiTelemetry.TranslateCancellation(
                                exception,
                                cancellationToken,
                                timeout.Token,
                                out outcome);
                        } catch (Exception exception) {
                            outcome = GeminiTelemetry.Error;
                            throw new AiProviderException(
                                "AI service response stream failed.",
                                exception);
                        }

                        if (line is null) {
                            break;
                        }

                        if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) {
                            continue;
                        }

                        var data = line[5..].Trim();
                        if (data.Length == 0) {
                            continue;
                        }

                        GeminiStreamEvent streamEvent;
                        try {
                            streamEvent = ReadEvent(data);
                        } catch (GeminiTransportException exception) {
                            outcome = exception.Outcome;
                            throw new AiProviderException(exception.Message, exception);
                        } catch (AiProviderException) {
                            outcome = GeminiTelemetry.InvalidResponse;
                            throw;
                        }

                        usage = streamEvent.Usage ?? usage;
                        if (!string.IsNullOrEmpty(streamEvent.Content)) {
                            if (!firstTokenRecorded) {
                                FactoryMindTelemetry.AiChatTimeToFirstToken.Record(
                                    Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds,
                                    FactoryMindTelemetry.Tags(("model", _settings.ChatModel)));
                                firstTokenRecorded = true;
                            }

                            generatedChunks++;
                            yield return streamEvent.Content;
                        }
                    }
                }
            }

            completed = true;
        } finally {
            if (!completed && outcome == GeminiTelemetry.Success) {
                outcome = GeminiTelemetry.Cancelled;
            }

            if (usage is not null) {
                GeminiTelemetry.RecordUsage("chat", _settings.ChatModel, usage);
            }

            var tags = FactoryMindTelemetry.Tags(
                ("model", _settings.ChatModel),
                ("outcome", outcome));
            if (streamStartedTimestamp > 0) {
                FactoryMindTelemetry.AiChatStreamDuration.Record(
                    Stopwatch.GetElapsedTime(streamStartedTimestamp).TotalMilliseconds,
                    tags);
            }

            FactoryMindTelemetry.AiChatGeneratedChunks.Record(generatedChunks, tags);
            GeminiTelemetry.RecordRequest("chat", _settings.ChatModel, outcome, startedTimestamp);
            activity?.SetTag("factorymind.outcome", outcome);
            activity?.SetTag("factorymind.ai.generated_chunk_count", generatedChunks);
            if (outcome is not GeminiTelemetry.Success and not GeminiTelemetry.Cancelled) {
                activity?.SetStatus(ActivityStatusCode.Error);
            }

            if (outcome == GeminiTelemetry.Success) {
                _logger.LogInformation(
                    "Gemini chat completed with {GeneratedChunkCount} chunks in {ElapsedMs} ms",
                    generatedChunks,
                    Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds);
            } else if (outcome == GeminiTelemetry.Cancelled) {
                _logger.LogDebug("Gemini chat was cancelled by the caller");
            } else {
                _logger.LogWarning("Gemini chat completed with outcome {Outcome}", outcome);
            }
        }
    }

    private HttpRequestMessage CreateRequest(string endpoint, GeminiGenerateContentRequest payload) {
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint) {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("x-goog-api-key", _settings.ApiKey);
        return request;
    }

    private static GeminiStreamEvent ReadEvent(string data) {
        try {
            using var document = JsonDocument.Parse(data);
            if (document.RootElement.TryGetProperty("error", out _)) {
                throw new GeminiTransportException(
                    "AI service is temporarily unavailable.",
                    GeminiTelemetry.Error);
            }

            var usage = document.RootElement.TryGetProperty("usageMetadata", out var usageElement)
                ? ReadUsage(usageElement)
                : null;

            if (!document.RootElement.TryGetProperty("candidates", out var candidates)
                || candidates.ValueKind != JsonValueKind.Array
                || candidates.GetArrayLength() == 0
                || !candidates[0].TryGetProperty("content", out var content)
                || !content.TryGetProperty("parts", out var parts)
                || parts.ValueKind != JsonValueKind.Array) {
                return new GeminiStreamEvent(null, usage);
            }

            var tokens = new List<string>();
            foreach (var part in parts.EnumerateArray()) {
                var isThought = part.TryGetProperty("thought", out var thought)
                    && thought.ValueKind == JsonValueKind.True;
                if (!isThought
                    && part.TryGetProperty("text", out var text)
                    && text.ValueKind == JsonValueKind.String) {
                    tokens.Add(text.GetString()!);
                }
            }

            return new GeminiStreamEvent(
                tokens.Count == 0 ? null : string.Concat(tokens),
                usage);
        } catch (JsonException exception) {
            throw new AiProviderException("AI service returned an invalid response.", exception);
        }
    }

    private static GeminiUsageMetadata ReadUsage(JsonElement element) => new(
        ReadTokenCount(element, "promptTokenCount"),
        ReadTokenCount(element, "candidatesTokenCount"),
        ReadTokenCount(element, "totalTokenCount"));

    private static long? ReadTokenCount(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value)
        && value.TryGetInt64(out var count)
        && count >= 0
            ? count
            : null;

    private void EnsureConfigured() {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey)) {
            throw new AiProviderException("AI API key is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_settings.ChatModel)) {
            throw new AiProviderException("AI model is not configured.");
        }
    }

    private sealed record GeminiGenerateContentRequest(
        GeminiContent SystemInstruction,
        IReadOnlyList<GeminiContent> Contents,
        GeminiGenerationConfig GenerationConfig);

    private sealed record GeminiContent(
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Role,
        IReadOnlyList<GeminiPart> Parts);

    private sealed record GeminiPart(string Text);

    private sealed record GeminiGenerationConfig(int MaxOutputTokens);

    private sealed record GeminiStreamEvent(string? Content, GeminiUsageMetadata? Usage);
}
