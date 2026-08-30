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
        var affected = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE production_orders AS production_order
            SET "BillOfMaterialId" = bom."Id",
                "Status" = {ProductionOrderStatuses.Released},
                "ReleasedAt" = {releasedAt},
                "UpdatedAt" = {releasedAt}
            FROM bill_of_materials AS bom
            WHERE production_order."Id" = {productionOrderId}
              AND production_order."CompanyId" = {companyId}
              AND production_order."Status" = {ProductionOrderStatuses.Planned}
              AND bom."CompanyId" = production_order."CompanyId"
              AND bom."ProductId" = production_order."ProductId"
              AND bom."Status" = {BillOfMaterialStatuses.Active}
            """, cancellationToken);
        if (affected == 1) {
            return new(ProductionExecutionStatus.Success, await GetAsync(
                productionOrderId,
                companyId,
                cancellationToken));
        }

        var remainsPlanned = await dbContext.ProductionOrders.AsNoTracking().AnyAsync(
            order => order.Id == productionOrderId &&
                order.CompanyId == companyId &&
                order.Status == ProductionOrderStatuses.Planned,
            cancellationToken);
        if (!remainsPlanned) {
            return new(ProductionExecutionStatus.StateConflict, null);
        }
        return new(ProductionExecutionStatus.ActiveBomNotFound, null);
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
                order.BillOfMaterialId != null)
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

    private IQueryable<ProductionOrder> GetOrderQuery() => dbContext.ProductionOrders
        .AsNoTracking()
        .Include(order => order.Product)
        .Include(order => order.BillOfMaterial)
            .ThenInclude(bom => bom!.Items)
                .ThenInclude(item => item.Material);
}
