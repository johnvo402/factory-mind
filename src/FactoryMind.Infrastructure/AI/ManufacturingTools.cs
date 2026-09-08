using System.Globalization;
using System.Text.Json;
using FactoryMind.Application.Features.Boms;
using FactoryMind.Application.Features.Chat;
using FactoryMind.Application.Features.Machines;
using FactoryMind.Application.Features.Materials;
using FactoryMind.Application.Features.ProductionOrders;
using FactoryMind.Application.Features.Warehouses;
using FactoryMind.Application.Features.WorkCenters;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactoryMind.Infrastructure.AI;

public sealed class ManufacturingToolRegistry(
    GetProductionOrderStatusTool productionOrderStatus,
    GetMachineStatusTool machineStatus,
    ListMachinesTool listMachines,
    GetWorkCenterStatusTool workCenterStatus,
    GetMaterialInventoryTool materialInventory,
    GetProductionOrderMaterialReadinessTool materialReadiness,
    ListProductionOrdersTool listProductionOrders) : IManufacturingToolRegistry {
    private readonly IReadOnlyDictionary<string, IManufacturingReadTool> _tools =
        new IManufacturingReadTool[] {
            productionOrderStatus,
            machineStatus,
            listMachines,
            workCenterStatus,
            materialInventory,
            materialReadiness,
            listProductionOrders
        }.ToDictionary(tool => tool.Definition.Name, StringComparer.Ordinal);

    public IReadOnlyList<AiToolDefinition> Definitions => _tools.Values
        .Select(tool => tool.Definition)
        .ToList();

    public Task<ToolExecutionResult> ExecuteAsync(
        Guid companyId,
        AiToolCall call,
        CancellationToken cancellationToken) =>
        _tools.TryGetValue(call.Name, out var tool)
            ? tool.ExecuteAsync(companyId, call.Arguments, cancellationToken)
            : Task.FromResult(ToolResults.UnknownTool());
}

internal static class ManufacturingToolSchemas {
    public static JsonElement ExactCode(string propertyName, int maximumLength) => Parse($$"""
        {
          "type": "object",
          "properties": {
            "{{propertyName}}": { "type": "string", "minLength": 1, "maxLength": {{maximumLength}} }
          },
          "required": ["{{propertyName}}"],
          "additionalProperties": false
        }
        """);

    public static JsonElement ListMachines => Parse("""
        {
          "type": "object",
          "properties": {
            "status": { "type": "string", "enum": ["available", "running", "maintenance", "offline"] },
            "workCenterCode": { "type": "string", "minLength": 1, "maxLength": 50 },
            "limit": { "type": "integer", "minimum": 1, "maximum": 20 }
          },
          "additionalProperties": false
        }
        """);

    public static JsonElement MaterialInventory => Parse("""
        {
          "type": "object",
          "properties": {
            "materialCode": { "type": "string", "minLength": 1, "maxLength": 50 },
            "warehouseCode": { "type": "string", "minLength": 1, "maxLength": 50 }
          },
          "required": ["materialCode"],
          "additionalProperties": false
        }
        """);

    public static JsonElement ListProductionOrders => Parse("""
        {
          "type": "object",
          "properties": {
            "status": { "type": "string", "enum": ["planned", "released", "in_progress", "completed", "cancelled", "active"] },
            "priority": { "type": "string", "enum": ["low", "normal", "high", "urgent"] },
            "deliveryStatus": { "type": "string", "enum": ["no_due_date", "on_track", "due_soon", "overdue", "completed_on_time", "completed_late", "cancelled"] },
            "productCode": { "type": "string", "minLength": 1, "maxLength": 50 },
            "limit": { "type": "integer", "minimum": 1, "maximum": 20 }
          },
          "additionalProperties": false
        }
        """);

    private static JsonElement Parse(string json) {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}

internal static class ToolArguments {
    public static bool HasOnly(JsonElement arguments, params string[] allowedProperties) {
        if (arguments.ValueKind != JsonValueKind.Object) {
            return false;
        }

        var allowed = allowedProperties.ToHashSet(StringComparer.Ordinal);
        return arguments.EnumerateObject().All(property => allowed.Contains(property.Name));
    }

    public static bool RequiredString(
        JsonElement arguments,
        string propertyName,
        int maximumLength,
        out string value) {
        value = string.Empty;
        if (!arguments.TryGetProperty(propertyName, out var element)
            || element.ValueKind != JsonValueKind.String) {
            return false;
        }

        value = element.GetString()?.Trim() ?? string.Empty;
        return value.Length is > 0 && value.Length <= maximumLength;
    }

    public static bool OptionalString(
        JsonElement arguments,
        string propertyName,
        int maximumLength,
        out string? value) {
        value = null;
        if (!arguments.TryGetProperty(propertyName, out var element)) {
            return true;
        }

        if (element.ValueKind != JsonValueKind.String) {
            return false;
        }

        value = element.GetString()?.Trim();
        return value is { Length: > 0 } && value.Length <= maximumLength;
    }

    public static bool OptionalLimit(JsonElement arguments, out int limit) {
        limit = 10;
        if (!arguments.TryGetProperty("limit", out var element)) {
            return true;
        }

        return element.ValueKind == JsonValueKind.Number
            && element.TryGetInt32(out limit)
            && limit is > 0 and <= 20;
    }
}

internal static class ToolResults {
    public static ToolExecutionResult Success(params BusinessDataRecord[] records) =>
        new(ToolExecutionStatuses.Success, records);

    public static ToolExecutionResult Success(IReadOnlyList<BusinessDataRecord> records) =>
        new(ToolExecutionStatuses.Success, records);

    public static ToolExecutionResult InvalidArguments() =>
        new(ToolExecutionStatuses.InvalidArguments, [], "invalid_arguments");

    public static ToolExecutionResult NotFound() =>
        new(ToolExecutionStatuses.NotFound, [], "not_found");

    public static ToolExecutionResult NotApplicable(BusinessDataRecord record) =>
        new(ToolExecutionStatuses.NotApplicable, [record], "not_applicable");

    public static ToolExecutionResult UnknownTool() =>
        new(ToolExecutionStatuses.UnknownTool, [], "unknown_tool");
}

internal static class ToolFormatting {
    public static string Decimal(decimal value) => value.ToString("0.######", CultureInfo.InvariantCulture);

    public static string Timestamp(DateTime? value) => value.HasValue
        ? value.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)
        : "none";

    public static string Date(DateTime? value) => value.HasValue
        ? value.Value.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        : "none";

    public static string Operation(ProductionOrderOperation operation) =>
        $"{operation.Sequence} {operation.Name} ({operation.Status}), Work Center "
        + $"{operation.WorkCenterCode} - {operation.WorkCenterName}, Machine "
        + $"{operation.MachineCode ?? "none"} - {operation.MachineName ?? "none"}, "
        + $"started {Timestamp(operation.StartedAt)}";
}

public sealed class GetProductionOrderStatusTool(
    FactoryMindDbContext dbContext,
    IProductionOrderDeliveryRiskCalculator riskCalculator) : IManufacturingReadTool {
    public AiToolDefinition Definition { get; } = new(
        "get_production_order_status",
        "Returns the current tenant-scoped state and server-calculated delivery deadline facts of one production order identified by its exact number, including locked BOM/routing revisions and current/next operations. Read-only; never predicts ETA.",
        ManufacturingToolSchemas.ExactCode("number", ProductionOrderConstraints.MaximumNumberLength));

    public async Task<ToolExecutionResult> ExecuteAsync(
        Guid companyId,
        JsonElement arguments,
        CancellationToken cancellationToken) {
        if (!ToolArguments.HasOnly(arguments, "number")
            || !ToolArguments.RequiredString(
                arguments,
                "number",
                ProductionOrderConstraints.MaximumNumberLength,
                out var number)) {
            return ToolResults.InvalidArguments();
        }

        var normalizedNumber = number.ToUpperInvariant();
        var order = await dbContext.ProductionOrders.AsNoTracking()
            .Include(item => item.Product)
            .Include(item => item.BillOfMaterial)
            .Include(item => item.Routing)
            .SingleOrDefaultAsync(
                item => item.CompanyId == companyId && item.Number.ToUpper() == normalizedNumber,
                cancellationToken);
        if (order is null) {
            return ToolResults.NotFound();
        }

        var operations = await dbContext.ProductionOrderOperations.AsNoTracking()
            .Where(operation => operation.CompanyId == companyId && operation.ProductionOrderId == order.Id)
            .OrderBy(operation => operation.Sequence)
            .ToListAsync(cancellationToken);
        var current = operations.FirstOrDefault(operation => operation.Status == ProductionOperationStatuses.InProgress);
        var next = operations.FirstOrDefault(operation => operation.Status == ProductionOperationStatuses.Pending);
        var completed = operations.Count(operation => operation.Status == ProductionOperationStatuses.Completed);
        var risk = riskCalculator.Calculate(order);
        var detail = $"Product {order.Product?.Code} - {order.Product?.Name}; quantity {ToolFormatting.Decimal(order.Quantity)}; "
            + $"status {order.Status}; priority {order.Priority}; due date {ToolFormatting.Date(order.DueDate)}; "
            + $"delivery status {risk.DeliveryStatus}; days until due {risk.DaysUntilDue?.ToString(CultureInfo.InvariantCulture) ?? "none"}; "
            + $"locked BOM revision {order.BillOfMaterial?.Revision.ToString(CultureInfo.InvariantCulture) ?? "none"}; "
            + $"locked Routing revision {order.Routing?.Revision.ToString(CultureInfo.InvariantCulture) ?? "none"}; "
            + $"released {ToolFormatting.Timestamp(order.ReleasedAt)}; started {ToolFormatting.Timestamp(order.StartedAt)}; "
            + $"completed {ToolFormatting.Timestamp(order.CompletedAt)}; operations completed {completed}/{operations.Count}; "
            + $"current operation {(current is null ? "none" : ToolFormatting.Operation(current))}; "
            + $"next operation {(next is null ? "none" : ToolFormatting.Operation(next))}.";
        return ToolResults.Success(new BusinessDataRecord(
            order.Id,
            "production_order",
            order.Number,
            detail));
    }
}

public sealed class GetMachineStatusTool(FactoryMindDbContext dbContext) : IManufacturingReadTool {
    public AiToolDefinition Definition { get; } = new(
        "get_machine_status",
        "Returns the current tenant-scoped state of one machine identified by its exact code, including Work Center and current in-progress production operation when present. Read-only.",
        ManufacturingToolSchemas.ExactCode("code", MachineConstraints.MaximumCodeLength));

    public async Task<ToolExecutionResult> ExecuteAsync(
        Guid companyId,
        JsonElement arguments,
        CancellationToken cancellationToken) {
        if (!ToolArguments.HasOnly(arguments, "code")
            || !ToolArguments.RequiredString(arguments, "code", MachineConstraints.MaximumCodeLength, out var code)) {
            return ToolResults.InvalidArguments();
        }

        var normalizedCode = code.ToUpperInvariant();
        var machine = await dbContext.Machines.AsNoTracking()
            .Include(item => item.WorkCenter)
            .SingleOrDefaultAsync(
                item => item.CompanyId == companyId && item.Code.ToUpper() == normalizedCode,
                cancellationToken);
        if (machine is null) {
            return ToolResults.NotFound();
        }

        var operation = await dbContext.ProductionOrderOperations.AsNoTracking()
            .Include(item => item.ProductionOrder)
            .Where(item => item.CompanyId == companyId
                && item.MachineId == machine.Id
                && item.Status == ProductionOperationStatuses.InProgress)
            .OrderBy(item => item.Sequence)
            .FirstOrDefaultAsync(cancellationToken);
        var current = operation is null
            ? "no current in-progress operation"
            : $"current operation: Production Order {operation.ProductionOrder?.Number}, "
                + $"sequence {operation.Sequence}, {operation.Name}, started {ToolFormatting.Timestamp(operation.StartedAt)}";
        return ToolResults.Success(new BusinessDataRecord(
            machine.Id,
            "machine",
            $"{machine.Code} - {machine.Name}",
            $"Status {machine.Status}; Work Center {machine.WorkCenter?.Code ?? "none"} - "
                + $"{machine.WorkCenter?.Name ?? "none"}; {current}."));
    }
}

public sealed class ListMachinesTool(FactoryMindDbContext dbContext) : IManufacturingReadTool {
    public AiToolDefinition Definition { get; } = new(
        "list_machines",
        "Lists up to 20 tenant-scoped machines, optionally filtered by an allowed status and exact Work Center code, with current in-progress production operation when present. Read-only.",
        ManufacturingToolSchemas.ListMachines);

    public async Task<ToolExecutionResult> ExecuteAsync(
        Guid companyId,
        JsonElement arguments,
        CancellationToken cancellationToken) {
        if (!ToolArguments.HasOnly(arguments, "status", "workCenterCode", "limit")
            || !ToolArguments.OptionalString(arguments, "status", 30, out var status)
            || status is not null && !MachineStatuses.All.Contains(status)
            || !ToolArguments.OptionalString(
                arguments,
                "workCenterCode",
                WorkCenterConstraints.MaximumCodeLength,
                out var workCenterCode)
            || !ToolArguments.OptionalLimit(arguments, out var limit)) {
            return ToolResults.InvalidArguments();
        }

        var normalizedWorkCenterCode = workCenterCode?.ToUpperInvariant();
        var query = dbContext.Machines.AsNoTracking()
            .Where(machine => machine.CompanyId == companyId);
        if (status is not null) {
            query = query.Where(machine => machine.Status == status.ToLowerInvariant());
        }

        if (normalizedWorkCenterCode is not null) {
            query = query.Where(machine => machine.WorkCenter != null
                && machine.WorkCenter.Code.ToUpper() == normalizedWorkCenterCode);
        }

        var machines = await query
            .OrderBy(machine => machine.Code)
            .Take(limit)
            .Select(machine => new MachineRow(
                machine.Id,
                machine.Code,
                machine.Name,
                machine.Status,
                machine.WorkCenter != null ? machine.WorkCenter.Code : null,
                machine.WorkCenter != null ? machine.WorkCenter.Name : null))
            .ToListAsync(cancellationToken);
        var ids = machines.Select(machine => machine.Id).ToList();
        var operations = await dbContext.ProductionOrderOperations.AsNoTracking()
            .Where(operation => operation.CompanyId == companyId
                && operation.MachineId.HasValue
                && ids.Contains(operation.MachineId.Value)
                && operation.Status == ProductionOperationStatuses.InProgress)
            .Select(operation => new OperationRow(
                operation.MachineId!.Value,
                operation.ProductionOrder!.Number,
                operation.Sequence,
                operation.Name,
                operation.StartedAt))
            .ToListAsync(cancellationToken);
        var byMachine = operations.ToDictionary(operation => operation.MachineId);
        return ToolResults.Success(machines.Select(machine => {
            var current = byMachine.TryGetValue(machine.Id, out var operation)
                ? $"current PO {operation.ProductionOrderNumber}, operation {operation.Sequence} {operation.Name}, started {ToolFormatting.Timestamp(operation.StartedAt)}"
                : "no current in-progress operation";
            return new BusinessDataRecord(
                machine.Id,
                "machine",
                $"{machine.Code} - {machine.Name}",
                $"Status {machine.Status}; Work Center {machine.WorkCenterCode ?? "none"} - {machine.WorkCenterName ?? "none"}; {current}.");
        }).ToList());
    }

    private sealed record MachineRow(
        Guid Id,
        string Code,
        string Name,
        string Status,
        string? WorkCenterCode,
        string? WorkCenterName);
    private sealed record OperationRow(
        Guid MachineId,
        string ProductionOrderNumber,
        int Sequence,
        string Name,
        DateTime? StartedAt);
}

public sealed class GetWorkCenterStatusTool(FactoryMindDbContext dbContext) : IManufacturingReadTool {
    private const int MaximumMachines = 10;
    private const int MaximumCurrentOperations = 5;

    public AiToolDefinition Definition { get; } = new(
        "get_work_center_status",
        "Returns one tenant-scoped Work Center by exact code, its active state, machine counts by stored status, a bounded machine list, and current in-progress operations. Read-only; no OEE or utilization inference.",
        ManufacturingToolSchemas.ExactCode("code", WorkCenterConstraints.MaximumCodeLength));

    public async Task<ToolExecutionResult> ExecuteAsync(
        Guid companyId,
        JsonElement arguments,
        CancellationToken cancellationToken) {
        if (!ToolArguments.HasOnly(arguments, "code")
            || !ToolArguments.RequiredString(arguments, "code", WorkCenterConstraints.MaximumCodeLength, out var code)) {
            return ToolResults.InvalidArguments();
        }

        var normalizedCode = code.ToUpperInvariant();
        var workCenter = await dbContext.WorkCenters.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.CompanyId == companyId && item.Code.ToUpper() == normalizedCode,
                cancellationToken);
        if (workCenter is null) {
            return ToolResults.NotFound();
        }

        var machineQuery = dbContext.Machines.AsNoTracking()
            .Where(machine => machine.CompanyId == companyId && machine.WorkCenterId == workCenter.Id);
        var statusCounts = await machineQuery
            .GroupBy(machine => machine.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Status, item => item.Count, cancellationToken);
        var machines = await machineQuery
            .OrderBy(machine => machine.Code)
            .Take(MaximumMachines)
            .Select(machine => new { machine.Id, machine.Code, machine.Name, machine.Status })
            .ToListAsync(cancellationToken);
        var operations = await dbContext.ProductionOrderOperations.AsNoTracking()
            .Where(operation => operation.CompanyId == companyId
                && operation.WorkCenterId == workCenter.Id
                && operation.Status == ProductionOperationStatuses.InProgress)
            .OrderBy(operation => operation.ProductionOrder!.Number)
            .ThenBy(operation => operation.Sequence)
            .Take(MaximumCurrentOperations)
            .Select(operation => new {
                ProductionOrderNumber = operation.ProductionOrder!.Number,
                operation.Name,
                operation.MachineCode,
                operation.StartedAt
            })
            .ToListAsync(cancellationToken);
        var counts = string.Join(", ", MachineStatuses.All
            .OrderBy(status => status, StringComparer.Ordinal)
            .Select(status => $"{status}={statusCounts.GetValueOrDefault(status)}"));
        var machineList = machines.Count == 0
            ? "none"
            : string.Join(", ", machines.Select(machine => $"{machine.Code} ({machine.Status})"));
        var currentOperations = operations.Count == 0
            ? "none"
            : string.Join("; ", operations.Select(operation =>
                $"PO {operation.ProductionOrderNumber}, {operation.Name}, machine {operation.MachineCode ?? "none"}, started {ToolFormatting.Timestamp(operation.StartedAt)}"));
        return ToolResults.Success(new BusinessDataRecord(
            workCenter.Id,
            "work_center",
            $"{workCenter.Code} - {workCenter.Name}",
            $"Active {workCenter.IsActive}; machine counts {counts}; current operations {currentOperations}; machines {machineList}."));
    }
}

public sealed class GetMaterialInventoryTool(FactoryMindDbContext dbContext) : IManufacturingReadTool {
    private const int MaximumWarehouseBalances = 8;

    public AiToolDefinition Definition { get; } = new(
        "get_material_inventory",
        "Returns current tenant-scoped inventory for one material exact code, aggregated across active warehouses or restricted to one exact active warehouse code. Read-only.",
        ManufacturingToolSchemas.MaterialInventory);

    public async Task<ToolExecutionResult> ExecuteAsync(
        Guid companyId,
        JsonElement arguments,
        CancellationToken cancellationToken) {
        if (!ToolArguments.HasOnly(arguments, "materialCode", "warehouseCode")
            || !ToolArguments.RequiredString(
                arguments,
                "materialCode",
                MaterialConstraints.MaximumCodeLength,
                out var materialCode)
            || !ToolArguments.OptionalString(
                arguments,
                "warehouseCode",
                WarehouseConstraints.MaximumCodeLength,
                out var warehouseCode)) {
            return ToolResults.InvalidArguments();
        }

        var normalizedMaterialCode = materialCode.ToUpperInvariant();
        var material = await dbContext.Materials.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.CompanyId == companyId && item.Code.ToUpper() == normalizedMaterialCode,
                cancellationToken);
        if (material is null) {
            return ToolResults.NotFound();
        }

        Guid? warehouseId = null;
        if (warehouseCode is not null) {
            var normalizedWarehouseCode = warehouseCode.ToUpperInvariant();
            warehouseId = await dbContext.Warehouses.AsNoTracking()
                .Where(warehouse => warehouse.CompanyId == companyId
                    && warehouse.IsActive
                    && warehouse.Code.ToUpper() == normalizedWarehouseCode)
                .Select(warehouse => (Guid?)warehouse.Id)
                .SingleOrDefaultAsync(cancellationToken);
            if (!warehouseId.HasValue) {
                return ToolResults.NotFound();
            }
        }

        var balancesQuery = dbContext.InventoryBalances.AsNoTracking()
            .Where(balance => balance.CompanyId == companyId
                && balance.MaterialId == material.Id
                && balance.Warehouse!.CompanyId == companyId
                && balance.Warehouse.IsActive);
        if (warehouseId.HasValue) {
            balancesQuery = balancesQuery.Where(balance => balance.WarehouseId == warehouseId.Value);
        }

        var balanceCount = await balancesQuery.CountAsync(cancellationToken);
        var total = await balancesQuery
            .SumAsync(balance => (decimal?)balance.Quantity, cancellationToken) ?? 0m;
        var balances = await balancesQuery
            .OrderBy(balance => balance.Warehouse!.Code)
            .Take(MaximumWarehouseBalances)
            .Select(balance => new {
                balance.Warehouse!.Code,
                balance.Warehouse.Name,
                balance.Quantity
            })
            .ToListAsync(cancellationToken);
        var breakdown = balances.Count == 0
            ? "no balance rows; total available quantity 0"
            : string.Join("; ", balances.Select(balance =>
                $"{balance.Code} - {balance.Name}: {ToolFormatting.Decimal(balance.Quantity)} {material.Unit}"));
        return ToolResults.Success(new BusinessDataRecord(
            material.Id,
            "material_inventory",
            $"{material.Code} - {material.Name}",
            $"Unit {material.Unit}; active warehouse balances shown {balances.Count}/{balanceCount}: {breakdown}; "
                + $"total available quantity {ToolFormatting.Decimal(total)} {material.Unit}."));
    }
}

public sealed class GetProductionOrderMaterialReadinessTool(
    FactoryMindDbContext dbContext,
    MaterialRequirementCalculator calculator) : IManufacturingReadTool {
    private const int MaximumMaterialEvidence = 20;

    public AiToolDefinition Definition { get; } = new(
        "get_production_order_material_readiness",
        "Compares one Planned or Released production order's server-calculated material requirements with current aggregate stock in active tenant warehouses. Returns not_applicable after production starts. Read-only snapshot; not a reservation or schedule promise.",
        ManufacturingToolSchemas.ExactCode("number", ProductionOrderConstraints.MaximumNumberLength));

    public async Task<ToolExecutionResult> ExecuteAsync(
        Guid companyId,
        JsonElement arguments,
        CancellationToken cancellationToken) {
        if (!ToolArguments.HasOnly(arguments, "number")
            || !ToolArguments.RequiredString(
                arguments,
                "number",
                ProductionOrderConstraints.MaximumNumberLength,
                out var number)) {
            return ToolResults.InvalidArguments();
        }

        var normalizedNumber = number.ToUpperInvariant();
        var order = await dbContext.ProductionOrders.AsNoTracking()
            .Include(item => item.Product)
            .SingleOrDefaultAsync(
                item => item.CompanyId == companyId && item.Number.ToUpper() == normalizedNumber,
                cancellationToken);
        if (order is null) {
            return ToolResults.NotFound();
        }

        if (order.Status is ProductionOrderStatuses.InProgress
            or ProductionOrderStatuses.Completed
            or ProductionOrderStatuses.Cancelled) {
            var reason = order.Status == ProductionOrderStatuses.InProgress
                ? "Production order is already in progress; full-order pre-start material readiness is not applicable because materials have already been consumed by the start flow."
                : $"Production order status is {order.Status}; pre-start material readiness is not applicable.";
            return ToolResults.NotApplicable(new BusinessDataRecord(
                order.Id,
                "production_order_material_readiness",
                order.Number,
                $"materialReadinessApplicable=false; reason: {reason}"));
        }

        var bomQuery = dbContext.BillOfMaterials.AsNoTracking()
            .AsSplitQuery()
            .Include(bom => bom.Product)
            .Include(bom => bom.Items)
            .ThenInclude(item => item.Material)
            .Where(bom => bom.CompanyId == companyId && bom.ProductId == order.ProductId);
        BillOfMaterial? bom;
        if (order.Status == ProductionOrderStatuses.Planned) {
            bom = await bomQuery.SingleOrDefaultAsync(
                item => item.Status == BillOfMaterialStatuses.Active,
                cancellationToken);
        } else {
            bom = order.BillOfMaterialId.HasValue
                ? await bomQuery.SingleOrDefaultAsync(
                    item => item.Id == order.BillOfMaterialId.Value,
                    cancellationToken)
                : null;
        }

        if (bom is null) {
            return ToolResults.NotApplicable(new BusinessDataRecord(
                order.Id,
                "production_order_material_readiness",
                order.Number,
                "materialReadinessApplicable=false; reason: the applicable active or locked BOM was not found."));
        }

        var materialIds = bom.Items.Select(item => item.MaterialId).Distinct().ToList();
        var inventory = await dbContext.InventoryBalances.AsNoTracking()
            .Where(balance => balance.CompanyId == companyId
                && materialIds.Contains(balance.MaterialId)
                && balance.Warehouse!.CompanyId == companyId
                && balance.Warehouse.IsActive)
            .GroupBy(balance => balance.MaterialId)
            .Select(group => new { MaterialId = group.Key, Quantity = group.Sum(item => item.Quantity) })
            .ToDictionaryAsync(item => item.MaterialId, item => item.Quantity, cancellationToken);
        var readiness = calculator.Calculate(bom, order.Quantity, inventory);
        var records = new List<BusinessDataRecord> {
            new(
                order.Id,
                "production_order_material_readiness",
                order.Number,
                $"materialReadinessApplicable=true; BOM revision {bom.Revision}; allMaterialsSufficient={readiness.CanProduce}; "
                    + $"evaluated {readiness.Materials.Count} materials against aggregate current stock in active warehouses. "
                    + "Snapshot compares this order independently; it excludes reservations, competing orders, future receipts, scheduling, and transfer lead time.")
        };
        records.AddRange(readiness.Materials.Take(MaximumMaterialEvidence).Select(material =>
            new BusinessDataRecord(
                material.MaterialId,
                "production_order_material",
                $"{order.Number} / {material.MaterialCode} - {material.MaterialName}",
                $"Unit {material.Unit}; required quantity {ToolFormatting.Decimal(material.RequiredQuantity)}; "
                    + $"available quantity {ToolFormatting.Decimal(material.AvailableQuantity)}; "
                    + $"shortage quantity {ToolFormatting.Decimal(material.ShortageQuantity)}; isSufficient={material.IsSufficient}.")));
        return ToolResults.Success(records);
    }
}

public sealed class ListProductionOrdersTool(
    FactoryMindDbContext dbContext,
    IProductionOrderDeliveryRiskCalculator riskCalculator) : IManufacturingReadTool {
    public AiToolDefinition Definition { get; } = new(
        "list_production_orders",
        "Lists up to 20 tenant-scoped production orders, optionally filtered by allowed status, priority, server-calculated delivery status, and exact product code. Includes current execution facts. Read-only; never predicts ETA or capacity.",
        ManufacturingToolSchemas.ListProductionOrders);

    public async Task<ToolExecutionResult> ExecuteAsync(
        Guid companyId,
        JsonElement arguments,
        CancellationToken cancellationToken) {
        if (!ToolArguments.HasOnly(arguments, "status", "priority", "deliveryStatus", "productCode", "limit")
            || !ToolArguments.OptionalString(
                arguments,
                "status",
                ProductionOrderConstraints.MaximumStatusLength,
                out var status)
            || status is not null
                && status != "active"
                && !ProductionOrderStatuses.All.Contains(status)
            || !ToolArguments.OptionalString(arguments, "priority", 30, out var priority)
            || priority is not null && !ProductionOrderPriorities.All.Contains(priority)
            || !ToolArguments.OptionalString(arguments, "deliveryStatus", 30, out var deliveryStatus)
            || deliveryStatus is not null && !ProductionOrderDeliveryStatuses.All.Contains(deliveryStatus)
            || !ToolArguments.OptionalString(arguments, "productCode", 50, out var productCode)
            || !ToolArguments.OptionalLimit(arguments, out var limit)) {
            return ToolResults.InvalidArguments();
        }

        var normalizedProductCode = productCode?.ToUpperInvariant();
        var activeStatuses = new[] {
            ProductionOrderStatuses.Planned,
            ProductionOrderStatuses.Released,
            ProductionOrderStatuses.InProgress
        };
        var query = dbContext.ProductionOrders.AsNoTracking()
            .Where(order => order.CompanyId == companyId);
        if (status == "active") {
            query = query.Where(order => activeStatuses.Contains(order.Status));
        } else if (status is not null) {
            query = query.Where(order => order.Status == status.ToLowerInvariant());
        }

        if (priority is not null) {
            query = query.Where(order => order.Priority == priority.ToLowerInvariant());
        }

        var now = riskCalculator.UtcNow;
        var dueSoonThrough = riskCalculator.DueSoonThrough;
        if (deliveryStatus is not null) {
            query = query.Where(ProductionOrderDeliveryRiskCalculator.DeliveryStatusPredicate(
                deliveryStatus,
                now,
                dueSoonThrough));
        }

        if (normalizedProductCode is not null) {
            query = query.Where(order => order.Product!.Code.ToUpper() == normalizedProductCode);
        }

        var orders = await query
            .OrderByDescending(order => order.UpdatedAt)
            .ThenBy(order => order.Number)
            .Take(limit)
            .Select(order => new OrderRow(
                order.Id,
                order.Number,
                order.Product!.Code,
                order.Product.Name,
                order.Quantity,
                order.Status,
                order.Priority,
                order.DueDate,
                order.CompletedAt))
            .ToListAsync(cancellationToken);
        var ids = orders.Select(order => order.Id).ToList();
        var operations = await dbContext.ProductionOrderOperations.AsNoTracking()
            .Where(operation => operation.CompanyId == companyId
                && ids.Contains(operation.ProductionOrderId)
                && operation.Status == ProductionOperationStatuses.InProgress)
            .Select(operation => new OperationRow(
                operation.ProductionOrderId,
                operation.Sequence,
                operation.Name,
                operation.WorkCenterCode,
                operation.MachineCode))
            .ToListAsync(cancellationToken);
        var byOrder = operations.ToDictionary(operation => operation.ProductionOrderId);
        return ToolResults.Success(orders.Select(order => {
            var risk = ProductionOrderDeliveryRiskCalculator.Calculate(
                order.Status,
                order.DueDate,
                order.CompletedAt,
                now,
                riskCalculator.DueSoonDays);
            var current = byOrder.TryGetValue(order.Id, out var operation)
                ? $"current operation {operation.Sequence} {operation.Name}; Work Center {operation.WorkCenterCode}; Machine {operation.MachineCode ?? "none"}"
                : "no current in-progress operation";
            return new BusinessDataRecord(
                order.Id,
                "production_order",
                order.Number,
                $"Product {order.ProductCode} - {order.ProductName}; quantity {ToolFormatting.Decimal(order.Quantity)}; "
                    + $"status {order.Status}; priority {order.Priority}; due date {ToolFormatting.Date(order.DueDate)}; "
                    + $"delivery status {risk.DeliveryStatus}; days until due {risk.DaysUntilDue?.ToString(CultureInfo.InvariantCulture) ?? "none"}; {current}.");
        }).ToList());
    }

    private sealed record OrderRow(
        Guid Id,
        string Number,
        string ProductCode,
        string ProductName,
        decimal Quantity,
        string Status,
        string Priority,
        DateTime? DueDate,
        DateTime? CompletedAt);
    private sealed record OperationRow(
        Guid ProductionOrderId,
        int Sequence,
        string Name,
        string WorkCenterCode,
        string? MachineCode);
}
