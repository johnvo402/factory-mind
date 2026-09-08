using FactoryMind.Application.Features.ProductionOrders;
using FactoryMind.Domain.Manufacturing;

namespace FactoryMind.Tests;

public sealed class ProductionOrderDeliveryRiskCalculatorTests {
    private static readonly DateTime UtcNow = new(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc);

    public static TheoryData<string, DateTime?, DateTime?, string, int?, bool, bool, bool> Cases => new() {
        { ProductionOrderStatuses.Planned, null, null, ProductionOrderDeliveryStatuses.NoDueDate, null, false, false, false },
        { ProductionOrderStatuses.Planned, EndOfDay(8), null, ProductionOrderDeliveryStatuses.DueSoon, 0, false, true, false },
        { ProductionOrderStatuses.Released, EndOfDay(9), null, ProductionOrderDeliveryStatuses.DueSoon, 1, false, true, false },
        { ProductionOrderStatuses.InProgress, EndOfDay(11), null, ProductionOrderDeliveryStatuses.DueSoon, 3, false, true, false },
        { ProductionOrderStatuses.Planned, EndOfDay(12), null, ProductionOrderDeliveryStatuses.OnTrack, 4, false, false, false },
        { ProductionOrderStatuses.InProgress, EndOfDay(7), null, ProductionOrderDeliveryStatuses.Overdue, -1, true, false, false },
        { ProductionOrderStatuses.Completed, EndOfDay(10), EndOfDay(9), ProductionOrderDeliveryStatuses.CompletedOnTime, 1, false, false, false },
        { ProductionOrderStatuses.Completed, EndOfDay(10), EndOfDay(10), ProductionOrderDeliveryStatuses.CompletedOnTime, 0, false, false, false },
        { ProductionOrderStatuses.Completed, EndOfDay(10), new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc), ProductionOrderDeliveryStatuses.CompletedLate, -1, false, false, true },
        { ProductionOrderStatuses.Cancelled, EndOfDay(7), null, ProductionOrderDeliveryStatuses.Cancelled, -1, false, false, false },
        { ProductionOrderStatuses.Cancelled, EndOfDay(12), null, ProductionOrderDeliveryStatuses.Cancelled, 4, false, false, false }
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Calculate_applies_deterministic_delivery_rules(
        string status,
        DateTime? dueDate,
        DateTime? completedAt,
        string expectedStatus,
        int? expectedDays,
        bool isOverdue,
        bool isDueSoon,
        bool isCompletedLate) {
        var calculator = CreateCalculator(UtcNow);
        var order = new ProductionOrder { Status = status, DueDate = dueDate, CompletedAt = completedAt };

        var result = calculator.Calculate(order);

        Assert.Equal(expectedStatus, result.DeliveryStatus);
        Assert.Equal(expectedDays, result.DaysUntilDue);
        Assert.Equal(isOverdue, result.IsOverdue);
        Assert.Equal(isDueSoon, result.IsDueSoon);
        Assert.Equal(isCompletedLate, result.IsCompletedLate);
    }

    [Fact]
    public void Calculate_uses_utc_calendar_date_at_boundary() {
        var calculator = CreateCalculator(new DateTime(2026, 9, 8, 23, 59, 59, DateTimeKind.Utc));
        var order = new ProductionOrder { DueDate = EndOfDay(9) };

        var result = calculator.Calculate(order);

        Assert.Equal(1, result.DaysUntilDue);
        Assert.Equal(ProductionOrderDeliveryStatuses.DueSoon, result.DeliveryStatus);
    }

    [Fact]
    public void NormalizeDueDate_truncates_sub_millisecond_precision_before_database_rounding() {
        var calculator = CreateCalculator(UtcNow);
        var clientEndOfDay = new DateTime(2026, 9, 9, 23, 59, 59, 999, DateTimeKind.Utc)
            .AddTicks(9_999);

        var normalized = calculator.NormalizeDueDate(clientEndOfDay);

        Assert.Equal(new DateTime(2026, 9, 9, 23, 59, 59, 999, DateTimeKind.Utc), normalized);
        Assert.Equal(
            new DateTime(2026, 9, 11, 23, 59, 59, 999, DateTimeKind.Utc),
            calculator.DueSoonThrough);
    }

    private static ProductionOrderDeliveryRiskCalculator CreateCalculator(DateTime utcNow) => new(
        new FixedTimeProvider(new DateTimeOffset(utcNow)),
        new PlanningSettings { DueSoonDays = 3 });

    private static DateTime EndOfDay(int day) =>
        new DateTime(2026, 9, day, 23, 59, 59, 999, DateTimeKind.Utc).AddTicks(9_999);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
