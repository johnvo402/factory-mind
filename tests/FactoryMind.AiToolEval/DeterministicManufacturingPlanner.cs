using System.Text.Json;
using System.Text.RegularExpressions;
using FactoryMind.Application.Common.Search;
using FactoryMind.Application.Features.Chat;

namespace FactoryMind.AiToolEval;

internal sealed partial class DeterministicManufacturingPlanner : IAiToolPlanner {
    public Task<AiToolPlan> PlanAsync(
        IReadOnlyList<ChatPromptMessage> messages,
        IReadOnlyList<AiToolDefinition> tools,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var question = messages.LastOrDefault(message => message.Role == "user")?.Content ?? string.Empty;
        var history = string.Join(' ', messages.Select(message => message.Content));
        var allowedTools = tools.Select(tool => tool.Name).ToHashSet(StringComparer.Ordinal);
        var calls = Select(question, history)
            .Where(call => allowedTools.Contains(call.Name))
            .Take(3)
            .ToList();
        return Task.FromResult(new AiToolPlan(calls));
    }

    private static IReadOnlyList<AiToolCall> Select(string question, string history) {
        var normalized = SearchTextNormalizer.Normalize(question);
        if (IsAdversarialMutation(normalized)) {
            return [];
        }

        var calls = new List<AiToolCall>();
        var productionOrder = Match(ProductionOrderRegex(), question)
            ?? Match(ProductionOrderRegex(), history);
        var machine = Match(MachineRegex(), question);
        var material = Match(MaterialRegex(), question);
        var warehouse = Match(WarehouseRegex(), question);
        var product = Match(ProductRegex(), question);
        var workCenter = WorkCenter(question);

        if (productionOrder is null && IsMachineList(normalized)) {
            var arguments = new Dictionary<string, object?>();
            AddStatus(arguments, normalized, machineStatuses: true);
            if (workCenter is not null) {
                arguments["workCenterCode"] = workCenter;
            }

            calls.Add(Call("list_machines", arguments));
            return calls;
        }

        if (IsProductionOrderList(normalized)) {
            var arguments = new Dictionary<string, object?>();
            AddStatus(arguments, normalized, machineStatuses: false);
            if (product is not null) {
                arguments["productCode"] = product;
            }

            calls.Add(Call("list_production_orders", arguments));
            return calls;
        }

        if (machine is not null && IsMachineQuestion(normalized)) {
            calls.Add(Call("get_machine_status", new Dictionary<string, object?> { ["code"] = machine }));
        }

        if (workCenter is not null && IsWorkCenterQuestion(normalized)) {
            calls.Add(Call("get_work_center_status", new Dictionary<string, object?> { ["code"] = workCenter }));
        }

        if (productionOrder is not null && IsProductionOrderStatusQuestion(normalized)) {
            calls.Add(Call(
                "get_production_order_status",
                new Dictionary<string, object?> { ["number"] = productionOrder }));
        }

        if (productionOrder is not null && IsReadinessQuestion(normalized)) {
            calls.Add(Call(
                "get_production_order_material_readiness",
                new Dictionary<string, object?> { ["number"] = productionOrder }));
        }

        if (material is not null && IsInventoryQuestion(normalized)) {
            var arguments = new Dictionary<string, object?> { ["materialCode"] = material };
            if (warehouse is not null) {
                arguments["warehouseCode"] = warehouse;
            }

            calls.Add(Call("get_material_inventory", arguments));
        }

        return calls
            .DistinctBy(call => $"{call.Name}:{call.Arguments.GetRawText()}", StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsAdversarialMutation(string text) => ContainsAny(
        text,
        "ignore system",
        "ignore previous",
        "execute_sql",
        "delete_machine",
        "update_machine",
        "internal method",
        "another tenant",
        "companyid",
        "tenantid",
        "act as admin",
        "start production order",
        "hay start",
        "call get_machine_status",
        "call get machine status",
        "call delete",
        "limit=999999");

    private static bool IsMachineList(string text) =>
        ContainsAny(text, "may nao", "liet ke may", "list machines", "show machines");

    private static bool IsProductionOrderList(string text) =>
        (ContainsAny(text, "liet ke", "show", "list")
            && ContainsAny(text, "lenh", "production order"))
        || text.Contains("production orders for", StringComparison.Ordinal);

    private static bool IsMachineQuestion(string text) => ContainsAny(
        text,
        "may",
        "machine",
        "trang thai",
        "dang chay",
        "dang lam",
        "running",
        "current",
        "broken",
        "bi hong");

    private static bool IsWorkCenterQuestion(string text) =>
        ContainsAny(text, "work center", "workcenter")
        && ContainsAny(text, "dang chay", "dang lam", "running", "status", "trang thai", "bao nhieu may");

    private static bool IsProductionOrderStatusQuestion(string text) => ContainsAny(
        text,
        "cong doan",
        "trang thai",
        "status",
        "hien trang",
        "o dau",
        "next operation",
        "tiep theo",
        "hoan thanh bao nhieu",
        "khi nao",
        "theo sop",
        "where is",
        "where and",
        "dang chay o may");

    private static bool IsReadinessQuestion(string text) => ContainsAny(
        text,
        "du nguyen lieu",
        "thieu nguyen lieu",
        "nguyen lieu chua",
        "nguyen lieu cho lenh",
        "material readiness",
        "materials to start",
        "enough material",
        "short materials",
        "can start");

    private static bool IsInventoryQuestion(string text) => ContainsAny(
        text,
        "bao nhieu",
        "how much",
        "con bao nhieu",
        "ton kho",
        "inventory",
        "stock",
        "in warehouse",
        "trong kho");

    private static void AddStatus(Dictionary<string, object?> arguments, string text, bool machineStatuses) {
        if (ContainsAny(text, "dang chay", "dang san xuat", "running", "in progress")) {
            arguments["status"] = machineStatuses ? "running" : "in_progress";
        } else if (ContainsAny(text, "maintenance", "bao tri")) {
            arguments["status"] = "maintenance";
        } else if (ContainsAny(text, "available", "san sang", "ranh")) {
            arguments["status"] = "available";
        } else if (ContainsAny(text, "offline", "ngoai tuyen")) {
            arguments["status"] = "offline";
        } else if (ContainsAny(text, "released", "da release")) {
            arguments["status"] = "released";
        } else if (ContainsAny(text, "planned", "ke hoach")) {
            arguments["status"] = "planned";
        } else if (ContainsAny(text, "completed", "da hoan thanh")) {
            arguments["status"] = "completed";
        } else if (ContainsAny(text, "cancelled", "canceled", "da huy")) {
            arguments["status"] = "cancelled";
        }
    }

    private static AiToolCall Call(string name, Dictionary<string, object?> arguments) {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(arguments));
        return new AiToolCall(name, document.RootElement.Clone());
    }

    private static string? WorkCenter(string text) {
        var match = WorkCenterRegex().Match(text);
        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : null;
    }

    private static string? Match(Regex regex, string text) {
        var match = regex.Match(text);
        return match.Success ? match.Value.ToUpperInvariant() : null;
    }

    private static bool ContainsAny(string text, params string[] values) =>
        values.Any(value => text.Contains(value, StringComparison.Ordinal));

    [GeneratedRegex(@"\bPO-[A-Z0-9]+(?:-[A-Z0-9]+)*\b", RegexOptions.IgnoreCase)]
    private static partial Regex ProductionOrderRegex();

    [GeneratedRegex(@"\b(?:CNC|PAINT)-[A-Z0-9]+(?:-[A-Z0-9]+)*\b", RegexOptions.IgnoreCase)]
    private static partial Regex MachineRegex();

    [GeneratedRegex(@"\b(?:RM|MAT)-[A-Z0-9]+(?:-[A-Z0-9]+)*\b", RegexOptions.IgnoreCase)]
    private static partial Regex MaterialRegex();

    [GeneratedRegex(@"\bWH-[A-Z0-9]+(?:-[A-Z0-9]+)*\b", RegexOptions.IgnoreCase)]
    private static partial Regex WarehouseRegex();

    [GeneratedRegex(@"\bPROD-[A-Z0-9]+(?:-[A-Z0-9]+)*\b", RegexOptions.IgnoreCase)]
    private static partial Regex ProductRegex();

    [GeneratedRegex(@"(?:work\s*center|workcenter)\s+([A-Z][A-Z0-9-]*)", RegexOptions.IgnoreCase)]
    private static partial Regex WorkCenterRegex();
}
