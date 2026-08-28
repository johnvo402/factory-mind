using FactoryMind.Application.Features.Materials;
using FactoryMind.Domain.Manufacturing;
using Microsoft.EntityFrameworkCore;

namespace FactoryMind.Infrastructure.Persistence.Materials;

public sealed class EfMaterialRepository(FactoryMindDbContext dbContext) : IMaterialRepository {
    public async Task<IReadOnlyList<Material>> GetByCompanyAsync(
        Guid companyId,
        string? search,
        CancellationToken cancellationToken) {
        var query = dbContext.Materials
            .AsNoTracking()
            .Where(material => material.CompanyId == companyId);
        if (search is not null) {
            var pattern = $"%{search}%";
            query = query.Where(material =>
                EF.Functions.ILike(material.Code, pattern) ||
                EF.Functions.ILike(material.Name, pattern));
        }

        return await query.OrderBy(material => material.Code).ToListAsync(cancellationToken);
    }

    public Task<Material?> GetByIdAsync(
        Guid materialId,
        Guid companyId,
        CancellationToken cancellationToken) => dbContext.Materials.SingleOrDefaultAsync(
            material => material.Id == materialId && material.CompanyId == companyId,
            cancellationToken);

    public Task<bool> CodeExistsAsync(
        Guid companyId,
        string code,
        Guid? excludedMaterialId,
        CancellationToken cancellationToken) => dbContext.Materials.AnyAsync(
            material => material.CompanyId == companyId &&
                material.Code == code &&
                (!excludedMaterialId.HasValue || material.Id != excludedMaterialId.Value),
            cancellationToken);

    public Task<bool> HasBomItemsAsync(
        Guid materialId,
        Guid companyId,
        CancellationToken cancellationToken) => dbContext.BomItems.AnyAsync(
            item => item.MaterialId == materialId &&
                item.BillOfMaterial != null &&
                item.BillOfMaterial.CompanyId == companyId,
            cancellationToken);

    public void Add(Material material) => dbContext.Materials.Add(material);
    public void Remove(Material material) => dbContext.Materials.Remove(material);
    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
