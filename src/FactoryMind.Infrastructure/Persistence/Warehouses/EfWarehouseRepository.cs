using FactoryMind.Application.Features.Warehouses;
using FactoryMind.Domain.Manufacturing;
using Microsoft.EntityFrameworkCore;

namespace FactoryMind.Infrastructure.Persistence.Warehouses;

public sealed class EfWarehouseRepository(FactoryMindDbContext dbContext) : IWarehouseRepository {
    public async Task<IReadOnlyList<Warehouse>> GetByCompanyAsync(
        Guid companyId,
        string? search,
        CancellationToken cancellationToken) {
        var query = dbContext.Warehouses
            .AsNoTracking()
            .Where(warehouse => warehouse.CompanyId == companyId);
        if (search is not null) {
            var pattern = $"%{search}%";
            query = query.Where(warehouse =>
                EF.Functions.ILike(warehouse.Code, pattern) ||
                EF.Functions.ILike(warehouse.Name, pattern));
        }
        return await query
            .OrderBy(warehouse => warehouse.Code)
            .ToListAsync(cancellationToken);
    }

    public Task<Warehouse?> GetByIdAsync(
        Guid warehouseId,
        Guid companyId,
        CancellationToken cancellationToken) => dbContext.Warehouses.SingleOrDefaultAsync(
            warehouse => warehouse.Id == warehouseId && warehouse.CompanyId == companyId,
            cancellationToken);

    public Task<bool> CodeExistsAsync(
        Guid companyId,
        string code,
        Guid? excludedWarehouseId,
        CancellationToken cancellationToken) => dbContext.Warehouses.AnyAsync(
            warehouse => warehouse.CompanyId == companyId &&
                warehouse.Code == code &&
                (!excludedWarehouseId.HasValue || warehouse.Id != excludedWarehouseId.Value),
            cancellationToken);

    public void Add(Warehouse warehouse) => dbContext.Warehouses.Add(warehouse);
    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
