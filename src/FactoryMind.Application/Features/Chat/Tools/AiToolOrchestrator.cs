using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FactoryMind.Shared.Observability;

namespace FactoryMind.Application.Features.Chat.Tools;

public sealed class AiToolOrchestrator(
    IIntentRouter intentRouter,
    IAiToolPlanner planner,
    IManufacturingToolRegistry registry) : IAiToolOrchestrator {
    public const int MaximumToolCallsPerRequest = 3;
    private const int MaximumHistoryMessages = 20;
    private const string PlannerInstructions =
        "Select only the provided read-only manufacturing tools needed to answer the current user question. "
        + "Use the smallest sufficient tool set and never request redundant or duplicate evidence. "
        + "Do not answer the user and do not emit prose. Choose zero tools when live manufacturing data is not needed. "
        + "Request at most three tools in this single planning round. Use exact identifiers supplied by the user. "
        + "Never invent tool names, request mutations, or supply company, tenant, user, role, permission, or authorization identity.";

    public async Task<IReadOnlyList<BusinessDataRecord>> CollectAsync(
        Guid companyId,
        string question,
        IReadOnlyList<ChatPromptMessage> recentMessages,
        CancellationToken cancellationToken) {
        var route = intentRouter.Route(question);
        if (!IsEligible(route)) {
            return [];
        }

        var messages = new List<ChatPromptMessage> {
            new("system", PlannerInstructions)
        };
        messages.AddRange(recentMessages.TakeLast(MaximumHistoryMessages));
        messages.Add(new ChatPromptMessage("user", question));

        AiToolPlan plan;
        try {
            plan = await planner.PlanAsync(messages, registry.Definitions, cancellationToken);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception) {
            return [];
        }

        FactoryMindTelemetry.AiToolPlanCallsRequested.Add(
            plan.Calls.Count,
            FactoryMindTelemetry.Tags(("outcome", "planned")));

        var distinctCalls = plan.Calls
            .DistinctBy(call => $"{call.Name}\n{Canonicalize(call.Arguments)}", StringComparer.Ordinal)
            .ToList();
        var duplicateCalls = plan.Calls.Count - distinctCalls.Count;
        if (duplicateCalls > 0) {
            RecordRejected(duplicateCalls, "bounded", "duplicate");
        }

        var droppedCalls = Math.Max(distinctCalls.Count - MaximumToolCallsPerRequest, 0);
        if (droppedCalls > 0) {
            RecordRejected(droppedCalls, "bounded", "limit_exceeded");
        }

        var records = new List<BusinessDataRecord>();
        foreach (var call in distinctCalls.Take(MaximumToolCallsPerRequest)) {
            cancellationToken.ThrowIfCancellationRequested();
            var startedTimestamp = Stopwatch.GetTimestamp();
            var safeToolName = registry.Definitions.Any(definition => definition.Name == call.Name)
                ? call.Name
                : "unknown";
            var outcome = ToolExecutionStatuses.Error;
            using var activity = FactoryMindTelemetry.ActivitySource.StartActivity(
                "factorymind.ai.tool.execute",
                ActivityKind.Internal);
            activity?.SetTag("tool.name", safeToolName);
            try {
                var result = await registry.ExecuteAsync(companyId, call, cancellationToken);
                outcome = result.Status;
                records.AddRange(result.Records);
                if (result.Status is ToolExecutionStatuses.UnknownTool
                    or ToolExecutionStatuses.InvalidArguments
                    or ToolExecutionStatuses.NotFound
                    or ToolExecutionStatuses.NotApplicable) {
                    RecordRejected(1, safeToolName, result.Status);
                }
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                outcome = "cancelled";
                throw;
            } catch (Exception) {
            } finally {
                var tags = FactoryMindTelemetry.Tags(("tool", safeToolName), ("outcome", outcome));
                FactoryMindTelemetry.AiToolCalls.Add(1, tags);
                FactoryMindTelemetry.AiToolPlanCallsExecuted.Add(1, tags);
                FactoryMindTelemetry.AiToolDuration.Record(
                    Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds,
                    tags);
                activity?.SetTag("factorymind.outcome", outcome);
                if (outcome is ToolExecutionStatuses.Error) {
                    activity?.SetStatus(ActivityStatusCode.Error);
                }
            }
        }

        return records;
    }

    public static bool IsEligible(IntentRoute route) =>
        route.Intent == ChatIntent.Business
        || route.Intent == ChatIntent.Hybrid && !route.IsFallback;

    private static void RecordRejected(long count, string tool, string reason) {
        var tags = FactoryMindTelemetry.Tags(("tool", tool), ("reason", reason));
        FactoryMindTelemetry.AiToolRejected.Add(count, tags);
        FactoryMindTelemetry.AiToolPlanCallsRejected.Add(count, tags);
    }

    private static string Canonicalize(JsonElement element) {
        var builder = new StringBuilder();
        AppendCanonical(element, builder);
        return builder.ToString();
    }

    private static void AppendCanonical(JsonElement element, StringBuilder builder) {
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
}
