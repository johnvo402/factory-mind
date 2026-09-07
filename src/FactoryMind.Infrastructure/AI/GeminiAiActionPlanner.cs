using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FactoryMind.Application.Features.AiActions;
using FactoryMind.Application.Features.Chat;
using FactoryMind.Domain.Chat;
using FactoryMind.Shared.AI;
using FactoryMind.Shared.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactoryMind.Infrastructure.AI;

public sealed class GeminiAiActionPlanner : IAiActionPlanner {
    private readonly HttpClient _httpClient;
    private readonly GeminiSettings _settings;
    private readonly ILogger<GeminiAiActionPlanner> _logger;

    public GeminiAiActionPlanner(
        HttpClient httpClient,
        IOptions<GeminiSettings> options,
        ILogger<GeminiAiActionPlanner> logger) {
        _httpClient = httpClient;
        _settings = options.Value;
        _logger = logger;
        _httpClient.BaseAddress = new Uri(
            _settings.BaseUrl.EndsWith('/') ? _settings.BaseUrl : $"{_settings.BaseUrl}/");
        _httpClient.Timeout = Timeout.InfiniteTimeSpan;
    }

    public async Task<AiActionPlan> PlanAsync(
        IReadOnlyList<ChatPromptMessage> messages,
        IReadOnlyList<AiActionDefinition> actions,
        CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey) || string.IsNullOrWhiteSpace(_settings.ChatModel)) {
            throw new AiProviderException("AI API key or model is not configured.");
        }

        var contents = messages.Select(message => new GeminiContent(
            message.Role == ChatRoles.Assistant ? "model" : "user",
            [new GeminiTextPart(message.Content)])).ToList();
        var payload = new GeminiActionRequest(
            new GeminiContent(null, [new GeminiTextPart(
                "Select the proposal function only for the user's current, explicit request to release exactly one production order. Never confirm or execute an action.")]),
            contents,
            [new GeminiTool(actions.Select(action => new GeminiFunctionDeclaration(
                action.Name, action.Description, action.Parameters)).ToList())],
            new(new("AUTO")),
            new(512));
        var endpoint = $"models/{Uri.EscapeDataString(_settings.ChatModel)}:generateContent";
        var started = Stopwatch.GetTimestamp();
        var outcome = GeminiTelemetry.Success;
        GeminiUsageMetadata? usage = null;
        using var activity = FactoryMindTelemetry.ActivitySource.StartActivity(
            "factorymind.ai.action.plan", ActivityKind.Client);
        activity?.SetTag("gen_ai.operation.name", "action_plan");
        activity?.SetTag("gen_ai.request.model", _settings.ChatModel);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_settings.ToolPlanningTimeoutSeconds));
        try {
            HttpResponseMessage response;
            try {
                response = await GeminiHttpResponse.SendAsync(
                    _httpClient,
                    () => CreateRequest(endpoint, payload),
                    HttpCompletionOption.ResponseContentRead,
                    "action_plan",
                    _settings.ChatModel,
                    _logger,
                    timeout.Token);
            } catch (GeminiTransportException exception) {
                outcome = exception.Outcome;
                throw new AiProviderException(exception.Message, exception);
            } catch (OperationCanceledException exception) {
                throw GeminiTelemetry.TranslateCancellation(
                    exception, cancellationToken, timeout.Token, out outcome);
            }

            using (response) {
                var body = await response.Content.ReadAsStringAsync(timeout.Token);
                try {
                    using var document = JsonDocument.Parse(body);
                    if (document.RootElement.TryGetProperty("error", out _)) {
                        outcome = GeminiTelemetry.Error;
                        throw new AiProviderException("AI service is temporarily unavailable.");
                    }
                    if (document.RootElement.TryGetProperty("usageMetadata", out var usageElement)) {
                        usage = new(
                            ReadCount(usageElement, "promptTokenCount"),
                            ReadCount(usageElement, "candidatesTokenCount"),
                            ReadCount(usageElement, "totalTokenCount"));
                    }
                    return new AiActionPlan(ReadCalls(document.RootElement));
                } catch (JsonException exception) {
                    outcome = GeminiTelemetry.InvalidResponse;
                    throw new AiProviderException("AI service returned an invalid response.", exception);
                }
            }
        } finally {
            if (usage is not null) {
                GeminiTelemetry.RecordUsage("action_plan", _settings.ChatModel, usage);
            }
            GeminiTelemetry.RecordRequest("action_plan", _settings.ChatModel, outcome, started);
            activity?.SetTag("factorymind.outcome", outcome);
        }
    }

    private HttpRequestMessage CreateRequest(string endpoint, GeminiActionRequest payload) {
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = JsonContent.Create(payload) };
        request.Headers.Add("x-goog-api-key", _settings.ApiKey);
        return request;
    }

    private static List<AiActionCall> ReadCalls(JsonElement root) {
        var calls = new List<AiActionCall>();
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
                if (part.TryGetProperty("functionCall", out var call)
                    && call.ValueKind == JsonValueKind.Object
                    && call.TryGetProperty("name", out var name)
                    && name.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(name.GetString())) {
                    calls.Add(new(name.GetString()!, call.TryGetProperty("args", out var args)
                        ? args.Clone()
                        : EmptyArguments()));
                }
            }
        }
        return calls;
    }

    private static JsonElement EmptyArguments() {
        using var document = JsonDocument.Parse("{}");
        return document.RootElement.Clone();
    }

    private static long? ReadCount(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt64(out var count) && count >= 0
            ? count
            : null;

    private sealed record GeminiActionRequest(
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
