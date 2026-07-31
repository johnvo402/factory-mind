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

    public void Add(ProductionOrder order) => dbContext.ProductionOrders.Add(order);
    public void Remove(ProductionOrder order) => dbContext.ProductionOrders.Remove(order);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
