using FactoryMind.Application.Features.ProductInventories;
using FactoryMind.Domain.Manufacturing;
using Microsoft.EntityFrameworkCore;

namespace FactoryMind.Infrastructure.Persistence.ProductInventories;

public sealed class EfProductInventoryRepository(FactoryMindDbContext dbContext)
    : IProductInventoryRepository {
    public async Task<IReadOnlyList<ProductInventoryBalance>> GetBalancesAsync(
        Guid companyId,
        Guid? warehouseId,
        Guid? productId,
        string? search,
        CancellationToken cancellationToken) {
        var query = dbContext.ProductInventoryBalances
            .AsNoTracking()
            .Include(balance => balance.Warehouse)
            .Include(balance => balance.Product)
            .Where(balance => balance.CompanyId == companyId);
        if (warehouseId.HasValue) {
            query = query.Where(balance => balance.WarehouseId == warehouseId.Value);
        }
        if (productId.HasValue) {
            query = query.Where(balance => balance.ProductId == productId.Value);
        }
        if (search is not null) {
            var pattern = $"%{search}%";
            query = query.Where(balance =>
                (balance.Warehouse != null &&
                    (EF.Functions.ILike(balance.Warehouse.Code, pattern) ||
                     EF.Functions.ILike(balance.Warehouse.Name, pattern))) ||
                (balance.Product != null &&
                    (EF.Functions.ILike(balance.Product.Code, pattern) ||
                     EF.Functions.ILike(balance.Product.Name, pattern))));
        }

        return await query
            .OrderBy(balance => balance.Warehouse!.Code)
            .ThenBy(balance => balance.Product!.Code)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<ProductInventoryTransaction> Items, int TotalCount)> GetTransactionsAsync(
        Guid companyId,
        Guid? warehouseId,
        Guid? productId,
        ProductInventoryTransactionType? type,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken) {
        var query = dbContext.ProductInventoryTransactions
            .AsNoTracking()
            .Include(transaction => transaction.Warehouse)
            .Include(transaction => transaction.Product)
            .Where(transaction => transaction.CompanyId == companyId);
        if (warehouseId.HasValue) {
            query = query.Where(transaction => transaction.WarehouseId == warehouseId.Value);
        }
        if (productId.HasValue) {
            query = query.Where(transaction => transaction.ProductId == productId.Value);
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
}
