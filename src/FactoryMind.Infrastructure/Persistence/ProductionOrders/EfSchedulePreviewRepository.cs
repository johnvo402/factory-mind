using FactoryMind.Application.Features.ProductionOrders;
using FactoryMind.Domain.Manufacturing;
using Microsoft.EntityFrameworkCore;

namespace FactoryMind.Infrastructure.Persistence.ProductionOrders;

public sealed class EfSchedulePreviewRepository(
    FactoryMindDbContext dbContext,
    IProductionOrderDeliveryRiskCalculator riskCalculator) : ISchedulePreviewRepository {
    public async Task<SchedulePreviewData?> LoadAsync(
        Guid companyId,
        CancellationToken cancellationToken) {
        var timeZoneId = await dbContext.Companies.AsNoTracking()
            .Where(company => company.Id == companyId)
            .Select(company => company.TimeZoneId)
            .SingleOrDefaultAsync(cancellationToken);
        if (timeZoneId is null || !IsValidTimeZone(timeZoneId)) return null;

        var activeStatuses = new[] {
            ProductionOrderStatuses.Planned,
            ProductionOrderStatuses.Released,
            ProductionOrderStatuses.InProgress
        };
        var orderQuery = dbContext.ProductionOrders.AsNoTracking()
            .AsSplitQuery()
            .Include(order => order.Product)
            .Include(order => order.Operations)
            .Where(order => order.CompanyId == companyId && activeStatuses.Contains(order.Status));
        var orders = await orderQuery.OrderBy(order => order.Number).ThenBy(order => order.Id)
            .ToListAsync(cancellationToken);

        var plannedProductIds = orders.Where(order => order.Status == ProductionOrderStatuses.Planned)
            .Select(order => order.ProductId).Distinct().ToList();
        var activeRoutings = await dbContext.Routings.AsNoTracking()
            .AsSplitQuery()
            .Include(routing => routing.Operations)
            .Where(routing => routing.CompanyId == companyId
                && routing.Status == RoutingStatuses.Active
                && plannedProductIds.Contains(routing.ProductId))
            .ToListAsync(cancellationToken);
        var routingByProduct = activeRoutings.ToDictionary(routing => routing.ProductId);

        var centers = await dbContext.WorkCenters.AsNoTracking()
            .AsSplitQuery()
            .Include(center => center.Shifts)
            .Include(center => center.DaysOff)
            .Where(center => center.CompanyId == companyId)
            .OrderBy(center => center.Code).ThenBy(center => center.Id)
            .ToListAsync(cancellationToken);
        var centerById = centers.ToDictionary(center => center.Id);

        return new SchedulePreviewData(
            timeZoneId,
            orders.Select(order => MapOrder(order, routingByProduct, centerById)).ToList(),
            centers.Select(center => new ScheduleWorkCenterInput(
                center.Id, center.Code, center.Name, center.IsActive, center.ParallelCapacity,
                center.Shifts.ToList(), center.DaysOff.ToList())).ToList());
    }

    private ScheduleOrderInput MapOrder(
        ProductionOrder order,
        IReadOnlyDictionary<Guid, Routing> routingByProduct,
        IReadOnlyDictionary<Guid, WorkCenter> centerById) {
        IReadOnlyList<ScheduleOperationInput> operations;
        string source;
        var provisional = order.Status == ProductionOrderStatuses.Planned;
        if (provisional) {
            if (!routingByProduct.TryGetValue(order.ProductId, out var routing)
                || routing.Operations.Count == 0) {
                source = PlanningSources.ActiveRoutingMissing;
                operations = [];
            } else {
                source = PlanningSources.ActiveRouting;
                operations = routing.Operations.OrderBy(operation => operation.Sequence)
                    .ThenBy(operation => operation.Id)
                    .Select(operation => {
                        centerById.TryGetValue(operation.WorkCenterId, out var center);
                        return new ScheduleOperationInput(
                            operation.Id, operation.Sequence, operation.Name, operation.WorkCenterId,
                            center?.Code ?? string.Empty, center?.Name ?? string.Empty,
                            operation.SetupTimeMinutes, operation.RunTimeMinutes,
                            ProductionOperationStatuses.Pending, null, null);
                    }).ToList();
            }
        } else {
            source = order.Operations.Count == 0
                ? PlanningSources.OperationSnapshotMissing
                : PlanningSources.LockedSnapshot;
            operations = order.Operations.OrderBy(operation => operation.Sequence)
                .ThenBy(operation => operation.Id)
                .Select(operation => new ScheduleOperationInput(
                    operation.Id, operation.Sequence, operation.Name, operation.WorkCenterId,
                    operation.WorkCenterCode, operation.WorkCenterName,
                    operation.SetupTimeMinutes, operation.RunTimeMinutes, operation.Status,
                    operation.StartedAt, operation.CompletedAt)).ToList();
        }

        return new ScheduleOrderInput(
            order.Id, order.Number, order.Product?.Code ?? string.Empty, order.Product?.Name ?? string.Empty,
            order.Status, order.Priority, riskCalculator.Calculate(order).DeliveryStatus,
            order.DueDate, order.StartedAt, source, provisional, operations);
    }

    private static bool IsValidTimeZone(string timeZoneId) {
        try {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        } catch (TimeZoneNotFoundException) {
            return false;
        } catch (InvalidTimeZoneException) {
            return false;
        }
    }
}
