using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using FactoryMind.Application.Features.Chat;
using FactoryMind.Domain.Chat;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactoryMind.Infrastructure.AI;

public sealed class OpenAiChatCompletionClient : IChatCompletionClient {
    private readonly HttpClient _httpClient;
    private readonly OpenAiSettings _settings;
    private readonly ILogger<OpenAiChatCompletionClient> _logger;

    public OpenAiChatCompletionClient(
        HttpClient httpClient,
        IOptions<OpenAiSettings> options,
        ILogger<OpenAiChatCompletionClient> logger) {
        _httpClient = httpClient;
        _settings = options.Value;
        _logger = logger;
        var baseUrl = _settings.BaseUrl.EndsWith('/') ? _settings.BaseUrl : $"{_settings.BaseUrl}/";
        _httpClient.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
        _httpClient.Timeout = Timeout.InfiniteTimeSpan;

        if (!string.IsNullOrWhiteSpace(_settings.ApiKey)) {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        }
    }

    public async IAsyncEnumerable<string> StreamAsync(
        IReadOnlyList<ChatPromptMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken) {
        EnsureConfigured();

        var providerMessages = messages
            .Select(message => new ChatCompletionMessage(message.Role, message.Content))
            .Prepend(new ChatCompletionMessage(ChatRoles.System, _settings.SystemPrompt))
            .ToList();
        var payload = new ChatCompletionRequest(
            _settings.Model,
            providerMessages,
            Stream: true,
            _settings.Temperature);
        _logger.LogInformation(
            "Starting AI chat request using model {Model} with {MessageCount} messages",
            _settings.Model,
            providerMessages.Count);
        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions") {
            Content = JsonContent.Create(payload)
        };
        using var response = await SendAsync(request, cancellationToken);
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

            if (data == "[DONE]") {
                yield break;
            }

            var content = ReadContent(data);
            if (!string.IsNullOrEmpty(content)) {
                yield return content;
            }
        }
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) {
        HttpResponseMessage response;

        try {
            response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        } catch (HttpRequestException exception) {
            throw new AiProviderException("AI service is temporarily unavailable.", exception);
        }

        if (response.IsSuccessStatusCode) {
            return response;
        }

        _logger.LogWarning("AI provider returned status code {StatusCode}", (int)response.StatusCode);
        response.Dispose();
        throw new AiProviderException("AI service is temporarily unavailable.");
    }

    private static string? ReadContent(string data) {
        try {
            using var document = JsonDocument.Parse(data);
            if (document.RootElement.TryGetProperty("error", out _)) {
                throw new AiProviderException("AI service is temporarily unavailable.");
            }

            if (!document.RootElement.TryGetProperty("choices", out var choices)
                || choices.ValueKind != JsonValueKind.Array
                || choices.GetArrayLength() == 0
                || !choices[0].TryGetProperty("delta", out var delta)
                || !delta.TryGetProperty("content", out var content)) {
                return null;
            }

            return content.ValueKind is JsonValueKind.String or JsonValueKind.Null
                ? content.GetString()
                : null;
        } catch (JsonException exception) {
            throw new AiProviderException("AI service returned an invalid response.", exception);
        }
    }

    private void EnsureConfigured() {
        if (string.IsNullOrWhiteSpace(_settings.Model) || _settings.Model == "your-model-name") {
            throw new AiProviderException("AI model is not configured.");
        }
    }

    private sealed record ChatCompletionRequest(
        string Model,
        IReadOnlyList<ChatCompletionMessage> Messages,
        bool Stream,
        double Temperature);

    private sealed record ChatCompletionMessage(string Role, string Content);
}
