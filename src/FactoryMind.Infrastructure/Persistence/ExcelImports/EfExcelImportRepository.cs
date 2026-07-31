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
                var inventoryKeys = await dbContext.Inventories.AsNoTracking()
                    .Where(inventory => inventory.CompanyId == companyId)
                    .Select(inventory => new { inventory.MaterialId, inventory.Warehouse })
                    .ToListAsync(cancellationToken);
                return new(
                    inventoryKeys
                        .Select(inventory => $"{inventory.MaterialId:N}|{inventory.Warehouse}")
                        .ToHashSet(StringComparer.OrdinalIgnoreCase),
                    materialIds);
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
        dbContext.Inventories.AddRange(batch.Inventories);
        dbContext.ProductionOrders.AddRange(batch.ProductionOrders);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    private static IReadOnlyDictionary<string, Guid> EmptyRelatedIds() =>
        new Dictionary<string, Guid>();
}
