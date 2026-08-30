using FactoryMind.Application.Features.ProductionOrders;
using FactoryMind.Domain.Manufacturing;
using Microsoft.EntityFrameworkCore;

namespace FactoryMind.Infrastructure.Persistence.ProductionOrders;

public sealed class EfProductionOrderRepository(FactoryMindDbContext dbContext) : IProductionOrderRepository {
    public async Task<IReadOnlyList<ProductionOrder>> GetByCompanyAsync(
        Guid companyId,
        string? search,
        CancellationToken cancellationToken) {
        var query = dbContext.ProductionOrders
            .AsNoTracking()
            .Include(order => order.Product)
            .Include(order => order.BillOfMaterial)
            .Where(order => order.CompanyId == companyId);
        if (search is not null) {
            var pattern = $"%{search}%";
            query = query.Where(order =>
                EF.Functions.ILike(order.Number, pattern) ||
                EF.Functions.ILike(order.Status, pattern) ||
                (order.Product != null &&
                    (EF.Functions.ILike(order.Product.Code, pattern) ||
                     EF.Functions.ILike(order.Product.Name, pattern))));
        }

        return await query.OrderByDescending(order => order.UpdatedAt).ToListAsync(cancellationToken);
    }

    public Task<ProductionOrder?> GetByIdAsync(
        Guid productionOrderId,
        Guid companyId,
        CancellationToken cancellationToken) => dbContext.ProductionOrders
        .Include(order => order.Product)
        .Include(order => order.BillOfMaterial)
        .SingleOrDefaultAsync(
            order => order.Id == productionOrderId && order.CompanyId == companyId,
            cancellationToken);

    public Task<bool> NumberExistsAsync(
        Guid companyId,
        string number,
        Guid? excludedProductionOrderId,
        CancellationToken cancellationToken) => dbContext.ProductionOrders.AnyAsync(
            order => order.CompanyId == companyId &&
                order.Number == number &&
                (!excludedProductionOrderId.HasValue || order.Id != excludedProductionOrderId.Value),
            cancellationToken);

    public async Task<bool> TryUpdatePlannedAsync(
        ProductionOrder order,
        CancellationToken cancellationToken) {
        var affected = await dbContext.ProductionOrders
            .Where(candidate => candidate.Id == order.Id &&
                candidate.CompanyId == order.CompanyId &&
                candidate.Status == ProductionOrderStatuses.Planned)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(candidate => candidate.Number, order.Number)
                .SetProperty(candidate => candidate.ProductId, order.ProductId)
                .SetProperty(candidate => candidate.Quantity, order.Quantity)
                .SetProperty(candidate => candidate.UpdatedAt, order.UpdatedAt), cancellationToken);
        if (affected == 1) {
            dbContext.Entry(order).State = EntityState.Detached;
        }
        return affected == 1;
    }

    public async Task<bool> TryDeletePlannedAsync(
        Guid productionOrderId,
        Guid companyId,
        CancellationToken cancellationToken) => await dbContext.ProductionOrders
        .Where(order => order.Id == productionOrderId &&
            order.CompanyId == companyId &&
            order.Status == ProductionOrderStatuses.Planned)
        .ExecuteDeleteAsync(cancellationToken) == 1;

    public void Add(ProductionOrder order) => dbContext.ProductionOrders.Add(order);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
