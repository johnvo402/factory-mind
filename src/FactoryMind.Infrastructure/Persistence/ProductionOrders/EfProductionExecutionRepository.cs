using System.Data;
using FactoryMind.Application.Features.ProductionOrders;
using FactoryMind.Domain.Manufacturing;
using Microsoft.EntityFrameworkCore;

namespace FactoryMind.Infrastructure.Persistence.ProductionOrders;

public sealed class EfProductionExecutionRepository(FactoryMindDbContext dbContext)
    : IProductionExecutionRepository {
    public Task<ProductionOrder?> GetAsync(
        Guid productionOrderId,
        Guid companyId,
        CancellationToken cancellationToken) => GetOrderQuery()
        .SingleOrDefaultAsync(
            order => order.Id == productionOrderId && order.CompanyId == companyId,
            cancellationToken);

    public async Task<ProductionOrderReleaseSnapshot?> GetReleaseSnapshotByNumberAsync(
        string number,
        Guid companyId,
        CancellationToken cancellationToken) {
        var order = await dbContext.ProductionOrders.AsNoTracking()
            .Where(candidate => candidate.CompanyId == companyId && candidate.Number == number)
            .Select(candidate => new {
                candidate.Id,
                candidate.Number,
                candidate.Status,
                candidate.ProductId,
                ProductCode = candidate.Product!.Code,
                ProductName = candidate.Product.Name,
                candidate.Quantity
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (order is null) {
            return null;
        }

        var bom = await dbContext.BillOfMaterials.AsNoTracking()
            .Where(candidate => candidate.CompanyId == companyId
                && candidate.ProductId == order.ProductId
                && candidate.Status == BillOfMaterialStatuses.Active)
            .Select(candidate => new { candidate.Id, candidate.Revision })
            .SingleOrDefaultAsync(cancellationToken);
        var routing = await dbContext.Routings.AsNoTracking()
            .Where(candidate => candidate.CompanyId == companyId
                && candidate.ProductId == order.ProductId
                && candidate.Status == RoutingStatuses.Active)
            .Select(candidate => new { candidate.Id, candidate.Revision })
            .SingleOrDefaultAsync(cancellationToken);
        var hasOperations = routing is not null && await dbContext.RoutingOperations.AsNoTracking()
            .AnyAsync(operation => operation.RoutingId == routing.Id, cancellationToken);
        var workCentersAvailable = routing is not null && hasOperations
            && !await dbContext.RoutingOperations.AsNoTracking()
                .Where(operation => operation.RoutingId == routing.Id)
                .AnyAsync(operation => !dbContext.WorkCenters.Any(workCenter =>
                    workCenter.Id == operation.WorkCenterId
                    && workCenter.CompanyId == companyId
                    && workCenter.IsActive), cancellationToken);

        return new ProductionOrderReleaseSnapshot(
            order.Id,
            order.Number,
            order.Status,
            order.ProductId,
            order.ProductCode,
            order.ProductName,
            order.Quantity,
            bom?.Id,
            bom?.Revision,
            routing?.Id,
            routing?.Revision,
            hasOperations,
            workCentersAvailable);
    }

    public async Task<ProductionExecutionResult> TryReleaseAsync(
        Guid productionOrderId,
        Guid companyId,
        DateTime releasedAt,
        ProductionOrderReleaseExpectation? expectation,
        CancellationToken cancellationToken) {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        var order = await dbContext.ProductionOrders
            .FromSqlInterpolated($"""
                SELECT * FROM production_orders
                WHERE "Id" = {productionOrderId}
                  AND "CompanyId" = {companyId}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);
        if (order is null || order.Status != ProductionOrderStatuses.Planned) {
            await transaction.RollbackAsync(cancellationToken);
            return new(ProductionExecutionStatus.StateConflict, null);
        }

        if (expectation is not null
            && (order.Number != expectation.Number
                || order.Status != expectation.Status
                || order.ProductId != expectation.ProductId
                || order.Quantity != expectation.Quantity)) {
            await transaction.RollbackAsync(cancellationToken);
            return new(ProductionExecutionStatus.SnapshotStale, null);
        }

        var bom = await dbContext.BillOfMaterials
            .FromSqlInterpolated($"""
                SELECT * FROM bill_of_materials
                WHERE "CompanyId" = {companyId}
                  AND "ProductId" = {order.ProductId}
                  AND "Status" = {BillOfMaterialStatuses.Active}
                FOR SHARE
                """)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (bom is null) {
            await transaction.RollbackAsync(cancellationToken);
            return new(ProductionExecutionStatus.ActiveBomNotFound, null);
        }

        if (expectation is not null && bom.Id != expectation.ActiveBillOfMaterialId) {
            await transaction.RollbackAsync(cancellationToken);
            return new(ProductionExecutionStatus.SnapshotStale, null);
        }

        var routing = await dbContext.Routings
            .FromSqlInterpolated($"""
                SELECT * FROM routings
                WHERE "CompanyId" = {companyId}
                  AND "ProductId" = {order.ProductId}
                  AND "Status" = {RoutingStatuses.Active}
                FOR SHARE
                """)
            .SingleOrDefaultAsync(cancellationToken);
        if (routing is null) {
            await transaction.RollbackAsync(cancellationToken);
            return new(ProductionExecutionStatus.ActiveRoutingNotFound, null);
        }
        if (expectation is not null && routing.Id != expectation.ActiveRoutingId) {
            await transaction.RollbackAsync(cancellationToken);
            return new(ProductionExecutionStatus.SnapshotStale, null);
        }
        await dbContext.Entry(routing).Collection(candidate => candidate.Operations)
            .LoadAsync(cancellationToken);
        if (routing.Operations.Count == 0) {
            await transaction.RollbackAsync(cancellationToken);
            return new(ProductionExecutionStatus.ActiveRoutingNotFound, null);
        }

        var workCenters = new Dictionary<Guid, WorkCenter>();
        foreach (var workCenterId in routing.Operations.Select(operation => operation.WorkCenterId).Distinct().Order()) {
            var workCenter = await dbContext.WorkCenters
                .FromSqlInterpolated($"""
                    SELECT * FROM work_centers
                    WHERE "Id" = {workCenterId}
                      AND "CompanyId" = {companyId}
                      AND "IsActive" = TRUE
                    FOR SHARE
                    """)
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);
            if (workCenter is null) {
                await transaction.RollbackAsync(cancellationToken);
                return new(ProductionExecutionStatus.RoutingWorkCenterUnavailable, null);
            }
            workCenters.Add(workCenter.Id, workCenter);
        }

        var snapshots = routing.Operations.OrderBy(operation => operation.Sequence)
            .Select(operation => new ProductionOrderOperation {
                CompanyId = companyId,
                ProductionOrderId = order.Id,
                RoutingOperationId = operation.Id,
                Sequence = operation.Sequence,
                Name = operation.Name,
                WorkCenterId = operation.WorkCenterId,
                WorkCenterCode = workCenters[operation.WorkCenterId].Code,
                WorkCenterName = workCenters[operation.WorkCenterId].Name,
                SetupTimeMinutes = operation.SetupTimeMinutes,
                RunTimeMinutes = operation.RunTimeMinutes,
                Description = operation.Description,
                Status = ProductionOperationStatuses.Pending,
                CreatedAt = releasedAt
            })
            .ToList();
        order.BillOfMaterialId = bom.Id;
        order.RoutingId = routing.Id;
        order.Status = ProductionOrderStatuses.Released;
        order.ReleasedAt = releasedAt;
        order.UpdatedAt = releasedAt;
        dbContext.ProductionOrderOperations.AddRange(snapshots);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(ProductionExecutionStatus.Success, await GetAsync(
            productionOrderId, companyId, cancellationToken));
    }

    public async Task<ProductionExecutionResult> TryStartAsync(
        Guid productionOrderId,
        Guid companyId,
        IReadOnlyList<InventoryTransaction> consumptionTransactions,
        DateTime startedAt,
        CancellationToken cancellationToken) {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        var claimed = await dbContext.ProductionOrders
            .Where(order => order.Id == productionOrderId &&
                order.CompanyId == companyId &&
                order.Status == ProductionOrderStatuses.Released &&
                order.BillOfMaterialId != null &&
                ((order.RoutingId == null && !dbContext.ProductionOrderOperations.Any(operation =>
                    operation.ProductionOrderId == order.Id && operation.CompanyId == companyId)) ||
                 (order.RoutingId != null && dbContext.ProductionOrderOperations.Any(operation =>
                     operation.ProductionOrderId == order.Id && operation.CompanyId == companyId))))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(order => order.Status, ProductionOrderStatuses.InProgress)
                .SetProperty(order => order.StartedAt, startedAt)
                .SetProperty(order => order.UpdatedAt, startedAt), cancellationToken);
        if (claimed != 1) {
            await transaction.RollbackAsync(cancellationToken);
            return new(ProductionExecutionStatus.StateConflict, null);
        }

        foreach (var warehouseId in consumptionTransactions
                     .Select(item => item.WarehouseId)
                     .Distinct()
                     .Order()) {
            var warehouse = await dbContext.Warehouses
                .FromSqlInterpolated($"""
                    SELECT * FROM warehouses
                    WHERE "Id" = {warehouseId}
                      AND "CompanyId" = {companyId}
                      AND "IsActive" = TRUE
                    FOR SHARE
                    """)
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);
            if (warehouse is null) {
                await transaction.RollbackAsync(cancellationToken);
                return new(ProductionExecutionStatus.WarehouseUnavailable, null);
            }
        }

        foreach (var materialId in consumptionTransactions
                     .Select(item => item.MaterialId)
                     .Distinct()
                     .Order()) {
            var material = await dbContext.Materials
                .FromSqlInterpolated($"""
                    SELECT * FROM materials
                    WHERE "Id" = {materialId}
                      AND "CompanyId" = {companyId}
                    FOR SHARE
                    """)
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);
            if (material is null) {
                await transaction.RollbackAsync(cancellationToken);
                return new(ProductionExecutionStatus.MaterialUnavailable, null);
            }
        }

        foreach (var consumption in consumptionTransactions
                     .OrderBy(item => item.MaterialId)
                     .ThenBy(item => item.WarehouseId)) {
            var affected = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE inventory_balances
                SET "Quantity" = "Quantity" - {consumption.Quantity},
                    "UpdatedAt" = {startedAt}
                WHERE "CompanyId" = {companyId}
                  AND "WarehouseId" = {consumption.WarehouseId}
                  AND "MaterialId" = {consumption.MaterialId}
                  AND "Quantity" >= {consumption.Quantity}
                """, cancellationToken);
            if (affected != 1) {
                await transaction.RollbackAsync(cancellationToken);
                return new(ProductionExecutionStatus.InsufficientStock, null);
            }
        }

        dbContext.InventoryTransactions.AddRange(consumptionTransactions);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(ProductionExecutionStatus.Success, await GetAsync(
            productionOrderId,
            companyId,
            cancellationToken));
    }

    public async Task<ProductionExecutionResult> TryCancelAsync(
        Guid productionOrderId,
        Guid companyId,
        DateTime cancelledAt,
        CancellationToken cancellationToken) {
        var affected = await dbContext.ProductionOrders
            .Where(order => order.Id == productionOrderId &&
                order.CompanyId == companyId &&
                (order.Status == ProductionOrderStatuses.Planned ||
                 order.Status == ProductionOrderStatuses.Released))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(order => order.Status, ProductionOrderStatuses.Cancelled)
                .SetProperty(order => order.CancelledAt, cancelledAt)
                .SetProperty(order => order.UpdatedAt, cancelledAt), cancellationToken);
        return affected == 1
            ? new(ProductionExecutionStatus.Success, await GetAsync(
                productionOrderId,
                companyId,
                cancellationToken))
            : new(ProductionExecutionStatus.StateConflict, null);
    }

    public async Task<ProductionExecutionResult> TryCompleteAsync(
        Guid productionOrderId,
        Guid companyId,
        ProductInventoryTransaction outputTransaction,
        DateTime completedAt,
        CancellationToken cancellationToken) {
        if (outputTransaction.CompanyId != companyId ||
            outputTransaction.Quantity <= 0 ||
            outputTransaction.Type != ProductInventoryTransactionType.ProductionOutput ||
            outputTransaction.ReferenceType != "ProductionOrder" ||
            outputTransaction.ReferenceId != productionOrderId) {
            return new(ProductionExecutionStatus.StateConflict, null);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        var claimed = await dbContext.ProductionOrders
            .Where(order => order.Id == productionOrderId &&
                order.CompanyId == companyId &&
                order.Status == ProductionOrderStatuses.InProgress &&
                order.BillOfMaterialId != null &&
                order.StartedAt != null &&
                order.ProductId == outputTransaction.ProductId &&
                order.Quantity == outputTransaction.Quantity &&
                ((order.RoutingId == null && !dbContext.ProductionOrderOperations.Any(operation =>
                    operation.ProductionOrderId == order.Id && operation.CompanyId == companyId)) ||
                 (order.RoutingId != null && dbContext.ProductionOrderOperations.Any(operation =>
                     operation.ProductionOrderId == order.Id && operation.CompanyId == companyId) &&
                  !dbContext.ProductionOrderOperations.Any(operation =>
                      operation.ProductionOrderId == order.Id &&
                      operation.CompanyId == companyId &&
                      operation.Status != ProductionOperationStatuses.Completed))))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(order => order.Status, ProductionOrderStatuses.Completed)
                .SetProperty(order => order.CompletedAt, completedAt)
                .SetProperty(order => order.UpdatedAt, completedAt), cancellationToken);
        if (claimed != 1) {
            var operationsIncomplete = await dbContext.ProductionOrders.AsNoTracking().AnyAsync(
                order => order.Id == productionOrderId &&
                    order.CompanyId == companyId &&
                    order.Status == ProductionOrderStatuses.InProgress &&
                    order.RoutingId != null &&
                    dbContext.ProductionOrderOperations.Any(operation =>
                        operation.ProductionOrderId == order.Id &&
                        operation.CompanyId == companyId &&
                        operation.Status != ProductionOperationStatuses.Completed),
                cancellationToken);
            await transaction.RollbackAsync(cancellationToken);
            return new(
                operationsIncomplete
                    ? ProductionExecutionStatus.OperationsIncomplete
                    : ProductionExecutionStatus.StateConflict,
                null);
        }

        var warehouse = await dbContext.Warehouses
            .FromSqlInterpolated($"""
                SELECT * FROM warehouses
                WHERE "Id" = {outputTransaction.WarehouseId}
                  AND "CompanyId" = {companyId}
                  AND "IsActive" = TRUE
                FOR SHARE
                """)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (warehouse is null) {
            await transaction.RollbackAsync(cancellationToken);
            return new(ProductionExecutionStatus.WarehouseUnavailable, null);
        }

        var product = await dbContext.Products
            .FromSqlInterpolated($"""
                SELECT * FROM products
                WHERE "Id" = {outputTransaction.ProductId}
                  AND "CompanyId" = {companyId}
                FOR SHARE
                """)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (product is null) {
            await transaction.RollbackAsync(cancellationToken);
            return new(ProductionExecutionStatus.ProductUnavailable, null);
        }

        var balanceId = Guid.NewGuid();
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO product_inventory_balances
                ("Id", "CompanyId", "WarehouseId", "ProductId", "Quantity", "UpdatedAt")
            VALUES
                ({balanceId}, {companyId}, {outputTransaction.WarehouseId},
                 {outputTransaction.ProductId}, {outputTransaction.Quantity}, {completedAt})
            ON CONFLICT ("CompanyId", "WarehouseId", "ProductId")
            DO UPDATE SET
                "Quantity" = product_inventory_balances."Quantity" + EXCLUDED."Quantity",
                "UpdatedAt" = EXCLUDED."UpdatedAt"
            """, cancellationToken);

        dbContext.ProductInventoryTransactions.Add(outputTransaction);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(ProductionExecutionStatus.Success, await GetAsync(
            productionOrderId,
            companyId,
            cancellationToken));
    }

    public async Task<IReadOnlyList<ProductionOrderOperation>> GetOperationsAsync(
        Guid productionOrderId,
        Guid companyId,
        CancellationToken cancellationToken) => await dbContext.ProductionOrderOperations
        .AsNoTracking()
        .Where(operation => operation.ProductionOrderId == productionOrderId &&
            operation.CompanyId == companyId)
        .OrderBy(operation => operation.Sequence)
        .ToListAsync(cancellationToken);

    public async Task<ProductionOperationExecutionResult> TryStartOperationAsync(
        Guid productionOrderId,
        Guid operationId,
        Guid machineId,
        Guid companyId,
        DateTime startedAt,
        CancellationToken cancellationToken) {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        var order = await dbContext.ProductionOrders
            .FromSqlInterpolated($"""
                SELECT * FROM production_orders
                WHERE "Id" = {productionOrderId}
                  AND "CompanyId" = {companyId}
                FOR UPDATE
                """)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (order is null || order.Status != ProductionOrderStatuses.InProgress) {
            await transaction.RollbackAsync(cancellationToken);
            return new(ProductionExecutionStatus.StateConflict, null);
        }

        var operation = await dbContext.ProductionOrderOperations
            .FromSqlInterpolated($"""
                SELECT * FROM production_order_operations
                WHERE "Id" = {operationId}
                  AND "ProductionOrderId" = {productionOrderId}
                  AND "CompanyId" = {companyId}
                FOR UPDATE
                """)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (operation is null || operation.Status != ProductionOperationStatuses.Pending) {
            await transaction.RollbackAsync(cancellationToken);
            return new(ProductionExecutionStatus.StateConflict, null);
        }

        var blockedBySequence = await dbContext.ProductionOrderOperations.AsNoTracking().AnyAsync(
            candidate => candidate.ProductionOrderId == productionOrderId &&
                candidate.CompanyId == companyId &&
                ((candidate.Status == ProductionOperationStatuses.InProgress) ||
                 (candidate.Sequence < operation.Sequence &&
                  candidate.Status != ProductionOperationStatuses.Completed)),
            cancellationToken);
        if (blockedBySequence) {
            await transaction.RollbackAsync(cancellationToken);
            return new(ProductionExecutionStatus.StateConflict, null);
        }

        var workCenterAvailable = await dbContext.WorkCenters
            .FromSqlInterpolated($"""
                SELECT * FROM work_centers
                WHERE "Id" = {operation.WorkCenterId}
                  AND "CompanyId" = {companyId}
                  AND "IsActive" = TRUE
                FOR SHARE
                """)
            .AsNoTracking()
            .AnyAsync(cancellationToken);
        if (!workCenterAvailable) {
            await transaction.RollbackAsync(cancellationToken);
            return new(ProductionExecutionStatus.RoutingWorkCenterUnavailable, null);
        }

        var machineExists = await dbContext.Machines.AsNoTracking().AnyAsync(
            machine => machine.Id == machineId && machine.CompanyId == companyId,
            cancellationToken);
        if (!machineExists) {
            await transaction.RollbackAsync(cancellationToken);
            return new(ProductionExecutionStatus.MachineNotFound, null);
        }

        var claimedMachine = await dbContext.Machines
            .Where(machine => machine.Id == machineId &&
                machine.CompanyId == companyId &&
                machine.WorkCenterId == operation.WorkCenterId &&
                machine.Status == MachineStatuses.Available &&
                !dbContext.ProductionOrderOperations.Any(candidate =>
                    candidate.CompanyId == companyId &&
                    candidate.MachineId == machine.Id &&
                    candidate.Status == ProductionOperationStatuses.InProgress))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(machine => machine.Status, MachineStatuses.Running)
                .SetProperty(machine => machine.UpdatedAt, startedAt), cancellationToken);
        if (claimedMachine != 1) {
            await transaction.RollbackAsync(cancellationToken);
            return new(ProductionExecutionStatus.StateConflict, null);
        }

        var machineSnapshot = await dbContext.Machines.AsNoTracking().SingleAsync(
            machine => machine.Id == machineId && machine.CompanyId == companyId,
            cancellationToken);
        var startedOperation = await dbContext.ProductionOrderOperations
            .Where(candidate => candidate.Id == operationId &&
                candidate.ProductionOrderId == productionOrderId &&
                candidate.CompanyId == companyId &&
                candidate.Status == ProductionOperationStatuses.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(candidate => candidate.Status, ProductionOperationStatuses.InProgress)
                .SetProperty(candidate => candidate.MachineId, machineSnapshot.Id)
                .SetProperty(candidate => candidate.MachineCode, machineSnapshot.Code)
                .SetProperty(candidate => candidate.MachineName, machineSnapshot.Name)
                .SetProperty(candidate => candidate.StartedAt, startedAt), cancellationToken);
        if (startedOperation != 1) {
            await transaction.RollbackAsync(cancellationToken);
            return new(ProductionExecutionStatus.StateConflict, null);
        }

        await transaction.CommitAsync(cancellationToken);
        return new(ProductionExecutionStatus.Success, await GetOperationAsync(
            productionOrderId, operationId, companyId, cancellationToken));
    }

    public async Task<ProductionOperationExecutionResult> TryCompleteOperationAsync(
        Guid productionOrderId,
        Guid operationId,
        Guid companyId,
        DateTime completedAt,
        CancellationToken cancellationToken) {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        var order = await dbContext.ProductionOrders
            .FromSqlInterpolated($"""
                SELECT * FROM production_orders
                WHERE "Id" = {productionOrderId}
                  AND "CompanyId" = {companyId}
                FOR UPDATE
                """)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (order is null || order.Status != ProductionOrderStatuses.InProgress) {
            await transaction.RollbackAsync(cancellationToken);
            return new(ProductionExecutionStatus.StateConflict, null);
        }

        var operation = await dbContext.ProductionOrderOperations
            .FromSqlInterpolated($"""
                SELECT * FROM production_order_operations
                WHERE "Id" = {operationId}
                  AND "ProductionOrderId" = {productionOrderId}
                  AND "CompanyId" = {companyId}
                FOR UPDATE
                """)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (operation is null || operation.Status != ProductionOperationStatuses.InProgress) {
            await transaction.RollbackAsync(cancellationToken);
            return new(ProductionExecutionStatus.StateConflict, null);
        }

        if (operation.MachineId.HasValue) {
            var assignedMachine = await dbContext.Machines
                .FromSqlInterpolated($"""
                    SELECT * FROM machines
                    WHERE "Id" = {operation.MachineId.Value}
                      AND "CompanyId" = {companyId}
                    FOR UPDATE
                    """)
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);
            if (assignedMachine is null || assignedMachine.Status != MachineStatuses.Running) {
                await transaction.RollbackAsync(cancellationToken);
                return new(ProductionExecutionStatus.StateConflict, null);
            }
        }

        var completedOperation = await dbContext.ProductionOrderOperations
            .Where(candidate => candidate.Id == operationId &&
                candidate.ProductionOrderId == productionOrderId &&
                candidate.CompanyId == companyId &&
                candidate.Status == ProductionOperationStatuses.InProgress)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(candidate => candidate.Status, ProductionOperationStatuses.Completed)
                .SetProperty(candidate => candidate.CompletedAt, completedAt), cancellationToken);
        if (completedOperation != 1) {
            await transaction.RollbackAsync(cancellationToken);
            return new(ProductionExecutionStatus.StateConflict, null);
        }

        if (operation.MachineId.HasValue) {
            var releasedMachine = await dbContext.Machines
                .Where(machine => machine.Id == operation.MachineId.Value &&
                    machine.CompanyId == companyId &&
                    machine.Status == MachineStatuses.Running)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(machine => machine.Status, MachineStatuses.Available)
                    .SetProperty(machine => machine.UpdatedAt, completedAt), cancellationToken);
            if (releasedMachine != 1) {
                await transaction.RollbackAsync(cancellationToken);
                return new(ProductionExecutionStatus.StateConflict, null);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return new(ProductionExecutionStatus.Success, await GetOperationAsync(
            productionOrderId, operationId, companyId, cancellationToken));
    }

    private Task<ProductionOrderOperation?> GetOperationAsync(
        Guid productionOrderId,
        Guid operationId,
        Guid companyId,
        CancellationToken cancellationToken) => dbContext.ProductionOrderOperations
        .AsNoTracking()
        .SingleOrDefaultAsync(operation => operation.Id == operationId &&
            operation.ProductionOrderId == productionOrderId &&
            operation.CompanyId == companyId, cancellationToken);

    private IQueryable<ProductionOrder> GetOrderQuery() => dbContext.ProductionOrders
        .AsNoTracking()
        .Include(order => order.Product)
        .Include(order => order.BillOfMaterial)
            .ThenInclude(bom => bom!.Items)
                .ThenInclude(item => item.Material)
        .Include(order => order.Routing)
        .Include(order => order.Operations);
}
