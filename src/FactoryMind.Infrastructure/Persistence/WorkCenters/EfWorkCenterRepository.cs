using FactoryMind.Application.Features.WorkCenters;
using FactoryMind.Domain.Manufacturing;
using Microsoft.EntityFrameworkCore;

namespace FactoryMind.Infrastructure.Persistence.WorkCenters;

public sealed class EfWorkCenterRepository(FactoryMindDbContext dbContext) : IWorkCenterRepository {
    public async Task<IReadOnlyList<WorkCenter>> GetByCompanyAsync(
        Guid companyId,
        string? search,
        CancellationToken cancellationToken) {
        var query = dbContext.WorkCenters.AsNoTracking()
            .Where(workCenter => workCenter.CompanyId == companyId);
        if (search is not null) {
            var pattern = $"%{search}%";
            query = query.Where(workCenter =>
                EF.Functions.ILike(workCenter.Code, pattern) ||
                EF.Functions.ILike(workCenter.Name, pattern));
        }
        return await query.OrderBy(workCenter => workCenter.Code).ToListAsync(cancellationToken);
    }

    public Task<WorkCenter?> GetByIdAsync(
        Guid id,
        Guid companyId,
        CancellationToken cancellationToken) => dbContext.WorkCenters.SingleOrDefaultAsync(
            workCenter => workCenter.Id == id && workCenter.CompanyId == companyId,
            cancellationToken);

    public Task<bool> CodeExistsAsync(
        Guid companyId,
        string code,
        Guid? excludedId,
        CancellationToken cancellationToken) => dbContext.WorkCenters.AnyAsync(
            workCenter => workCenter.CompanyId == companyId &&
                workCenter.Code == code &&
                (!excludedId.HasValue || workCenter.Id != excludedId.Value),
            cancellationToken);

    public void Add(WorkCenter workCenter) => dbContext.WorkCenters.Add(workCenter);
    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
