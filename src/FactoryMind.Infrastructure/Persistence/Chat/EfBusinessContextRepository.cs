using System.Globalization;
using FactoryMind.Application.Common.Search;
using FactoryMind.Application.Features.Chat;
using FactoryMind.Application.Features.Chat.Rag;
using FactoryMind.Application.Features.ProductionOrders;
using FactoryMind.Domain.Manufacturing;
using Microsoft.EntityFrameworkCore;

namespace FactoryMind.Infrastructure.Persistence.Chat;

public sealed class EfBusinessContextRepository(
    FactoryMindDbContext dbContext,
    IProductionOrderDeliveryRiskCalculator riskCalculator) : IBusinessContextRepository {
    private const int MinimumCandidateLimit = 40;

    public async Task<IReadOnlyList<BusinessDataRecord>> RetrieveAsync(
        Guid companyId,
        string question,
        BusinessDataScope scopes,
        string? machineStatus,
        string? productionOrderStatus,
        int limitPerScope,
        CancellationToken cancellationToken) {
        var records = new List<BusinessDataRecord>();

        if (scopes.HasFlag(BusinessDataScope.Machines)) {
            records.AddRange(await MachinesAsync(
                companyId,
                question,
                machineStatus,
                limitPerScope,
                cancellationToken));
        }

        if (scopes.HasFlag(BusinessDataScope.Materials)) {
            records.AddRange(await MaterialsAsync(companyId, question, limitPerScope, cancellationToken));
        }

        if (scopes.HasFlag(BusinessDataScope.Inventory)) {
            records.AddRange(await InventoryAsync(companyId, question, limitPerScope, cancellationToken));
        }

        if (scopes.HasFlag(BusinessDataScope.Products)) {
            records.AddRange(await ProductsAsync(companyId, question, limitPerScope, cancellationToken));
        }

        if (scopes.HasFlag(BusinessDataScope.ProductionOrders)) {
            records.AddRange(await ProductionOrdersAsync(
                companyId,
                question,
                productionOrderStatus,
                limitPerScope,
                cancellationToken));
        }

        if (scopes.HasFlag(BusinessDataScope.WorkCenters)) {
            records.AddRange(await WorkCentersAsync(companyId, question, limitPerScope, cancellationToken));
        }

        if (scopes.HasFlag(BusinessDataScope.Routings)) {
            records.AddRange(await RoutingsAsync(companyId, question, limitPerScope, cancellationToken));
        }

        if (scopes.HasFlag(BusinessDataScope.ProductionOperations)) {
            records.AddRange(await ProductionOperationsAsync(
                companyId,
                question,
                limitPerScope,
                cancellationToken));
        }

        return records;
    }

    private async Task<IReadOnlyList<BusinessDataRecord>> MachinesAsync(
        Guid companyId,
        string question,
        string? status,
        int limit,
        CancellationToken cancellationToken) {
        var exactTerms = ExactQueryTerms(question);
        var candidates = await dbContext.Machines
            .AsNoTracking()
            .Where(machine => machine.CompanyId == companyId)
            .Where(machine => status == null || machine.Status == status)
            .OrderByDescending(machine => exactTerms.Contains(machine.Code.ToLower()))
            .ThenBy(machine => machine.Code)
            .Take(CandidateLimit(limit))
            .Select(machine => new MachineProjection(
                machine.Id,
                machine.Code,
                machine.Name,
                machine.Status,
                machine.WorkCenter == null ? null : machine.WorkCenter.Code,
                machine.WorkCenter == null ? null : machine.WorkCenter.Name,
                machine.UpdatedAt))
            .ToListAsync(cancellationToken);
        var machineIds = candidates.Select(machine => machine.Id).ToList();
        var operations = await dbContext.ProductionOrderOperations
            .AsNoTracking()
            .Where(operation => operation.CompanyId == companyId
                && operation.Status == ProductionOperationStatuses.InProgress
                && operation.MachineId.HasValue
                && machineIds.Contains(operation.MachineId.Value))
            .Select(operation => new ActiveOperationProjection(
                operation.Id,
                operation.MachineId,
                operation.WorkCenterId,
                operation.ProductionOrder!.Number,
                operation.Sequence,
                operation.Name,
                operation.WorkCenterCode,
                operation.WorkCenterName,
                operation.MachineCode,
                operation.Status,
                operation.SetupTimeMinutes,
                operation.RunTimeMinutes,
                operation.StartedAt,
                operation.CompletedAt))
            .ToListAsync(cancellationToken);
        var operationByMachine = operations
            .Where(operation => operation.MachineId.HasValue)
            .ToDictionary(operation => operation.MachineId!.Value);

        return Rank(question, candidates, item => item.Code, item => item.Name, limit)
            .Select(machine => {
                operationByMachine.TryGetValue(machine.Id, out var operation);
                var workCenter = machine.WorkCenterCode is null
                    ? "Work Center: none."
                    : $"Work Center: {machine.WorkCenterCode} - {machine.WorkCenterName}.";
                var execution = operation is null
                    ? " Current operation: none."
                    : $" Current operation: {operation.Name} ({operation.Status})."
                        + $" Production Order: {operation.ProductionOrderNumber}."
                        + $" Started: {FormatTimestamp(operation.StartedAt)}.";
                return new BusinessDataRecord(
                    machine.Id,
                    "machine",
                    $"{machine.Code} - {machine.Name}",
                    $"Status: {MachineStatusLabel(machine.Status)}. {workCenter}{execution} "
                    + $"Updated: {FormatTimestamp(machine.UpdatedAt)}.");
            })
            .ToList();
    }

    private async Task<IReadOnlyList<BusinessDataRecord>> MaterialsAsync(
        Guid companyId,
        string question,
        int limit,
        CancellationToken cancellationToken) {
        var exactTerms = ExactQueryTerms(question);
        var candidates = await dbContext.Materials.AsNoTracking()
            .Where(material => material.CompanyId == companyId)
            .OrderByDescending(material => exactTerms.Contains(material.Code.ToLower()))
            .ThenBy(material => material.Code)
            .Take(CandidateLimit(limit))
            .Select(material => new NamedProjection(material.Id, material.Code, material.Name, material.Unit))
            .ToListAsync(cancellationToken);
        return Rank(question, candidates, item => item.Code, item => item.Name, limit)
            .Select(material => new BusinessDataRecord(
                material.Id,
                "material",
                $"{material.Code} - {material.Name}",
                $"Unit: {material.Extra}."))
            .ToList();
    }

    private async Task<IReadOnlyList<BusinessDataRecord>> InventoryAsync(
        Guid companyId,
        string question,
        int limit,
        CancellationToken cancellationToken) {
        var exactTerms = ExactQueryTerms(question);
        var materialBalances = await dbContext.InventoryBalances.AsNoTracking()
            .Where(balance => balance.CompanyId == companyId)
            .OrderByDescending(balance => exactTerms.Contains(balance.Material!.Code.ToLower()))
            .ThenByDescending(balance => balance.Quantity)
            .ThenBy(balance => balance.Material!.Code)
            .Take(CandidateLimit(limit))
            .Select(balance => new InventoryProjection(
                balance.Id,
                balance.Material!.Code,
                balance.Material.Name,
                balance.Material.Unit,
                balance.Warehouse!.Code,
                balance.Warehouse.Name,
                balance.Quantity,
                balance.UpdatedAt,
                false))
            .ToListAsync(cancellationToken);
        var productBalances = await dbContext.ProductInventoryBalances.AsNoTracking()
            .Where(balance => balance.CompanyId == companyId)
            .OrderByDescending(balance => exactTerms.Contains(balance.Product!.Code.ToLower()))
            .ThenByDescending(balance => balance.Quantity)
            .ThenBy(balance => balance.Product!.Code)
            .Take(CandidateLimit(limit))
            .Select(balance => new InventoryProjection(
                balance.Id,
                balance.Product!.Code,
                balance.Product.Name,
                null,
                balance.Warehouse!.Code,
                balance.Warehouse.Name,
                balance.Quantity,
                balance.UpdatedAt,
                true))
            .ToListAsync(cancellationToken);

        return Rank(
                question,
                materialBalances.Concat(productBalances),
                item => item.Code,
                item => item.Name,
                limit)
            .Select(inventory => new BusinessDataRecord(
                inventory.Id,
                inventory.IsProduct ? "product_inventory" : "inventory",
                $"{inventory.Code} - {inventory.Name}",
                $"Warehouse: {inventory.WarehouseCode} - {inventory.WarehouseName}. "
                + $"Quantity: {Format(inventory.Quantity)}"
                + (inventory.Unit is null ? "." : $" {inventory.Unit}.")
                + $" Updated: {FormatTimestamp(inventory.UpdatedAt)}."))
            .ToList();
    }

    private async Task<IReadOnlyList<BusinessDataRecord>> ProductsAsync(
        Guid companyId,
        string question,
        int limit,
        CancellationToken cancellationToken) {
        var exactTerms = ExactQueryTerms(question);
        var candidates = await dbContext.Products.AsNoTracking()
            .Where(product => product.CompanyId == companyId)
            .OrderByDescending(product => exactTerms.Contains(product.Code.ToLower()))
            .ThenBy(product => product.Code)
            .Take(CandidateLimit(limit))
            .Select(product => new NamedProjection(product.Id, product.Code, product.Name, null))
            .ToListAsync(cancellationToken);
        var selected = Rank(question, candidates, item => item.Code, item => item.Name, limit);
        var productIds = selected.Select(product => product.Id).ToList();
        var activeBoms = await dbContext.BillOfMaterials
            .AsNoTracking()
            .Include(bom => bom.Items)
                .ThenInclude(item => item.Material)
            .Where(bom => bom.CompanyId == companyId
                && productIds.Contains(bom.ProductId)
                && bom.Status == BillOfMaterialStatuses.Active)
            .ToDictionaryAsync(bom => bom.ProductId, cancellationToken);

        return selected.Select(product => new BusinessDataRecord(
            product.Id,
            "product",
            $"{product.Code} - {product.Name}",
            ProductDetail(product.Id, activeBoms)))
            .ToList();
    }

    private async Task<IReadOnlyList<BusinessDataRecord>> ProductionOrdersAsync(
        Guid companyId,
        string question,
        string? status,
        int limit,
        CancellationToken cancellationToken) {
        var exactTerms = ExactQueryTerms(question);
        var candidates = await dbContext.ProductionOrders.AsNoTracking()
            .Where(order => order.CompanyId == companyId)
            .Where(order => status == null || order.Status == status)
            .OrderByDescending(order => exactTerms.Contains(order.Number.ToLower()))
            .ThenByDescending(order => order.UpdatedAt)
            .Take(CandidateLimit(limit))
            .Select(order => new ProductionOrderProjection(
                order.Id,
                order.Number,
                order.Product!.Code,
                order.Product.Name,
                order.Quantity,
                order.Status,
                order.Priority,
                order.DueDate,
                order.BillOfMaterial == null ? null : order.BillOfMaterial.Revision,
                order.Routing == null ? null : order.Routing.Revision,
                order.ReleasedAt,
                order.StartedAt,
                order.CompletedAt,
                order.UpdatedAt))
            .ToListAsync(cancellationToken);
        var selected = Rank(
            question,
            candidates,
            item => item.Number,
            item => $"{item.ProductCode} {item.ProductName}",
            limit);
        var orderIds = selected.Select(order => order.Id).ToList();
        var operations = await dbContext.ProductionOrderOperations.AsNoTracking()
            .Where(operation => operation.CompanyId == companyId
                && orderIds.Contains(operation.ProductionOrderId))
            .OrderBy(operation => operation.Sequence)
            .Select(operation => new ActiveOperationProjection(
                operation.Id,
                operation.MachineId,
                operation.WorkCenterId,
                operation.ProductionOrder!.Number,
                operation.Sequence,
                operation.Name,
                operation.WorkCenterCode,
                operation.WorkCenterName,
                operation.MachineCode,
                operation.Status,
                operation.SetupTimeMinutes,
                operation.RunTimeMinutes,
                operation.StartedAt,
                operation.CompletedAt))
            .ToListAsync(cancellationToken);
        var operationsByOrder = operations.ToLookup(operation => operation.ProductionOrderNumber);
        var now = riskCalculator.UtcNow;

        return selected.Select(order => {
            var risk = ProductionOrderDeliveryRiskCalculator.Calculate(
                order.Status,
                order.DueDate,
                order.CompletedAt,
                now,
                riskCalculator.DueSoonDays);
            var orderOperations = operationsByOrder[order.Number].OrderBy(operation => operation.Sequence).ToList();
            var current = orderOperations.FirstOrDefault(operation =>
                operation.Status == ProductionOperationStatuses.InProgress);
            var next = orderOperations.FirstOrDefault(operation =>
                operation.Status == ProductionOperationStatuses.Pending);
            var execution = current is not null
                ? $" Current operation: {OperationDetail(current)}"
                : next is not null
                    ? $" Next pending operation: {OperationDetail(next)}"
                    : string.Empty;
            return new BusinessDataRecord(
                order.Id,
                "production_order",
                $"{order.Number} - {order.ProductCode} {order.ProductName}",
                $"Quantity: {Format(order.Quantity)}. Status: {order.Status}. "
                + $"Priority: {order.Priority}. Due Date: {FormatDate(order.DueDate)}. "
                + $"Delivery Status: {risk.DeliveryStatus}. Days Until Due: {risk.DaysUntilDue?.ToString(CultureInfo.InvariantCulture) ?? "none"}. "
                + $"Locked BOM revision: {NullableRevision(order.BomRevision)}. "
                + $"Locked Routing revision: {NullableRevision(order.RoutingRevision)}. "
                + $"Released: {FormatTimestamp(order.ReleasedAt)}. "
                + $"Started: {FormatTimestamp(order.StartedAt)}. "
                + $"Completed: {FormatTimestamp(order.CompletedAt)}.{execution}");
        }).ToList();
    }

    private async Task<IReadOnlyList<BusinessDataRecord>> WorkCentersAsync(
        Guid companyId,
        string question,
        int limit,
        CancellationToken cancellationToken) {
        var exactTerms = ExactQueryTerms(question);
        var candidates = await dbContext.WorkCenters.AsNoTracking()
            .Where(workCenter => workCenter.CompanyId == companyId)
            .OrderByDescending(workCenter => exactTerms.Contains(workCenter.Code.ToLower()))
            .ThenBy(workCenter => workCenter.Code)
            .Take(CandidateLimit(limit))
            .Select(workCenter => new WorkCenterProjection(
                workCenter.Id,
                workCenter.Code,
                workCenter.Name,
                workCenter.IsActive,
                workCenter.ParallelCapacity,
                workCenter.Shifts.Count,
                workCenter.DaysOff.Count))
            .ToListAsync(cancellationToken);
        var selected = Rank(question, candidates, item => item.Code, item => item.Name, limit);
        var workCenterIds = selected.Select(workCenter => workCenter.Id).ToList();
        var machines = await dbContext.Machines.AsNoTracking()
            .Where(machine => machine.CompanyId == companyId
                && machine.WorkCenterId.HasValue
                && workCenterIds.Contains(machine.WorkCenterId.Value))
            .Select(machine => new WorkCenterMachineProjection(
                machine.Id,
                machine.WorkCenterId!.Value,
                machine.Code,
                machine.Status))
            .ToListAsync(cancellationToken);
        var operations = await dbContext.ProductionOrderOperations.AsNoTracking()
            .Where(operation => operation.CompanyId == companyId
                && workCenterIds.Contains(operation.WorkCenterId)
                && operation.Status == ProductionOperationStatuses.InProgress)
            .Select(operation => new ActiveOperationProjection(
                operation.Id,
                operation.MachineId,
                operation.WorkCenterId,
                operation.ProductionOrder!.Number,
                operation.Sequence,
                operation.Name,
                operation.WorkCenterCode,
                operation.WorkCenterName,
                operation.MachineCode,
                operation.Status,
                operation.SetupTimeMinutes,
                operation.RunTimeMinutes,
                operation.StartedAt,
                operation.CompletedAt))
            .ToListAsync(cancellationToken);

        return selected.Select(workCenter => {
            var assignedMachines = machines.Where(machine => machine.WorkCenterId == workCenter.Id).ToList();
            var summary = string.Join(", ", assignedMachines
                .GroupBy(machine => machine.Status)
                .OrderBy(group => group.Key)
                .Select(group => $"{group.Count()} {group.Key}"));
            var current = operations
                .Where(operation => operation.WorkCenterId == workCenter.Id)
                .OrderBy(operation => operation.ProductionOrderNumber)
                .Take(3)
                .Select(operation =>
                    $"{operation.ProductionOrderNumber} / {operation.Name} / {operation.MachineCode ?? "no machine"}");
            var currentText = string.Join("; ", current);
            return new BusinessDataRecord(
                workCenter.Id,
                "work_center",
                $"{workCenter.Code} - {workCenter.Name}",
                $"Active: {workCenter.IsActive}. Machines: {assignedMachines.Count}"
                + $". Parallel planning capacity: {workCenter.ParallelCapacity}. Weekly shifts: {workCenter.ShiftCount}. Days off configured: {workCenter.DayOffCount}"
                + (summary.Length == 0 ? "." : $" ({summary}).")
                + (currentText.Length == 0 ? " Current operations: none." : $" Current operations: {currentText}."));
        }).ToList();
    }

    private async Task<IReadOnlyList<BusinessDataRecord>> RoutingsAsync(
        Guid companyId,
        string question,
        int limit,
        CancellationToken cancellationToken) {
        var exactTerms = ExactQueryTerms(question);
        var candidates = await dbContext.Routings.AsNoTracking()
            .Where(routing => routing.CompanyId == companyId)
            .OrderByDescending(routing => exactTerms.Contains(routing.Product!.Code.ToLower()))
            .ThenByDescending(routing => routing.Status == RoutingStatuses.Active)
            .ThenByDescending(routing => routing.Revision)
            .Take(CandidateLimit(limit))
            .Select(routing => new RoutingProjection(
                routing.Id,
                routing.ProductId,
                routing.Product!.Code,
                routing.Product.Name,
                routing.Revision,
                routing.Status))
            .ToListAsync(cancellationToken);
        var selected = Rank(
            question,
            candidates,
            item => item.ProductCode,
            item => item.ProductName,
            limit);
        var routingIds = selected.Select(routing => routing.Id).ToList();
        var operations = await dbContext.RoutingOperations.AsNoTracking()
            .Where(operation => routingIds.Contains(operation.RoutingId))
            .OrderBy(operation => operation.Sequence)
            .Select(operation => new RoutingOperationProjection(
                operation.RoutingId,
                operation.Sequence,
                operation.Name,
                operation.WorkCenter!.Code,
                operation.WorkCenter.Name,
                operation.SetupTimeMinutes,
                operation.RunTimeMinutes))
            .ToListAsync(cancellationToken);
        var byRouting = operations.ToLookup(operation => operation.RoutingId);

        return selected.Select(routing => {
            var steps = string.Join("; ", byRouting[routing.Id]
                .Take(8)
                .Select(operation =>
                    $"{operation.Sequence} {operation.Name} @ {operation.WorkCenterCode} "
                    + $"(setup {operation.SetupTimeMinutes}m, run {operation.RunTimeMinutes}m)"));
            return new BusinessDataRecord(
                routing.Id,
                "routing",
                $"{routing.ProductCode} - {routing.ProductName} / revision {routing.Revision}",
                $"Status: {routing.Status}. Operations: {steps}.");
        }).ToList();
    }

    private async Task<IReadOnlyList<BusinessDataRecord>> ProductionOperationsAsync(
        Guid companyId,
        string question,
        int limit,
        CancellationToken cancellationToken) {
        var exactTerms = ExactQueryTerms(question);
        var candidates = await dbContext.ProductionOrderOperations.AsNoTracking()
            .Where(operation => operation.CompanyId == companyId)
            .OrderByDescending(operation =>
                exactTerms.Contains(operation.ProductionOrder!.Number.ToLower())
                || (operation.MachineCode != null && exactTerms.Contains(operation.MachineCode.ToLower()))
                || exactTerms.Contains(operation.WorkCenterCode.ToLower()))
            .ThenByDescending(operation => operation.Status == ProductionOperationStatuses.InProgress)
            .ThenBy(operation => operation.ProductionOrder!.Number)
            .ThenBy(operation => operation.Sequence)
            .Take(CandidateLimit(limit))
            .Select(operation => new ActiveOperationProjection(
                operation.Id,
                operation.MachineId,
                operation.WorkCenterId,
                operation.ProductionOrder!.Number,
                operation.Sequence,
                operation.Name,
                operation.WorkCenterCode,
                operation.WorkCenterName,
                operation.MachineCode,
                operation.Status,
                operation.SetupTimeMinutes,
                operation.RunTimeMinutes,
                operation.StartedAt,
                operation.CompletedAt))
            .ToListAsync(cancellationToken);

        return candidates
            .Select(operation => new {
                Operation = operation,
                Score = new[] {
                    BusinessEntityRanker.Score(question, operation.ProductionOrderNumber, operation.Name),
                    BusinessEntityRanker.Score(question, operation.WorkCenterCode, operation.WorkCenterName),
                    BusinessEntityRanker.Score(question, operation.MachineCode ?? string.Empty, operation.Name)
                }.Max()
            })
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Operation.Status == ProductionOperationStatuses.InProgress)
            .ThenBy(item => item.Operation.ProductionOrderNumber)
            .ThenBy(item => item.Operation.Sequence)
            .Take(limit)
            .Select(item => new BusinessDataRecord(
                item.Operation.Id,
                "production_operation",
                $"{item.Operation.ProductionOrderNumber} / {item.Operation.Sequence} {item.Operation.Name}",
                $"Status: {item.Operation.Status}. "
                + $"Work Center: {item.Operation.WorkCenterCode} - {item.Operation.WorkCenterName}. "
                + $"Machine: {item.Operation.MachineCode ?? "none"}. "
                + $"Setup estimate: {item.Operation.SetupTimeMinutes} minutes. "
                + $"Run estimate: {item.Operation.RunTimeMinutes} minutes. "
                + $"Started: {FormatTimestamp(item.Operation.StartedAt)}. "
                + $"Completed: {FormatTimestamp(item.Operation.CompletedAt)}."))
            .ToList();
    }

    private static IReadOnlyList<T> Rank<T>(
        string question,
        IEnumerable<T> candidates,
        Func<T, string> code,
        Func<T, string> name,
        int limit) => candidates
        .OrderByDescending(item => BusinessEntityRanker.Score(question, code(item), name(item)))
        .ThenBy(code, StringComparer.OrdinalIgnoreCase)
        .Take(limit)
        .ToList();

    private static int CandidateLimit(int resultLimit) => Math.Max(MinimumCandidateLimit, resultLimit * 8);

    private static List<string> ExactQueryTerms(string question) => SearchTextNormalizer.Tokens(question)
        .Concat(SearchTextNormalizer.Identifiers(question))
        .Distinct(StringComparer.Ordinal)
        .ToList();

    private static string Format(decimal value) => value.ToString("0.######", CultureInfo.InvariantCulture);

    private static string ProductDetail(
        Guid productId,
        IReadOnlyDictionary<Guid, BillOfMaterial> activeBoms) {
        if (!activeBoms.TryGetValue(productId, out var bom)) {
            return "Product has no active BOM.";
        }

        var components = string.Join("; ", bom.Items
            .OrderBy(item => item.Material!.Code)
            .Take(12)
            .Select(item =>
                $"{item.Material!.Code} {Format(item.Quantity)} {item.Material.Unit}"
                + (item.ScrapPercentage.HasValue
                    ? $" (scrap {Format(item.ScrapPercentage.Value)}%)"
                    : string.Empty)));
        return $"Active BOM revision {bom.Revision}, output {Format(bom.OutputQuantity)}. Components: {components}.";
    }

    private static string OperationDetail(ActiveOperationProjection operation) =>
        $"{operation.Sequence} {operation.Name} ({operation.Status}); "
        + $"Work Center {operation.WorkCenterCode}; Machine {operation.MachineCode ?? "none"}; "
        + $"Started {FormatTimestamp(operation.StartedAt)}; Completed {FormatTimestamp(operation.CompletedAt)}.";

    private static string NullableRevision(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "none";

    private static string FormatTimestamp(DateTime? value) => value.HasValue
        ? value.Value.ToUniversalTime().ToString("dd/MM/yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture)
        : "none";

    private static string FormatDate(DateTime? value) => value.HasValue
        ? value.Value.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        : "none";

    private static string MachineStatusLabel(string status) => status switch {
        MachineStatuses.Available => "Available",
        MachineStatuses.Running => "Running",
        MachineStatuses.Maintenance => "Maintenance",
        MachineStatuses.Offline => "Offline",
        _ => status
    };

    private sealed record NamedProjection(Guid Id, string Code, string Name, string? Extra);
    private sealed record MachineProjection(
        Guid Id,
        string Code,
        string Name,
        string Status,
        string? WorkCenterCode,
        string? WorkCenterName,
        DateTime UpdatedAt);
    private sealed record InventoryProjection(
        Guid Id,
        string Code,
        string Name,
        string? Unit,
        string WarehouseCode,
        string WarehouseName,
        decimal Quantity,
        DateTime UpdatedAt,
        bool IsProduct);
    private sealed record ProductionOrderProjection(
        Guid Id,
        string Number,
        string ProductCode,
        string ProductName,
        decimal Quantity,
        string Status,
        string Priority,
        DateTime? DueDate,
        int? BomRevision,
        int? RoutingRevision,
        DateTime? ReleasedAt,
        DateTime? StartedAt,
        DateTime? CompletedAt,
        DateTime UpdatedAt);
    private sealed record WorkCenterProjection(
        Guid Id,
        string Code,
        string Name,
        bool IsActive,
        int ParallelCapacity,
        int ShiftCount,
        int DayOffCount);
    private sealed record WorkCenterMachineProjection(Guid Id, Guid WorkCenterId, string Code, string Status);
    private sealed record RoutingProjection(
        Guid Id,
        Guid ProductId,
        string ProductCode,
        string ProductName,
        int Revision,
        string Status);
    private sealed record RoutingOperationProjection(
        Guid RoutingId,
        int Sequence,
        string Name,
        string WorkCenterCode,
        string WorkCenterName,
        int SetupTimeMinutes,
        int RunTimeMinutes);
    private sealed record ActiveOperationProjection(
        Guid Id,
        Guid? MachineId,
        Guid WorkCenterId,
        string ProductionOrderNumber,
        int Sequence,
        string Name,
        string WorkCenterCode,
        string WorkCenterName,
        string? MachineCode,
        string Status,
        int SetupTimeMinutes,
        int RunTimeMinutes,
        DateTime? StartedAt,
        DateTime? CompletedAt);
}
