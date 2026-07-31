using FactoryMind.Application.Features.Dashboard;
using FactoryMind.Domain.Knowledge;
using FactoryMind.Domain.Manufacturing;
using Microsoft.EntityFrameworkCore;

namespace FactoryMind.Infrastructure.Persistence.Dashboard;

public sealed class EfDashboardRepository(
    FactoryMindDbContext dbContext) : IDashboardRepository {
    public async Task<DashboardSummary> GetSummaryAsync(
        Guid companyId,
        CancellationToken cancellationToken) {
        var activeOrders = await dbContext.ProductionOrders
            .AsNoTracking()
            .CountAsync(order => order.CompanyId == companyId
                && (order.Status == ProductionOrderStatuses.Planned
                    || order.Status == ProductionOrderStatuses.InProgress), cancellationToken);
        var inventoryBalances = await dbContext.Inventories
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
            activeOrders,
            inventoryBalances,
            availableMachines,
            totalMachines,
            machineAlerts + documentAlerts);
    }
}
