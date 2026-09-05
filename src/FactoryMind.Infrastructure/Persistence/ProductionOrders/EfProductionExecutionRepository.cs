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

    public async Task<ProductionExecutionResult> TryReleaseAsync(
        Guid productionOrderId,
        Guid companyId,
        DateTime releasedAt,
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
        await dbContext.Entry(routing).Collection(candidate => candidate.Operations).Query()
            .Include(operation => operation.WorkCenter)
            .LoadAsync(cancellationToken);
        if (routing.Operations.Count == 0) {
            await transaction.RollbackAsync(cancellationToken);
            return new(ProductionExecutionStatus.ActiveRoutingNotFound, null);
        }

        var snapshots = routing.Operations.OrderBy(operation => operation.Sequence)
            .Select(operation => new ProductionOrderOperation {
                CompanyId = companyId,
                ProductionOrderId = order.Id,
                RoutingOperationId = operation.Id,
                Sequence = operation.Sequence,
                Name = operation.Name,
                WorkCenterId = operation.WorkCenterId,
                WorkCenterCode = operation.WorkCenter?.Code ?? string.Empty,
                WorkCenterName = operation.WorkCenter?.Name ?? string.Empty,
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
                order.RoutingId != null)
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
                order.RoutingId != null &&
                order.StartedAt != null &&
                order.ProductId == outputTransaction.ProductId &&
                order.Quantity == outputTransaction.Quantity &&
                !dbContext.ProductionOrderOperations.Any(operation =>
                    operation.ProductionOrderId == order.Id &&
                    operation.CompanyId == companyId &&
                    operation.Status != ProductionOperationStatuses.Completed))
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
        Guid companyId,
        DateTime startedAt,
        CancellationToken cancellationToken) {
        var affected = await dbContext.ProductionOrderOperations
            .Where(operation => operation.Id == operationId &&
                operation.ProductionOrderId == productionOrderId &&
                operation.CompanyId == companyId &&
                operation.Status == ProductionOperationStatuses.Pending &&
                operation.ProductionOrder!.Status == ProductionOrderStatuses.InProgress &&
                !dbContext.ProductionOrderOperations.Any(candidate =>
                    candidate.ProductionOrderId == productionOrderId &&
                    candidate.Status == ProductionOperationStatuses.InProgress) &&
                !dbContext.ProductionOrderOperations.Any(candidate =>
                    candidate.ProductionOrderId == productionOrderId &&
                    candidate.Sequence < operation.Sequence &&
                    candidate.Status != ProductionOperationStatuses.Completed))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(operation => operation.Status, ProductionOperationStatuses.InProgress)
                .SetProperty(operation => operation.StartedAt, startedAt), cancellationToken);
        return affected == 1
            ? new(ProductionExecutionStatus.Success, await GetOperationAsync(
                productionOrderId, operationId, companyId, cancellationToken))
            : new(ProductionExecutionStatus.StateConflict, null);
    }

    public async Task<ProductionOperationExecutionResult> TryCompleteOperationAsync(
        Guid productionOrderId,
        Guid operationId,
        Guid companyId,
        DateTime completedAt,
        CancellationToken cancellationToken) {
        var affected = await dbContext.ProductionOrderOperations
            .Where(operation => operation.Id == operationId &&
                operation.ProductionOrderId == productionOrderId &&
                operation.CompanyId == companyId &&
                operation.Status == ProductionOperationStatuses.InProgress &&
                operation.ProductionOrder!.Status == ProductionOrderStatuses.InProgress)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(operation => operation.Status, ProductionOperationStatuses.Completed)
                .SetProperty(operation => operation.CompletedAt, completedAt), cancellationToken);
        return affected == 1
            ? new(ProductionExecutionStatus.Success, await GetOperationAsync(
                productionOrderId, operationId, companyId, cancellationToken))
            : new(ProductionExecutionStatus.StateConflict, null);
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
