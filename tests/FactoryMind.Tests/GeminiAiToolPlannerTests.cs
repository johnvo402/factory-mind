using System.Net;
using System.Text;
using System.Text.Json;
using FactoryMind.Application.Features.Chat;
using FactoryMind.Application.Features.Chat.Rag;
using FactoryMind.Application.Features.Chat.Tools;
using FactoryMind.Infrastructure.AI;
using FactoryMind.Shared.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FactoryMind.Tests;

public sealed class GeminiAiToolPlannerTests {
    [Fact]
    public async Task Planner_uses_native_function_declarations_and_reads_no_calls() {
        var handler = new StubHttpMessageHandler("""{"candidates":[{"content":{"parts":[{"text":"ignored"}]}}]}""");
        var planner = CreatePlanner(handler);

        var plan = await planner.PlanAsync(
            [new ChatPromptMessage("system", "select only"), new ChatPromptMessage("user", "SOP question")],
            [Definition("get_machine_status")],
            CancellationToken.None);

        Assert.Empty(plan.Calls);
        Assert.Equal("/v1beta/models/test-model:generateContent", handler.RequestUri?.PathAndQuery);
        Assert.Contains("\"functionDeclarations\"", handler.RequestBody);
        Assert.Contains("\"functionCallingConfig\":{\"mode\":\"AUTO\"}", handler.RequestBody);
        Assert.DoesNotContain("streamGenerateContent", handler.RequestBody);
    }

    [Fact]
    public async Task Planner_reads_one_native_function_call_and_ignores_text() {
        var handler = new StubHttpMessageHandler("""
            {"candidates":[{"content":{"parts":[
              {"text":"I would query a machine."},
              {"functionCall":{"name":"get_machine_status","args":{"code":"CNC-02"}}}
            ]}}]}
            """);
        var planner = CreatePlanner(handler);

        var plan = await planner.PlanAsync([], [Definition("get_machine_status")], CancellationToken.None);

        var call = Assert.Single(plan.Calls);
        Assert.Equal("get_machine_status", call.Name);
        Assert.Equal("CNC-02", call.Arguments.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Planner_preserves_multiple_unknown_and_malformed_structured_calls_for_server_validation() {
        var handler = new StubHttpMessageHandler("""
            {"candidates":[{"content":{"parts":[
              {"functionCall":{"name":"get_machine_status","args":{"code":123}}},
              {"functionCall":{"name":"delete_machine","args":{"code":"CNC-02"}}},
              {"functionCall":{"name":"get_machine_status","args":{"code":"CNC-02","companyId":"foreign"}}}
            ]}}]}
            """);
        var planner = CreatePlanner(handler);

        var plan = await planner.PlanAsync([], [Definition("get_machine_status")], CancellationToken.None);

        Assert.Equal(3, plan.Calls.Count);
        Assert.Equal(JsonValueKind.Number, plan.Calls[0].Arguments.GetProperty("code").ValueKind);
        Assert.Equal("delete_machine", plan.Calls[1].Name);
        Assert.True(plan.Calls[2].Arguments.TryGetProperty("companyId", out _));
    }

    [Fact]
    public async Task Native_response_with_duplicates_and_more_than_limit_executes_at_most_three_distinct_calls() {
        var handler = new StubHttpMessageHandler("""
            {"candidates":[{"content":{"parts":[
              {"functionCall":{"name":"get_machine_status","args":{"code":"CNC-01"}}},
              {"functionCall":{"name":"get_machine_status","args":{"code":"CNC-01"}}},
              {"functionCall":{"name":"get_machine_status","args":{"code":"CNC-02"}}},
              {"functionCall":{"name":"get_machine_status","args":{"code":"CNC-03"}}},
              {"functionCall":{"name":"get_machine_status","args":{"code":"CNC-04"}}},
              {"functionCall":{"name":"get_machine_status","args":{"code":"CNC-05"}}}
            ]}}]}
            """);
        var registry = new RecordingRegistry();
        var orchestrator = new AiToolOrchestrator(
            new IntentRouter(),
            CreatePlanner(handler),
            registry);

        await orchestrator.CollectAsync(
            Guid.NewGuid(),
            "Máy nào đang chạy?",
            [],
            CancellationToken.None);

        Assert.Equal(["CNC-01", "CNC-02", "CNC-03"], registry.ExecutedCodes);
    }

    [Fact]
    public async Task Planner_records_exact_provider_usage_metadata() {
        using var telemetry = new TelemetryTestListener();
        var handler = new StubHttpMessageHandler("""
            {"candidates":[],"usageMetadata":{"promptTokenCount":17,"candidatesTokenCount":3,"totalTokenCount":20}}
            """);
        var planner = CreatePlanner(handler);

        await planner.PlanAsync([], [Definition("get_machine_status")], CancellationToken.None);

        Assert.Contains(telemetry.Measurements, measurement =>
            measurement.Name == "factorymind.ai.input.tokens"
            && measurement.Value == 17
            && measurement.HasTags(("operation", "tool_plan"), ("model", "test-model")));
        Assert.Contains(telemetry.Measurements, measurement =>
            measurement.Name == "factorymind.ai.output.tokens" && measurement.Value == 3);
        Assert.Contains(telemetry.Measurements, measurement =>
            measurement.Name == "factorymind.ai.total.tokens" && measurement.Value == 20);
    }

    [Fact]
    public async Task Planner_rejects_invalid_provider_json() {
        var planner = CreatePlanner(new StubHttpMessageHandler("not-json"));

        var exception = await Assert.ThrowsAsync<AiProviderException>(() => planner.PlanAsync(
            [],
            [Definition("get_machine_status")],
            CancellationToken.None));

        Assert.Equal("AI service returned an invalid response.", exception.Message);
    }

    [Fact]
    public async Task Planner_timeout_degrades_to_zero_tool_evidence_in_orchestrator() {
        var handler = new StubHttpMessageHandler(
            """{"candidates":[]}""",
            TimeSpan.FromSeconds(5));
        var registry = new RecordingRegistry();
        var orchestrator = new AiToolOrchestrator(
            new IntentRouter(),
            CreatePlanner(handler, timeoutSeconds: 1),
            registry);

        var records = await orchestrator.CollectAsync(
            Guid.NewGuid(),
            "Máy CNC-02 đang chạy gì?",
            [],
            CancellationToken.None);

        Assert.Empty(records);
        Assert.Empty(registry.ExecutedCodes);
    }

    private static AiToolDefinition Definition(string name) {
        using var document = JsonDocument.Parse("""
            {"type":"object","properties":{"code":{"type":"string"}},"required":["code"],"additionalProperties":false}
            """);
        return new AiToolDefinition(name, "Read-only test tool.", document.RootElement.Clone());
    }

    private static GeminiAiToolPlanner CreatePlanner(HttpMessageHandler handler, int timeoutSeconds = 15) => new(
        new HttpClient(handler),
        Options.Create(new GeminiSettings {
            BaseUrl = "https://provider.example/v1beta/",
            ApiKey = "test-key",
            ChatModel = "test-model",
            ToolPlanningTimeoutSeconds = timeoutSeconds
        }),
        NullLogger<GeminiAiToolPlanner>.Instance);

    private sealed class StubHttpMessageHandler(
        string responseBody,
        TimeSpan? delay = null) : HttpMessageHandler {
        public string RequestBody { get; private set; } = string.Empty;
        public Uri? RequestUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) {
            if (delay.HasValue) {
                await Task.Delay(delay.Value, cancellationToken);
            }

            RequestUri = request.RequestUri;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class RecordingRegistry : IManufacturingToolRegistry {
        public IReadOnlyList<AiToolDefinition> Definitions { get; } = [Definition("get_machine_status")];
        public List<string> ExecutedCodes { get; } = [];

        public Task<ToolExecutionResult> ExecuteAsync(
            Guid companyId,
            AiToolCall call,
            CancellationToken cancellationToken) {
            ExecutedCodes.Add(call.Arguments.GetProperty("code").GetString()!);
            return Task.FromResult(new ToolExecutionResult(ToolExecutionStatuses.Success, []));
        }
    }
}
