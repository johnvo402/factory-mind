using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FactoryMind.Application.Features.Chat;
using FactoryMind.Domain.Chat;
using FactoryMind.Shared.AI;
using FactoryMind.Shared.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactoryMind.Infrastructure.AI;

public sealed class GeminiAiToolPlanner : IAiToolPlanner {
    private const int MaximumPlannerOutputTokens = 1_024;
    private readonly HttpClient _httpClient;
    private readonly GeminiSettings _settings;
    private readonly ILogger<GeminiAiToolPlanner> _logger;

    public GeminiAiToolPlanner(
        HttpClient httpClient,
        IOptions<GeminiSettings> options,
        ILogger<GeminiAiToolPlanner> logger) {
        _httpClient = httpClient;
        _settings = options.Value;
        _logger = logger;
        var baseUrl = _settings.BaseUrl.EndsWith('/') ? _settings.BaseUrl : $"{_settings.BaseUrl}/";
        _httpClient.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
        _httpClient.Timeout = Timeout.InfiniteTimeSpan;
    }

    public async Task<AiToolPlan> PlanAsync(
        IReadOnlyList<ChatPromptMessage> messages,
        IReadOnlyList<AiToolDefinition> tools,
        CancellationToken cancellationToken) {
        EnsureConfigured();
        var systemInstructions = messages
            .Where(message => message.Role == ChatRoles.System)
            .Select(message => message.Content)
            .Where(instruction => !string.IsNullOrWhiteSpace(instruction));
        var contents = messages
            .Where(message => message.Role != ChatRoles.System)
            .Select(message => new GeminiContent(
                message.Role == ChatRoles.Assistant ? "model" : "user",
                [new GeminiTextPart(message.Content)]))
            .ToList();
        var payload = new GeminiToolPlanningRequest(
            new GeminiContent(null, [new GeminiTextPart(string.Join("\n\n", systemInstructions))]),
            contents,
            [new GeminiTool(tools.Select(tool => new GeminiFunctionDeclaration(
                tool.Name,
                tool.Description,
                tool.Parameters)).ToList())],
            new GeminiToolConfig(new GeminiFunctionCallingConfig("AUTO")),
            new GeminiGenerationConfig(MaximumPlannerOutputTokens));
        var endpoint = $"models/{Uri.EscapeDataString(_settings.ChatModel)}:generateContent";
        var startedTimestamp = Stopwatch.GetTimestamp();
        var outcome = GeminiTelemetry.Success;
        GeminiUsageMetadata? usage = null;
        using var activity = FactoryMindTelemetry.ActivitySource.StartActivity(
            "factorymind.ai.tool_plan",
            ActivityKind.Client);
        activity?.SetTag("gen_ai.operation.name", "tool_plan");
        activity?.SetTag("gen_ai.request.model", _settings.ChatModel);
        activity?.SetTag("factorymind.ai.tool_definition_count", tools.Count);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_settings.ToolPlanningTimeoutSeconds));

        try {
            HttpResponseMessage response;
            try {
                response = await GeminiHttpResponse.SendAsync(
                    _httpClient,
                    () => CreateRequest(endpoint, payload),
                    HttpCompletionOption.ResponseContentRead,
                    "tool_plan",
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
                string body;
                try {
                    body = await response.Content.ReadAsStringAsync(timeout.Token);
                } catch (OperationCanceledException exception) {
                    throw GeminiTelemetry.TranslateCancellation(
                        exception,
                        cancellationToken,
                        timeout.Token,
                        out outcome);
                }

                try {
                    using var document = JsonDocument.Parse(body);
                    if (document.RootElement.TryGetProperty("error", out _)) {
                        outcome = GeminiTelemetry.Error;
                        throw new AiProviderException("AI service is temporarily unavailable.");
                    }

                    if (document.RootElement.TryGetProperty("usageMetadata", out var usageElement)) {
                        usage = ReadUsage(usageElement);
                    }

                    var calls = ReadCalls(document.RootElement);
                    activity?.SetTag("factorymind.ai.tool_call_count", calls.Count);
                    return new AiToolPlan(calls);
                } catch (JsonException exception) {
                    outcome = GeminiTelemetry.InvalidResponse;
                    throw new AiProviderException("AI service returned an invalid response.", exception);
                }
            }
        } finally {
            if (usage is not null) {
                GeminiTelemetry.RecordUsage("tool_plan", _settings.ChatModel, usage);
            }

            var tags = FactoryMindTelemetry.Tags(
                ("model", _settings.ChatModel),
                ("outcome", outcome));
            FactoryMindTelemetry.AiToolPlans.Add(1, tags);
            FactoryMindTelemetry.AiToolPlanDuration.Record(
                Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds,
                tags);
            GeminiTelemetry.RecordRequest("tool_plan", _settings.ChatModel, outcome, startedTimestamp);
            activity?.SetTag("factorymind.outcome", outcome);
            if (outcome is not GeminiTelemetry.Success and not GeminiTelemetry.Cancelled) {
                activity?.SetStatus(ActivityStatusCode.Error);
            }
        }
    }

    private HttpRequestMessage CreateRequest(string endpoint, GeminiToolPlanningRequest payload) {
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint) {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("x-goog-api-key", _settings.ApiKey);
        return request;
    }

    private static List<AiToolCall> ReadCalls(JsonElement root) {
        var calls = new List<AiToolCall>();
        if (!root.TryGetProperty("candidates", out var candidates)
            || candidates.ValueKind != JsonValueKind.Array) {
            return calls;
        }

        foreach (var candidate in candidates.EnumerateArray()) {
            if (!candidate.TryGetProperty("content", out var content)
                || !content.TryGetProperty("parts", out var parts)
                || parts.ValueKind != JsonValueKind.Array) {
                continue;
            }

            foreach (var part in parts.EnumerateArray()) {
                if (!part.TryGetProperty("functionCall", out var functionCall)
                    || functionCall.ValueKind != JsonValueKind.Object
                    || !functionCall.TryGetProperty("name", out var nameElement)
                    || nameElement.ValueKind != JsonValueKind.String) {
                    continue;
                }

                var name = nameElement.GetString();
                if (string.IsNullOrWhiteSpace(name)) {
                    continue;
                }

                var arguments = functionCall.TryGetProperty("args", out var argumentsElement)
                    ? argumentsElement.Clone()
                    : EmptyArguments();
                calls.Add(new AiToolCall(name, arguments));
            }
        }

        return calls;
    }

    private static JsonElement EmptyArguments() {
        using var document = JsonDocument.Parse("{}");
        return document.RootElement.Clone();
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

    private sealed record GeminiToolPlanningRequest(
        GeminiContent SystemInstruction,
        IReadOnlyList<GeminiContent> Contents,
        IReadOnlyList<GeminiTool> Tools,
        GeminiToolConfig ToolConfig,
        GeminiGenerationConfig GenerationConfig);

    private sealed record GeminiContent(
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Role,
        IReadOnlyList<GeminiTextPart> Parts);

    private sealed record GeminiTextPart(string Text);
    private sealed record GeminiTool(IReadOnlyList<GeminiFunctionDeclaration> FunctionDeclarations);
    private sealed record GeminiFunctionDeclaration(string Name, string Description, JsonElement Parameters);
    private sealed record GeminiToolConfig(GeminiFunctionCallingConfig FunctionCallingConfig);
    private sealed record GeminiFunctionCallingConfig(string Mode);
    private sealed record GeminiGenerationConfig(int MaxOutputTokens);
}
