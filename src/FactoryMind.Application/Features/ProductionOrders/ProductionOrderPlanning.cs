using FactoryMind.Domain.Manufacturing;
using System.Linq.Expressions;

namespace FactoryMind.Application.Features.ProductionOrders;

public sealed class PlanningSettings {
    public const string SectionName = "Planning";
    public int DueSoonDays { get; set; } = 3;
    public int DefaultScheduleHorizonDays { get; set; } = 14;
    public int MaximumScheduleHorizonDays { get; set; } = 90;
    public int MaximumOrdersPerPreview { get; set; } = 500;
    public int MaximumOperationsPerPreview { get; set; } = 5000;
}

public sealed record ProductionOrderDeliveryRisk(
    string DeliveryStatus,
    int? DaysUntilDue,
    bool IsOverdue,
    bool IsDueSoon,
    bool IsCompletedLate);

public sealed record ProductionOrderPlanningSummary(
    int Active,
    int Overdue,
    int DueSoon,
    int UrgentActive,
    int CompletedLate,
    int ActiveWithoutDueDate);

public interface IProductionOrderDeliveryRiskCalculator {
    DateTime UtcNow { get; }
    DateTime DueSoonThrough { get; }
    int DueSoonDays { get; }
    DateTime? NormalizeDueDate(DateTime? dueDate);
    ProductionOrderDeliveryRisk Calculate(ProductionOrder order);
}

public sealed class ProductionOrderDeliveryRiskCalculator(
    TimeProvider timeProvider,
    PlanningSettings settings) : IProductionOrderDeliveryRiskCalculator {
    public DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;
    public DateTime DueSoonThrough => UtcNow.Date.AddDays(settings.DueSoonDays + 1).AddMilliseconds(-1);
    public int DueSoonDays => settings.DueSoonDays;

    public DateTime? NormalizeDueDate(DateTime? dueDate) {
        if (!dueDate.HasValue) return null;

        var utc = dueDate.Value.Kind switch {
            DateTimeKind.Utc => dueDate.Value,
            DateTimeKind.Local => dueDate.Value.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(dueDate.Value, DateTimeKind.Utc),
            _ => dueDate.Value
        };
        return new DateTime(
            utc.Ticks - utc.Ticks % TimeSpan.TicksPerMillisecond,
            DateTimeKind.Utc);
    }

    public ProductionOrderDeliveryRisk Calculate(ProductionOrder order) =>
        Calculate(order.Status, order.DueDate, order.CompletedAt, UtcNow, settings.DueSoonDays);

    public static ProductionOrderDeliveryRisk Calculate(
        string status,
        DateTime? dueDate,
        DateTime? completedAt,
        DateTime utcNow,
        int dueSoonDays) {
        if (status == ProductionOrderStatuses.Cancelled) {
            return new(ProductionOrderDeliveryStatuses.Cancelled, DaysUntil(dueDate, utcNow), false, false, false);
        }

        if (!dueDate.HasValue) {
            return new(ProductionOrderDeliveryStatuses.NoDueDate, null, false, false, false);
        }

        if (status == ProductionOrderStatuses.Completed && completedAt.HasValue) {
            var completedLate = completedAt.Value > dueDate.Value;
            return new(
                completedLate
                    ? ProductionOrderDeliveryStatuses.CompletedLate
                    : ProductionOrderDeliveryStatuses.CompletedOnTime,
                (dueDate.Value.Date - completedAt.Value.Date).Days,
                false,
                false,
                completedLate);
        }

        var daysUntilDue = (dueDate.Value.Date - utcNow.Date).Days;
        if (utcNow > dueDate.Value) {
            return new(ProductionOrderDeliveryStatuses.Overdue, daysUntilDue, true, false, false);
        }

        if (daysUntilDue <= dueSoonDays) {
            return new(ProductionOrderDeliveryStatuses.DueSoon, daysUntilDue, false, true, false);
        }

        return new(ProductionOrderDeliveryStatuses.OnTrack, daysUntilDue, false, false, false);
    }

    public static Expression<Func<ProductionOrder, bool>> DeliveryStatusPredicate(
        string deliveryStatus,
        DateTime utcNow,
        DateTime dueSoonThrough) => deliveryStatus switch {
            ProductionOrderDeliveryStatuses.Cancelled => order =>
                order.Status == ProductionOrderStatuses.Cancelled,
            ProductionOrderDeliveryStatuses.NoDueDate => order =>
                order.Status != ProductionOrderStatuses.Cancelled && order.DueDate == null,
            ProductionOrderDeliveryStatuses.Overdue => order =>
                (order.Status == ProductionOrderStatuses.Planned
                    || order.Status == ProductionOrderStatuses.Released
                    || order.Status == ProductionOrderStatuses.InProgress)
                && order.DueDate < utcNow,
            ProductionOrderDeliveryStatuses.DueSoon => order =>
                (order.Status == ProductionOrderStatuses.Planned
                    || order.Status == ProductionOrderStatuses.Released
                    || order.Status == ProductionOrderStatuses.InProgress)
                && order.DueDate >= utcNow
                && order.DueDate <= dueSoonThrough,
            ProductionOrderDeliveryStatuses.OnTrack => order =>
                (order.Status == ProductionOrderStatuses.Planned
                    || order.Status == ProductionOrderStatuses.Released
                    || order.Status == ProductionOrderStatuses.InProgress)
                && order.DueDate > dueSoonThrough,
            ProductionOrderDeliveryStatuses.CompletedLate => order =>
                order.Status == ProductionOrderStatuses.Completed
                && order.DueDate != null
                && order.CompletedAt != null
                && order.CompletedAt > order.DueDate,
            ProductionOrderDeliveryStatuses.CompletedOnTime => order =>
                order.Status == ProductionOrderStatuses.Completed
                && order.DueDate != null
                && order.CompletedAt != null
                && order.CompletedAt <= order.DueDate,
            _ => order => true
        };

    public static Expression<Func<ProductionOrder, int>> PlanningRankExpression(
        DateTime utcNow,
        DateTime dueSoonThrough) => order =>
        order.DueDate < utcNow
            && (order.Status == ProductionOrderStatuses.Planned
                || order.Status == ProductionOrderStatuses.Released
                || order.Status == ProductionOrderStatuses.InProgress) ? 0
            : order.DueDate >= utcNow
                && order.DueDate <= dueSoonThrough
                && (order.Status == ProductionOrderStatuses.Planned
                    || order.Status == ProductionOrderStatuses.Released
                    || order.Status == ProductionOrderStatuses.InProgress) ? 1
                : (order.Status == ProductionOrderStatuses.Planned
                        || order.Status == ProductionOrderStatuses.Released
                        || order.Status == ProductionOrderStatuses.InProgress)
                    && (order.Priority == ProductionOrderPriorities.Urgent
                        || order.Priority == ProductionOrderPriorities.High) ? 2 : 3;

    public static Expression<Func<IGrouping<int, ProductionOrder>, ProductionOrderPlanningSummary>>
        PlanningSummaryProjection(
        DateTime utcNow,
        DateTime dueSoonThrough) => group => new ProductionOrderPlanningSummary(
            group.Count(order => order.Status == ProductionOrderStatuses.Planned
                || order.Status == ProductionOrderStatuses.Released
                || order.Status == ProductionOrderStatuses.InProgress),
            group.Count(order =>
                (order.Status == ProductionOrderStatuses.Planned
                    || order.Status == ProductionOrderStatuses.Released
                    || order.Status == ProductionOrderStatuses.InProgress)
                && order.DueDate < utcNow),
            group.Count(order =>
                (order.Status == ProductionOrderStatuses.Planned
                    || order.Status == ProductionOrderStatuses.Released
                    || order.Status == ProductionOrderStatuses.InProgress)
                && order.DueDate >= utcNow
                && order.DueDate <= dueSoonThrough),
            group.Count(order => order.Priority == ProductionOrderPriorities.Urgent
                && (order.Status == ProductionOrderStatuses.Planned
                    || order.Status == ProductionOrderStatuses.Released
                    || order.Status == ProductionOrderStatuses.InProgress)),
            group.Count(order => order.Status == ProductionOrderStatuses.Completed
                && order.DueDate != null
                && order.CompletedAt != null
                && order.CompletedAt > order.DueDate),
            group.Count(order => order.DueDate == null
                && (order.Status == ProductionOrderStatuses.Planned
                    || order.Status == ProductionOrderStatuses.Released
                    || order.Status == ProductionOrderStatuses.InProgress)));

    private static int? DaysUntil(DateTime? dueDate, DateTime utcNow) =>
        dueDate.HasValue ? (dueDate.Value.Date - utcNow.Date).Days : null;
}
