using FactoryMind.Application.Features.Boms;
using FactoryMind.Domain.Manufacturing;
using Microsoft.EntityFrameworkCore;

namespace FactoryMind.Infrastructure.Persistence.Boms;

public sealed class EfBomRepository(FactoryMindDbContext dbContext) : IBomRepository {
    public async Task<IReadOnlyList<BillOfMaterial>> GetByProductAsync(
        Guid productId,
        Guid companyId,
        CancellationToken cancellationToken) => await dbContext.BillOfMaterials
        .AsNoTracking()
        .Include(bom => bom.Product)
        .Include(bom => bom.Items)
            .ThenInclude(item => item.Material)
        .Where(bom => bom.ProductId == productId && bom.CompanyId == companyId)
        .OrderByDescending(bom => bom.Revision)
        .ToListAsync(cancellationToken);

    public Task<BillOfMaterial?> GetByIdAsync(
        Guid bomId,
        Guid productId,
        Guid companyId,
        CancellationToken cancellationToken) => dbContext.BillOfMaterials
        .Include(bom => bom.Product)
        .Include(bom => bom.Items)
            .ThenInclude(item => item.Material)
        .SingleOrDefaultAsync(
            bom => bom.Id == bomId &&
                bom.ProductId == productId &&
                bom.CompanyId == companyId,
            cancellationToken);

    public Task<BillOfMaterial?> GetActiveAsync(
        Guid productId,
        Guid companyId,
        CancellationToken cancellationToken) => dbContext.BillOfMaterials
        .AsNoTracking()
        .Include(bom => bom.Product)
        .Include(bom => bom.Items)
            .ThenInclude(item => item.Material)
        .SingleOrDefaultAsync(
            bom => bom.ProductId == productId &&
                bom.CompanyId == companyId &&
                bom.Status == BillOfMaterialStatuses.Active,
            cancellationToken);

    public async Task<int> GetNextRevisionAsync(
        Guid productId,
        Guid companyId,
        CancellationToken cancellationToken) {
        var latestRevision = await dbContext.BillOfMaterials
            .Where(bom => bom.ProductId == productId && bom.CompanyId == companyId)
            .MaxAsync(bom => (int?)bom.Revision, cancellationToken);
        return latestRevision.GetValueOrDefault() + 1;
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetAvailableQuantitiesAsync(
        Guid companyId,
        IReadOnlyCollection<Guid> materialIds,
        CancellationToken cancellationToken) {
        if (materialIds.Count == 0) {
            return new Dictionary<Guid, decimal>();
        }

        var distinctMaterialIds = materialIds.Distinct().ToList();
        return await dbContext.InventoryBalances
            .AsNoTracking()
            .Where(balance => balance.CompanyId == companyId &&
                distinctMaterialIds.Contains(balance.MaterialId))
            .GroupBy(balance => balance.MaterialId)
            .ToDictionaryAsync(
                group => group.Key,
                group => group.Sum(balance => balance.Quantity),
                cancellationToken);
    }

    public void Add(BillOfMaterial bom) => dbContext.BillOfMaterials.Add(bom);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public async Task ActivateAsync(
        BillOfMaterial bom,
        DateTime activatedAt,
        CancellationToken cancellationToken) {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.BillOfMaterials
            .Where(candidate => candidate.CompanyId == bom.CompanyId &&
                candidate.ProductId == bom.ProductId &&
                candidate.Id != bom.Id &&
                candidate.Status == BillOfMaterialStatuses.Active)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(candidate => candidate.Status, BillOfMaterialStatuses.Archived)
                .SetProperty(candidate => candidate.UpdatedAt, activatedAt), cancellationToken);

        bom.Status = BillOfMaterialStatuses.Active;
        bom.UpdatedAt = activatedAt;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
