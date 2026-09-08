namespace FactoryMind.Application.Features.Dashboard;

public sealed record DashboardSummary(
    int ActiveOrders,
    int InventoryBalances,
    int AvailableMachines,
    int TotalMachines,
    int Alerts,
    int OverdueOrders,
    int DueSoonOrders,
    int UrgentActiveOrders,
    int CompletedLateOrders,
    int OrdersWithoutDueDate);

public interface IDashboardRepository {
    Task<DashboardSummary> GetSummaryAsync(
        Guid companyId,
        CancellationToken cancellationToken);
}
