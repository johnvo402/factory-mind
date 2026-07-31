using FactoryMind.Application.Features.Inventories;
using FactoryMind.Domain.Manufacturing;
using Microsoft.EntityFrameworkCore;

namespace FactoryMind.Infrastructure.Persistence.Inventories;

public sealed class EfInventoryRepository(FactoryMindDbContext dbContext) : IInventoryRepository {
    public async Task<IReadOnlyList<Inventory>> GetByCompanyAsync(
        Guid companyId,
        string? search,
        CancellationToken cancellationToken) {
        var query = dbContext.Inventories
            .AsNoTracking()
            .Include(inventory => inventory.Material)
            .Where(inventory => inventory.CompanyId == companyId);
        if (search is not null) {
            var pattern = $"%{search}%";
            query = query.Where(inventory =>
                EF.Functions.ILike(inventory.Warehouse, pattern) ||
                (inventory.Material != null &&
                    (EF.Functions.ILike(inventory.Material.Code, pattern) ||
                     EF.Functions.ILike(inventory.Material.Name, pattern))));
        }

        return await query
            .OrderBy(inventory => inventory.Warehouse)
            .ThenBy(inventory => inventory.Material!.Code)
            .ToListAsync(cancellationToken);
    }

    public Task<Inventory?> GetByIdAsync(
        Guid inventoryId,
        Guid companyId,
        CancellationToken cancellationToken) => dbContext.Inventories
        .Include(inventory => inventory.Material)
        .SingleOrDefaultAsync(
            inventory => inventory.Id == inventoryId && inventory.CompanyId == companyId,
            cancellationToken);

    public Task<bool> EntryExistsAsync(
        Guid companyId,
        Guid materialId,
        string warehouse,
        Guid? excludedInventoryId,
        CancellationToken cancellationToken) => dbContext.Inventories.AnyAsync(
            inventory => inventory.CompanyId == companyId &&
                inventory.MaterialId == materialId &&
                inventory.Warehouse == warehouse &&
                (!excludedInventoryId.HasValue || inventory.Id != excludedInventoryId.Value),
            cancellationToken);

    public void Add(Inventory inventory) => dbContext.Inventories.Add(inventory);
    public void Remove(Inventory inventory) => dbContext.Inventories.Remove(inventory);
    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
