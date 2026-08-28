using FactoryMind.Application.Features.ExcelImports;
using Microsoft.EntityFrameworkCore;

namespace FactoryMind.Infrastructure.Persistence.ExcelImports;

public sealed class EfExcelImportRepository(
    FactoryMindDbContext dbContext) : IExcelImportRepository {
    public async Task<ExcelImportReferenceData> GetReferenceDataAsync(
        Guid companyId,
        string entityType,
        CancellationToken cancellationToken) {
        switch (entityType) {
            case ExcelImportEntityTypes.Machine:
                return new(
                    await dbContext.Machines.AsNoTracking()
                        .Where(machine => machine.CompanyId == companyId)
                        .Select(machine => machine.Code)
                        .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, cancellationToken),
                    EmptyRelatedIds());
            case ExcelImportEntityTypes.Material:
                return new(
                    await dbContext.Materials.AsNoTracking()
                        .Where(material => material.CompanyId == companyId)
                        .Select(material => material.Code)
                        .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, cancellationToken),
                    EmptyRelatedIds());
            case ExcelImportEntityTypes.Product:
                return new(
                    await dbContext.Products.AsNoTracking()
                        .Where(product => product.CompanyId == companyId)
                        .Select(product => product.Code)
                        .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, cancellationToken),
                    EmptyRelatedIds());
            case ExcelImportEntityTypes.Inventory:
                var materialIds = await dbContext.Materials.AsNoTracking()
                    .Where(material => material.CompanyId == companyId)
                    .ToDictionaryAsync(
                        material => material.Code,
                        material => material.Id,
                        StringComparer.OrdinalIgnoreCase,
                        cancellationToken);
                var warehouseIds = await dbContext.Warehouses.AsNoTracking()
                    .Where(warehouse => warehouse.CompanyId == companyId && warehouse.IsActive)
                    .ToDictionaryAsync(
                        warehouse => warehouse.Code,
                        warehouse => warehouse.Id,
                        StringComparer.OrdinalIgnoreCase,
                        cancellationToken);
                var inventoryKeys = await dbContext.InventoryBalances.AsNoTracking()
                    .Where(balance => balance.CompanyId == companyId)
                    .Select(balance => new { balance.MaterialId, balance.WarehouseId })
                    .ToListAsync(cancellationToken);
                return new(
                    inventoryKeys
                        .Select(balance => $"{balance.MaterialId:N}|{balance.WarehouseId:N}")
                        .ToHashSet(StringComparer.OrdinalIgnoreCase),
                    materialIds
                        .Select(pair => new KeyValuePair<string, Guid>($"material:{pair.Key}", pair.Value))
                        .Concat(warehouseIds.Select(pair =>
                            new KeyValuePair<string, Guid>($"warehouse:{pair.Key}", pair.Value)))
                        .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase));
            case ExcelImportEntityTypes.ProductionOrder:
                var productIds = await dbContext.Products.AsNoTracking()
                    .Where(product => product.CompanyId == companyId)
                    .ToDictionaryAsync(
                        product => product.Code,
                        product => product.Id,
                        StringComparer.OrdinalIgnoreCase,
                        cancellationToken);
                var orderNumbers = await dbContext.ProductionOrders.AsNoTracking()
                    .Where(order => order.CompanyId == companyId)
                    .Select(order => order.Number)
                    .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, cancellationToken);
                return new(orderNumbers, productIds);
            default:
                return new(new HashSet<string>(), EmptyRelatedIds());
        }
    }

    public void Add(ExcelImportBatch batch) {
        dbContext.Machines.AddRange(batch.Machines);
        dbContext.Materials.AddRange(batch.Materials);
        dbContext.Products.AddRange(batch.Products);
        dbContext.InventoryTransactions.AddRange(batch.InventoryTransactions);
        dbContext.InventoryBalances.AddRange(batch.InventoryBalances);
        dbContext.ProductionOrders.AddRange(batch.ProductionOrders);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    private static IReadOnlyDictionary<string, Guid> EmptyRelatedIds() =>
        new Dictionary<string, Guid>();
}
