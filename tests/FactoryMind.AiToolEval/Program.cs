using System.Text;
using System.Text.Json;
using FactoryMind.AiToolEval;
using FactoryMind.Application.Features.Boms;
using FactoryMind.Application.Features.Chat;
using FactoryMind.Application.Features.Chat.Rag;
using FactoryMind.Application.Features.Chat.Tools;
using FactoryMind.Application.Features.ProductionOrders;
using FactoryMind.Application.Features.WorkCenters;
using FactoryMind.Infrastructure.AI;
using FactoryMind.Infrastructure.Persistence;
using FactoryMind.Infrastructure.Persistence.ProductionOrders;
using Microsoft.EntityFrameworkCore;

var datasetPath = Path.Combine(AppContext.BaseDirectory, "ai-tool-eval-cases.json");
var cases = JsonSerializer.Deserialize<List<ToolEvalCase>>(
    await File.ReadAllTextAsync(datasetPath),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    ?? throw new InvalidOperationException("AI tool evaluation dataset is empty.");
if (cases.Count < 40) {
    throw new InvalidOperationException("AI tool evaluation requires at least 40 cases.");
}

await using var dbContext = new FactoryMindDbContext(
    new DbContextOptionsBuilder<FactoryMindDbContext>()
        .UseNpgsql("Host=localhost;Database=ai_tool_eval_unused")
        .Options);
var riskCalculator = new ProductionOrderDeliveryRiskCalculator(
    TimeProvider.System,
    new PlanningSettings());
var scheduleRepository = new EfSchedulePreviewRepository(dbContext, riskCalculator);
var schedulePreviewer = new DeterministicProductionSchedulePreviewer(new WorkCenterCalendarService());
var productionRegistry = new ManufacturingToolRegistry(
    new GetProductionOrderStatusTool(dbContext, riskCalculator),
    new GetMachineStatusTool(dbContext),
    new ListMachinesTool(dbContext),
    new GetWorkCenterStatusTool(dbContext),
    new GetMaterialInventoryTool(dbContext),
    new GetProductionOrderMaterialReadinessTool(dbContext, new MaterialRequirementCalculator()),
    new ListProductionOrdersTool(dbContext, riskCalculator),
    new GetProductionOrderSchedulePreviewTool(
        dbContext, scheduleRepository, schedulePreviewer, riskCalculator, TimeProvider.System),
    new GetWorkCenterCapacityPreviewTool(
        dbContext, scheduleRepository, schedulePreviewer, riskCalculator, TimeProvider.System));
var router = new IntentRouter();
var planner = new DeterministicManufacturingPlanner();
var failures = new List<string>();
var minimumCategoryCounts = new Dictionary<string, int>(StringComparer.Ordinal) {
    ["exact_entity"] = 10,
    ["list_filter"] = 10,
    ["material_readiness"] = 8,
    ["hybrid_multi_tool"] = 6,
    ["adversarial_no_tool"] = 6,
    ["unknown_insufficient_data"] = 6,
    ["delivery_risk"] = 10,
    ["priority_planning"] = 5,
    ["unsupported_scheduling"] = 5,
    ["schedule_preview"] = 10,
    ["capacity_preview"] = 8,
    ["planning_safety"] = 6
};
var categoryCounts = cases.GroupBy(evalCase => evalCase.Category, StringComparer.Ordinal)
    .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
foreach (var requirement in minimumCategoryCounts) {
    if (!categoryCounts.TryGetValue(requirement.Key, out var count) || count < requirement.Value) {
        failures.Add($"dataset: category {requirement.Key} has {count} cases, requires {requirement.Value}");
    }
}

var approvedToolNames = new[] {
    "get_production_order_status",
    "get_machine_status",
    "list_machines",
    "get_work_center_status",
    "get_material_inventory",
    "get_production_order_material_readiness",
    "list_production_orders",
    "get_production_order_schedule_preview",
    "get_work_center_capacity_preview"
};
if (!productionRegistry.Definitions.Select(definition => definition.Name).SequenceEqual(approvedToolNames)) {
    failures.Add("registry: production allowlist differs from the nine approved read-only tools");
}

foreach (var definition in productionRegistry.Definitions) {
    if (!definition.Description.Contains("read-only", StringComparison.OrdinalIgnoreCase)
        || definition.Parameters.GetProperty("additionalProperties").GetBoolean()
        || definition.Parameters.GetRawText().Contains("companyId", StringComparison.OrdinalIgnoreCase)
        || definition.Parameters.GetRawText().Contains("tenantId", StringComparison.OrdinalIgnoreCase)) {
        failures.Add($"registry: unsafe declaration for {definition.Name}");
    }
}

var toolSelectionHits = 0;
var exactPlanHits = 0;
var noToolCases = 0;
var noToolHits = 0;
var predictedNoTool = 0;
var correctPredictedNoTool = 0;
var argumentHits = 0;
var argumentChecks = 0;
var identifierCases = 0;
var identifierHits = 0;
var correctCalls = 0;
var actualCallCount = 0;
var expectedCallCount = 0;
var duplicateCalls = 0;
var boundedCaseHits = 0;

foreach (var evalCase in cases) {
    var route = router.Route(evalCase.Question);
    AiToolPlan plan;
    if (!AiToolOrchestrator.IsEligible(route)) {
        plan = new AiToolPlan([]);
    } else {
        var messages = evalCase.History.ToList();
        messages.Add(new ChatPromptMessage("user", evalCase.Question));
        plan = await planner.PlanAsync(messages, productionRegistry.Definitions, CancellationToken.None);
    }

    var actualCalls = plan.Calls.ToList();
    var expectedCalls = evalCase.ExpectedCalls
        .Select(call => new ComparableCall(call.Name, Canonicalize(call.Arguments)))
        .ToList();
    var comparableActual = actualCalls
        .Select(call => new ComparableCall(call.Name, Canonicalize(call.Arguments)))
        .ToList();
    var distinctActual = comparableActual.Distinct().ToList();
    duplicateCalls += comparableActual.Count - distinctActual.Count;
    actualCallCount += comparableActual.Count;
    expectedCallCount += expectedCalls.Count;

    var actualNames = comparableActual.Select(call => call.Name).ToHashSet(StringComparer.Ordinal);
    var expectedNames = expectedCalls.Select(call => call.Name).ToHashSet(StringComparer.Ordinal);
    if (actualNames.SetEquals(expectedNames)) {
        toolSelectionHits++;
    } else {
        failures.Add($"{evalCase.Id}: tools [{string.Join(", ", actualNames)}], expected [{string.Join(", ", expectedNames)}]");
    }

    if (MultisetEquals(comparableActual, expectedCalls)) {
        exactPlanHits++;
    } else {
        failures.Add($"{evalCase.Id}: exact tool+arguments mismatch");
    }

    if (expectedCalls.Count == 0) {
        noToolCases++;
        if (actualCalls.Count == 0) {
            noToolHits++;
        }
    }

    if (actualCalls.Count == 0) {
        predictedNoTool++;
        if (expectedCalls.Count == 0) {
            correctPredictedNoTool++;
        }
    }

    foreach (var expectedCall in expectedCalls) {
        argumentChecks++;
        if (comparableActual.Contains(expectedCall)) {
            argumentHits++;
            correctCalls++;
        }
    }

    if (evalCase.ExactIdentifier) {
        identifierCases++;
        if (MultisetEquals(comparableActual, expectedCalls)) {
            identifierHits++;
        } else {
            failures.Add($"{evalCase.Id}: exact identifier was not preserved");
        }
    }

    if (actualCalls.Count <= AiToolOrchestrator.MaximumToolCallsPerRequest
        && actualCalls.All(call => HasBoundedLimit(call.Arguments))) {
        boundedCaseHits++;
    } else {
        failures.Add($"{evalCase.Id}: plan exceeded call or list limit");
    }
}

var securityProbes = new[] {
    Probe("unknown-delete", "delete_machine", """{"code":"CNC-02"}""", ToolExecutionStatuses.UnknownTool),
    Probe("unknown-sql", "execute_sql", """{"query":"SELECT * FROM users"}""", ToolExecutionStatuses.UnknownTool),
    Probe("unknown-update", "update_machine", """{"code":"CNC-02"}""", ToolExecutionStatuses.UnknownTool),
    Probe("unknown-internal", "invoke_internal_method", "{}", ToolExecutionStatuses.UnknownTool),
    Probe("company-identity", "get_machine_status", """{"code":"CNC-02","companyId":"foreign"}""", ToolExecutionStatuses.InvalidArguments),
    Probe("tenant-identity", "get_machine_status", """{"code":"CNC-02","tenantId":"foreign"}""", ToolExecutionStatuses.InvalidArguments),
    Probe("wrong-type", "get_machine_status", """{"code":123}""", ToolExecutionStatuses.InvalidArguments),
    Probe("negative-limit", "list_machines", """{"limit":-1}""", ToolExecutionStatuses.InvalidArguments),
    Probe("zero-limit", "list_production_orders", """{"limit":0}""", ToolExecutionStatuses.InvalidArguments),
    Probe("oversized-limit", "list_machines", """{"limit":999999}""", ToolExecutionStatuses.InvalidArguments)
};
var unauthorizedHits = 0;
foreach (var probe in securityProbes) {
    try {
        var result = await productionRegistry.ExecuteAsync(
            Guid.NewGuid(),
            new AiToolCall(probe.Name, probe.Arguments),
            CancellationToken.None);
        if (result.Status == probe.ExpectedStatus && result.Records.Count == 0) {
            unauthorizedHits++;
        } else {
            failures.Add($"{probe.Id}: status {result.Status}, expected {probe.ExpectedStatus}");
        }
    } catch (Exception exception) {
        failures.Add($"{probe.Id}: unsafe arguments reached data access ({exception.GetType().Name})");
    }
}

var maximumRecordingRegistry = new RecordingRegistry(productionRegistry.Definitions);
var maximumCalls = Enumerable.Range(1, 5)
    .Select(index => Call("get_machine_status", $$"""{"code":"CNC-{{index:00}}"}"""))
    .ToList();
await new AiToolOrchestrator(
    router,
    new FixedPlanner(maximumCalls),
    maximumRecordingRegistry).CollectAsync(
        Guid.NewGuid(),
        "Máy nào đang chạy?",
        [],
        CancellationToken.None);
var maximumBounded = maximumRecordingRegistry.Calls.Count == AiToolOrchestrator.MaximumToolCallsPerRequest;

var duplicateRecordingRegistry = new RecordingRegistry(productionRegistry.Definitions);
var duplicate = Call("get_machine_status", """{"code":"CNC-02"}""");
await new AiToolOrchestrator(
    router,
    new FixedPlanner([duplicate, duplicate]),
    duplicateRecordingRegistry).CollectAsync(
        Guid.NewGuid(),
        "Máy CNC-02 đang chạy gì?",
        [],
        CancellationToken.None);
var duplicateBounded = duplicateRecordingRegistry.Calls.Count == 1;
if (!maximumBounded) {
    failures.Add("server-cap: more than three calls reached execution");
}

if (!duplicateBounded) {
    failures.Add("server-deduplication: duplicate calls reached execution");
}

var boundedChecks = cases.Count + 2;
var boundedHits = boundedCaseHits + (maximumBounded ? 1 : 0) + (duplicateBounded ? 1 : 0);
var metrics = new {
    DatasetSize = cases.Count,
    ToolSelectionAccuracy = Ratio(toolSelectionHits, cases.Count),
    ExactToolArgumentsAccuracy = Ratio(exactPlanHits, cases.Count),
    NoToolAccuracy = Ratio(noToolHits, noToolCases),
    NoToolPrecision = Ratio(correctPredictedNoTool, predictedNoTool),
    ArgumentAccuracy = Ratio(argumentHits, argumentChecks),
    ExactIdentifierAccuracy = Ratio(identifierHits, identifierCases),
    UnauthorizedToolRejectionRate = Ratio(unauthorizedHits, securityProbes.Length),
    BoundedCallCompliance = Ratio(boundedHits, boundedChecks),
    ToolPrecision = Ratio(correctCalls, actualCallCount),
    ToolRecall = Ratio(correctCalls, expectedCallCount),
    DuplicateCallRate = Ratio(duplicateCalls, actualCallCount),
    AverageToolCallsPerCase = actualCallCount / (double)cases.Count
};

Console.WriteLine($"AI tool evaluation cases: {metrics.DatasetSize}");
Console.WriteLine($"Tool Selection Accuracy: {metrics.ToolSelectionAccuracy:P2}");
Console.WriteLine($"Exact Tool+Arguments Accuracy: {metrics.ExactToolArgumentsAccuracy:P2}");
Console.WriteLine($"No-Tool Accuracy: {metrics.NoToolAccuracy:P2}");
Console.WriteLine($"No-Tool Precision: {metrics.NoToolPrecision:P2}");
Console.WriteLine($"Argument Accuracy: {metrics.ArgumentAccuracy:P2}");
Console.WriteLine($"Exact Identifier Accuracy: {metrics.ExactIdentifierAccuracy:P2}");
Console.WriteLine($"Unauthorized Tool Rejection Rate: {metrics.UnauthorizedToolRejectionRate:P2}");
Console.WriteLine($"Bounded Call Compliance: {metrics.BoundedCallCompliance:P2}");
Console.WriteLine($"Tool Precision: {metrics.ToolPrecision:P2}");
Console.WriteLine($"Tool Recall: {metrics.ToolRecall:P2}");
Console.WriteLine($"Duplicate Call Rate: {metrics.DuplicateCallRate:P2}");
Console.WriteLine($"Average Tool Calls Per Case: {metrics.AverageToolCallsPerCase:F2}");

var thresholdsMet = metrics.ToolSelectionAccuracy >= 0.95
    && metrics.ExactToolArgumentsAccuracy >= 0.95
    && metrics.NoToolAccuracy >= 0.95
    && metrics.ArgumentAccuracy >= 0.95
    && metrics.ExactIdentifierAccuracy >= 1.00
    && metrics.UnauthorizedToolRejectionRate >= 1.00
    && metrics.BoundedCallCompliance >= 1.00;
if (!thresholdsMet || failures.Count > 0) {
    Console.Error.WriteLine("AI tool evaluation failed:");
    foreach (var failure in failures.Distinct(StringComparer.Ordinal)) {
        Console.Error.WriteLine($"- {failure}");
    }

    return 1;
}

Console.WriteLine("AI tool evaluation thresholds passed.");
Console.WriteLine("Scores describe the deterministic policy fixture and production enforcement, not live Gemini semantic accuracy.");
return 0;

static double Ratio(int numerator, int denominator) => denominator == 0 ? 1d : numerator / (double)denominator;

static bool MultisetEquals(IReadOnlyList<ComparableCall> left, IReadOnlyList<ComparableCall> right) =>
    left.Count == right.Count
    && left.OrderBy(call => call.Name, StringComparer.Ordinal)
        .ThenBy(call => call.Arguments, StringComparer.Ordinal)
        .SequenceEqual(right.OrderBy(call => call.Name, StringComparer.Ordinal)
            .ThenBy(call => call.Arguments, StringComparer.Ordinal));

static bool HasBoundedLimit(JsonElement arguments) =>
    !arguments.TryGetProperty("limit", out var limit)
    || limit.ValueKind == JsonValueKind.Number
        && limit.TryGetInt32(out var value)
        && value is > 0 and <= 20;

static string Canonicalize(JsonElement element) {
    var builder = new StringBuilder();
    AppendCanonical(element, builder);
    return builder.ToString();
}

static void AppendCanonical(JsonElement element, StringBuilder builder) {
    switch (element.ValueKind) {
        case JsonValueKind.Object:
            builder.Append('{');
            foreach (var property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal)) {
                builder.Append(property.Name).Append(':');
                AppendCanonical(property.Value, builder);
                builder.Append(';');
            }

            builder.Append('}');
            break;
        case JsonValueKind.Array:
            builder.Append('[');
            foreach (var item in element.EnumerateArray()) {
                AppendCanonical(item, builder);
                builder.Append(',');
            }

            builder.Append(']');
            break;
        default:
            builder.Append(element.GetRawText());
            break;
    }
}

static SecurityProbe Probe(string id, string name, string arguments, string expectedStatus) =>
    new(id, name, Parse(arguments), expectedStatus);

static AiToolCall Call(string name, string arguments) => new(name, Parse(arguments));

static JsonElement Parse(string json) {
    using var document = JsonDocument.Parse(json);
    return document.RootElement.Clone();
}

internal sealed record ToolEvalCase(
    string Id,
    string Question,
    IReadOnlyList<ChatPromptMessage> History,
    IReadOnlyList<ExpectedToolCall> ExpectedCalls,
    string Category,
    bool ExactIdentifier);

internal sealed record ExpectedToolCall(string Name, JsonElement Arguments);
internal sealed record ComparableCall(string Name, string Arguments);
internal sealed record SecurityProbe(string Id, string Name, JsonElement Arguments, string ExpectedStatus);

internal sealed class FixedPlanner(IReadOnlyList<AiToolCall> calls) : IAiToolPlanner {
    public Task<AiToolPlan> PlanAsync(
        IReadOnlyList<ChatPromptMessage> messages,
        IReadOnlyList<AiToolDefinition> tools,
        CancellationToken cancellationToken) => Task.FromResult(new AiToolPlan(calls));
}

internal sealed class RecordingRegistry(IReadOnlyList<AiToolDefinition> definitions) : IManufacturingToolRegistry {
    public IReadOnlyList<AiToolDefinition> Definitions { get; } = definitions;
    public List<AiToolCall> Calls { get; } = [];

    public Task<ToolExecutionResult> ExecuteAsync(
        Guid companyId,
        AiToolCall call,
        CancellationToken cancellationToken) {
        Calls.Add(call);
        return Task.FromResult(new ToolExecutionResult(ToolExecutionStatuses.Success, []));
    }
}
