using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using FactoryMind.Application.Features.Chat;
using FactoryMind.Domain.Chat;
using FactoryMind.Shared.AI;
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

        _logger.LogInformation(
            "Starting Gemini chat request using model {Model} with {MessageCount} messages",
            _settings.ChatModel,
            contents.Count);
        using var response = await GeminiHttpResponse.SendAsync(
            _httpClient,
            () => CreateRequest(endpoint, payload),
            HttpCompletionOption.ResponseHeadersRead,
            _logger,
            cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (await reader.ReadLineAsync(cancellationToken) is { } line) {
            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            var data = line[5..].Trim();
            if (data.Length == 0) {
                continue;
            }

            var content = ReadContent(data);
            if (!string.IsNullOrEmpty(content)) {
                yield return content;
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

    private static string? ReadContent(string data) {
        try {
            using var document = JsonDocument.Parse(data);
            if (document.RootElement.TryGetProperty("error", out _)) {
                throw new AiProviderException("AI service is temporarily unavailable.");
            }

            if (!document.RootElement.TryGetProperty("candidates", out var candidates)
                || candidates.ValueKind != JsonValueKind.Array
                || candidates.GetArrayLength() == 0
                || !candidates[0].TryGetProperty("content", out var content)
                || !content.TryGetProperty("parts", out var parts)
                || parts.ValueKind != JsonValueKind.Array) {
                return null;
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

            return tokens.Count == 0 ? null : string.Concat(tokens);
        } catch (JsonException exception) {
            throw new AiProviderException("AI service returned an invalid response.", exception);
        }
    }

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
}
