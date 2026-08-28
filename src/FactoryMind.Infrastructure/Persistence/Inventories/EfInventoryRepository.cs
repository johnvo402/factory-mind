using System.Data;
using FactoryMind.Application.Features.Inventories;
using FactoryMind.Domain.Manufacturing;
using Microsoft.EntityFrameworkCore;

namespace FactoryMind.Infrastructure.Persistence.Inventories;

public sealed class EfInventoryRepository(FactoryMindDbContext dbContext) : IInventoryRepository {
    public async Task<IReadOnlyList<InventoryBalance>> GetBalancesAsync(
        Guid companyId,
        Guid? warehouseId,
        Guid? materialId,
        string? search,
        CancellationToken cancellationToken) {
        var query = dbContext.InventoryBalances
            .AsNoTracking()
            .Include(balance => balance.Warehouse)
            .Include(balance => balance.Material)
            .Where(balance => balance.CompanyId == companyId);
        if (warehouseId.HasValue) {
            query = query.Where(balance => balance.WarehouseId == warehouseId.Value);
        }
        if (materialId.HasValue) {
            query = query.Where(balance => balance.MaterialId == materialId.Value);
        }
        if (search is not null) {
            var pattern = $"%{search}%";
            query = query.Where(balance =>
                (balance.Warehouse != null &&
                    (EF.Functions.ILike(balance.Warehouse.Code, pattern) ||
                     EF.Functions.ILike(balance.Warehouse.Name, pattern))) ||
                (balance.Material != null &&
                    (EF.Functions.ILike(balance.Material.Code, pattern) ||
                     EF.Functions.ILike(balance.Material.Name, pattern))));
        }
        return await query
            .OrderBy(balance => balance.Warehouse!.Code)
            .ThenBy(balance => balance.Material!.Code)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<InventoryTransaction> Items, int TotalCount)> GetTransactionsAsync(
        Guid companyId,
        Guid? warehouseId,
        Guid? materialId,
        InventoryTransactionType? type,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken) {
        var query = dbContext.InventoryTransactions
            .AsNoTracking()
            .Include(transaction => transaction.Warehouse)
            .Include(transaction => transaction.Material)
            .Where(transaction => transaction.CompanyId == companyId);
        if (warehouseId.HasValue) {
            query = query.Where(transaction => transaction.WarehouseId == warehouseId.Value);
        }
        if (materialId.HasValue) {
            query = query.Where(transaction => transaction.MaterialId == materialId.Value);
        }
        if (type.HasValue) {
            query = query.Where(transaction => transaction.Type == type.Value);
        }
        if (from.HasValue) {
            query = query.Where(transaction => transaction.CreatedAt >= from.Value);
        }
        if (to.HasValue) {
            query = query.Where(transaction => transaction.CreatedAt <= to.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(transaction => transaction.CreatedAt)
            .ThenByDescending(transaction => transaction.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    public async Task<InventoryOperationResult> ApplyAsync(
        InventoryTransaction transaction,
        CancellationToken cancellationToken) {
        await using var databaseTransaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        var changed = await ChangeBalanceAsync(transaction, cancellationToken);
        if (!changed) {
            await databaseTransaction.RollbackAsync(cancellationToken);
            return new(InventoryOperationStatus.InsufficientStock, []);
        }
        dbContext.InventoryTransactions.Add(transaction);
        await dbContext.SaveChangesAsync(cancellationToken);
        await databaseTransaction.CommitAsync(cancellationToken);
        return new(InventoryOperationStatus.Success, [transaction]);
    }

    public async Task<InventoryOperationResult> TransferAsync(
        InventoryTransaction transferOut,
        InventoryTransaction transferIn,
        CancellationToken cancellationToken) {
        await using var databaseTransaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        if (!await ChangeBalanceAsync(transferOut, cancellationToken)) {
            await databaseTransaction.RollbackAsync(cancellationToken);
            return new(InventoryOperationStatus.InsufficientStock, []);
        }
        await ChangeBalanceAsync(transferIn, cancellationToken);
        dbContext.InventoryTransactions.AddRange(transferOut, transferIn);
        await dbContext.SaveChangesAsync(cancellationToken);
        await databaseTransaction.CommitAsync(cancellationToken);
        return new(InventoryOperationStatus.Success, [transferOut, transferIn]);
    }

    private Task<bool> ChangeBalanceAsync(
        InventoryTransaction transaction,
        CancellationToken cancellationToken) => transaction.SignedQuantity() > 0
            ? IncreaseBalanceAsync(transaction, cancellationToken)
            : DecreaseBalanceAsync(transaction, cancellationToken);

    private async Task<bool> IncreaseBalanceAsync(
        InventoryTransaction transaction,
        CancellationToken cancellationToken) {
        var balanceId = Guid.NewGuid();
        var changedAt = transaction.CreatedAt;
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO inventory_balances
                ("Id", "CompanyId", "WarehouseId", "MaterialId", "Quantity", "UpdatedAt")
            VALUES
                ({balanceId}, {transaction.CompanyId}, {transaction.WarehouseId},
                 {transaction.MaterialId}, {transaction.Quantity}, {changedAt})
            ON CONFLICT ("CompanyId", "WarehouseId", "MaterialId")
            DO UPDATE SET
                "Quantity" = inventory_balances."Quantity" + EXCLUDED."Quantity",
                "UpdatedAt" = EXCLUDED."UpdatedAt"
            """, cancellationToken);
        return true;
    }

    private async Task<bool> DecreaseBalanceAsync(
        InventoryTransaction transaction,
        CancellationToken cancellationToken) {
        var changedAt = transaction.CreatedAt;
        var affected = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE inventory_balances
            SET "Quantity" = "Quantity" - {transaction.Quantity},
                "UpdatedAt" = {changedAt}
            WHERE "CompanyId" = {transaction.CompanyId}
              AND "WarehouseId" = {transaction.WarehouseId}
              AND "MaterialId" = {transaction.MaterialId}
              AND "Quantity" >= {transaction.Quantity}
            """, cancellationToken);
        return affected == 1;
    }
}
