using FactoryMind.Application.Features.Dashboard;
using FactoryMind.Application.Features.ProductionOrders;
using FactoryMind.Domain.Knowledge;
using FactoryMind.Domain.Manufacturing;
using Microsoft.EntityFrameworkCore;

namespace FactoryMind.Infrastructure.Persistence.Dashboard;

public sealed class EfDashboardRepository(
    FactoryMindDbContext dbContext,
    IProductionOrderDeliveryRiskCalculator riskCalculator) : IDashboardRepository {
    public async Task<DashboardSummary> GetSummaryAsync(
        Guid companyId,
        CancellationToken cancellationToken) {
        var now = riskCalculator.UtcNow;
        var dueSoonThrough = riskCalculator.DueSoonThrough;
        var planning = await dbContext.ProductionOrders.AsNoTracking()
            .Where(order => order.CompanyId == companyId)
            .GroupBy(_ => 1)
            .Select(ProductionOrderDeliveryRiskCalculator.PlanningSummaryProjection(
                now,
                dueSoonThrough))
            .SingleOrDefaultAsync(cancellationToken);
        var inventoryBalances = await dbContext.InventoryBalances
            .AsNoTracking()
            .CountAsync(inventory => inventory.CompanyId == companyId, cancellationToken);
        var availableMachines = await dbContext.Machines
            .AsNoTracking()
            .CountAsync(machine => machine.CompanyId == companyId
                && machine.Status == MachineStatuses.Available, cancellationToken);
        var totalMachines = await dbContext.Machines
            .AsNoTracking()
            .CountAsync(machine => machine.CompanyId == companyId, cancellationToken);
        var machineAlerts = await dbContext.Machines
            .AsNoTracking()
            .CountAsync(machine => machine.CompanyId == companyId
                && (machine.Status == MachineStatuses.Maintenance
                    || machine.Status == MachineStatuses.Offline), cancellationToken);
        var documentAlerts = await dbContext.Documents
            .AsNoTracking()
            .CountAsync(document => document.CompanyId == companyId
                && document.Status == DocumentStatuses.Failed, cancellationToken);

        return new DashboardSummary(
            planning?.Active ?? 0,
            inventoryBalances,
            availableMachines,
            totalMachines,
            machineAlerts + documentAlerts,
            planning?.Overdue ?? 0,
            planning?.DueSoon ?? 0,
            planning?.UrgentActive ?? 0,
            planning?.CompletedLate ?? 0,
            planning?.ActiveWithoutDueDate ?? 0);
    }
}
