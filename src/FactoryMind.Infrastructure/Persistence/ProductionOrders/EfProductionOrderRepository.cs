using FactoryMind.Application.Features.ProductionOrders;
using FactoryMind.Domain.Manufacturing;
using Microsoft.EntityFrameworkCore;

namespace FactoryMind.Infrastructure.Persistence.ProductionOrders;

public sealed class EfProductionOrderRepository(FactoryMindDbContext dbContext) : IProductionOrderRepository {
    public async Task<(IReadOnlyList<ProductionOrder> Items, int TotalCount)> GetByCompanyAsync(
        Guid companyId,
        ProductionOrderListCriteria criteria,
        CancellationToken cancellationToken) {
        var query = dbContext.ProductionOrders
            .AsNoTracking()
            .Include(order => order.Product)
            .Include(order => order.BillOfMaterial)
            .Include(order => order.Routing)
            .Where(order => order.CompanyId == companyId);
        if (criteria.Search is not null) {
            var pattern = $"%{criteria.Search}%";
            query = query.Where(order =>
                EF.Functions.ILike(order.Number, pattern) ||
                EF.Functions.ILike(order.Status, pattern) ||
                (order.Product != null &&
                    (EF.Functions.ILike(order.Product.Code, pattern) ||
                     EF.Functions.ILike(order.Product.Name, pattern))));
        }

        if (criteria.Status is not null) {
            query = query.Where(order => order.Status == criteria.Status);
        }
        if (criteria.Priority is not null) {
            query = query.Where(order => order.Priority == criteria.Priority);
        }
        if (criteria.ProductId.HasValue) {
            query = query.Where(order => order.ProductId == criteria.ProductId.Value);
        }
        if (criteria.DueFrom.HasValue) {
            query = query.Where(order => order.DueDate >= criteria.DueFrom.Value);
        }
        if (criteria.DueTo.HasValue) {
            query = query.Where(order => order.DueDate <= criteria.DueTo.Value);
        }
        query = ApplyDeliveryStatus(query, criteria);

        var totalCount = await query.CountAsync(cancellationToken);
        var ordered = ApplyOrdering(query, criteria);
        var items = await ordered
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    private static IQueryable<ProductionOrder> ApplyDeliveryStatus(
        IQueryable<ProductionOrder> query,
        ProductionOrderListCriteria criteria) {
        return criteria.DeliveryStatus is null
            ? query
            : query.Where(ProductionOrderDeliveryRiskCalculator.DeliveryStatusPredicate(
                criteria.DeliveryStatus,
                criteria.UtcNow,
                criteria.DueSoonThrough));
    }

    private static IOrderedQueryable<ProductionOrder> ApplyOrdering(
        IQueryable<ProductionOrder> query,
        ProductionOrderListCriteria criteria) {
        if (criteria.PlanningOrder || criteria.SortBy == ProductionOrderSortFields.DeliveryRisk) {
            var rank = ProductionOrderDeliveryRiskCalculator.PlanningRankExpression(
                criteria.UtcNow,
                criteria.DueSoonThrough);
            var ordered = criteria.SortBy == ProductionOrderSortFields.DeliveryRisk
                    && criteria.SortDirection == ProductionOrderSortDirections.Descending
                ? query.OrderByDescending(rank)
                : query.OrderBy(rank);
            return ordered
                .ThenBy(order => order.DueDate == null)
                .ThenBy(order => order.DueDate)
                .ThenBy(order => order.Number)
                .ThenBy(order => order.Id);
        }

        var descending = criteria.SortDirection == ProductionOrderSortDirections.Descending;
        return criteria.SortBy switch {
            ProductionOrderSortFields.DueDate => descending
                ? query.OrderBy(order => order.DueDate == null).ThenByDescending(order => order.DueDate)
                    .ThenBy(order => order.Number).ThenBy(order => order.Id)
                : query.OrderBy(order => order.DueDate == null).ThenBy(order => order.DueDate)
                    .ThenBy(order => order.Number).ThenBy(order => order.Id),
            ProductionOrderSortFields.Priority => descending
                ? query.OrderByDescending(order => order.Priority == ProductionOrderPriorities.Urgent ? 4
                        : order.Priority == ProductionOrderPriorities.High ? 3
                        : order.Priority == ProductionOrderPriorities.Normal ? 2 : 1)
                    .ThenBy(order => order.DueDate == null).ThenBy(order => order.DueDate)
                    .ThenBy(order => order.Number).ThenBy(order => order.Id)
                : query.OrderBy(order => order.Priority == ProductionOrderPriorities.Urgent ? 4
                        : order.Priority == ProductionOrderPriorities.High ? 3
                        : order.Priority == ProductionOrderPriorities.Normal ? 2 : 1)
                    .ThenBy(order => order.DueDate == null).ThenBy(order => order.DueDate)
                    .ThenBy(order => order.Number).ThenBy(order => order.Id),
            ProductionOrderSortFields.Number => descending
                ? query.OrderByDescending(order => order.Number).ThenBy(order => order.Id)
                : query.OrderBy(order => order.Number).ThenBy(order => order.Id),
            ProductionOrderSortFields.Status => descending
                ? query.OrderByDescending(order => order.Status).ThenBy(order => order.Number).ThenBy(order => order.Id)
                : query.OrderBy(order => order.Status).ThenBy(order => order.Number).ThenBy(order => order.Id),
            _ => descending
                ? query.OrderByDescending(order => order.UpdatedAt).ThenBy(order => order.Number).ThenBy(order => order.Id)
                : query.OrderBy(order => order.UpdatedAt).ThenBy(order => order.Number).ThenBy(order => order.Id)
        };
    }

    public Task<ProductionOrder?> GetByIdAsync(
        Guid productionOrderId,
        Guid companyId,
        CancellationToken cancellationToken) => dbContext.ProductionOrders
        .Include(order => order.Product)
        .Include(order => order.BillOfMaterial)
        .Include(order => order.Routing)
        .Include(order => order.Operations)
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
                .SetProperty(candidate => candidate.DueDate, order.DueDate)
                .SetProperty(candidate => candidate.Priority, order.Priority)
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
