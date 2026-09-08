using System.Text.Json;
using FactoryMind.Application.Features.ProductionOrders;
using FactoryMind.Application.Features.WorkCenters;
using FactoryMind.Domain.Manufacturing;

namespace FactoryMind.Tests;

public sealed class DeterministicProductionSchedulePreviewerTests {
    private static readonly DateTime Now = new(2026, 9, 7, 8, 0, 0, DateTimeKind.Utc);
    private readonly DeterministicProductionSchedulePreviewer previewer =
        new(new WorkCenterCalendarService());

    [Fact]
    public void Capacity_one_serializes_and_stable_order_tie_breaks_by_number() {
        var center = Center(1);
        var data = Data([Order("PO-B", Operation(center, 60)), Order("PO-A", Operation(center, 60))], [center]);

        var result = previewer.Calculate(data, Now, 1, 3, CancellationToken.None).Value!;

        var first = result.Orders.Single(order => order.Number == "PO-A").Operations.Single();
        var second = result.Orders.Single(order => order.Number == "PO-B").Operations.Single();
        Assert.Equal(Now, first.ScheduledStart);
        Assert.Equal(Now.AddHours(1), second.ScheduledStart);
    }

    [Fact]
    public void Capacity_two_allows_parallel_operations_on_abstract_lanes() {
        var center = Center(2);
        var result = previewer.Calculate(
            Data([Order("PO-A", Operation(center, 60)), Order("PO-B", Operation(center, 60))], [center]),
            Now, 1, 3, CancellationToken.None).Value!;

        var operations = result.Orders.SelectMany(order => order.Operations).ToList();
        Assert.All(operations, operation => Assert.Equal(Now, operation.ScheduledStart));
        Assert.Equal([1, 2], operations.Select(operation => operation.Lane).Order().ToArray());
        Assert.Equal(960, result.WorkCenters.Single().AvailableCapacityMinutes);
    }

    [Fact]
    public void Different_work_centers_overlap_and_order_sequence_is_preserved() {
        var cut = Center(1, "CUT");
        var assembly = Center(1, "ASM");
        var parallelOrder = Order("PO-A", Operation(cut, 60));
        var sequenceOrder = Order("PO-B", Operation(assembly, 60, 1), Operation(cut, 60, 2));

        var result = previewer.Calculate(Data([parallelOrder, sequenceOrder], [cut, assembly]),
            Now, 1, 3, CancellationToken.None).Value!;

        var first = result.Orders.Single(order => order.Number == "PO-B").Operations[0];
        var second = result.Orders.Single(order => order.Number == "PO-B").Operations[1];
        Assert.Equal(Now, first.ScheduledStart);
        Assert.True(second.ScheduledStart >= first.ScheduledEnd);
        Assert.Equal(Now, result.Orders.Single(order => order.Number == "PO-A").Operations[0].ScheduledStart);
    }

    [Fact]
    public void Shift_break_is_excluded_from_operation_duration() {
        var center = Center(1, shifts: [Shift(1, 8, 12), Shift(1, 13, 17)]);
        var result = previewer.Calculate(Data([Order("PO-A", Operation(center, 180))], [center]),
            Now.AddHours(2), 1, 3, CancellationToken.None).Value!;

        Assert.Equal(new DateTime(2026, 9, 7, 14, 0, 0, DateTimeKind.Utc),
            result.Orders.Single().ProjectedCompletion);
    }

    [Fact]
    public void Overdue_precedes_future_and_urgent_precedes_normal_with_same_due_risk() {
        var center = Center(1);
        var future = Order("PO-FUTURE", Operation(center, 60)) with { DueDate = Now.AddDays(10) };
        var overdue = Order("PO-OVERDUE", Operation(center, 60)) with { DueDate = Now.AddMinutes(-1) };
        var normal = Order("PO-NORMAL", Operation(center, 60)) with { DueDate = Now.AddDays(1), Priority = ProductionOrderPriorities.Normal };
        var urgent = Order("PO-URGENT", Operation(center, 60)) with { DueDate = Now.AddDays(1), Priority = ProductionOrderPriorities.Urgent };

        var result = previewer.Calculate(Data([future, normal, overdue, urgent], [center]),
            Now, 2, 3, CancellationToken.None).Value!;
        var starts = result.Orders.ToDictionary(order => order.Number, order => order.Operations.Single().ScheduledStart);

        Assert.True(starts["PO-OVERDUE"] < starts["PO-URGENT"]);
        Assert.True(starts["PO-URGENT"] < starts["PO-NORMAL"]);
        Assert.True(starts["PO-NORMAL"] < starts["PO-FUTURE"]);
    }

    [Fact]
    public void Missing_calendar_and_missing_active_routing_are_explicit() {
        var center = Center(1, shifts: []);
        var missingRouting = Order("PO-NO-ROUTING") with {
            PlanningSource = PlanningSources.ActiveRoutingMissing,
            Operations = []
        };
        var result = previewer.Calculate(Data([
            missingRouting,
            Order("PO-NO-CALENDAR", Operation(center, 60))
        ], [center]), Now, 1, 3, CancellationToken.None).Value!;

        Assert.Contains(result.Unscheduled, item => item.Reason == ScheduleUnscheduledReasons.ActiveRoutingMissing);
        Assert.Contains(result.Unscheduled, item => item.Reason == ScheduleUnscheduledReasons.CalendarMissing);
    }

    [Fact]
    public void In_progress_remaining_uses_working_minutes_not_wall_time() {
        var center = Center(1);
        var operation = Operation(center, 180) with {
            Status = ProductionOperationStatuses.InProgress,
            StartedAt = Now
        };
        var order = Order("PO-RUN", operation) with {
            Status = ProductionOrderStatuses.InProgress,
            StartedAt = Now,
            IsProvisional = false,
            PlanningSource = PlanningSources.LockedSnapshot
        };

        var result = previewer.Calculate(Data([order], [center]), Now.AddHours(2), 1, 3, CancellationToken.None).Value!;
        var scheduled = result.Orders.Single().Operations.Single();

        Assert.Equal(60, scheduled.PlannedDurationMinutes);
        Assert.Equal(Now.AddHours(3), scheduled.ScheduledEnd);
    }

    [Fact]
    public void Horizon_exceeded_has_no_completion_and_due_null_is_unknown() {
        var center = Center(1);
        var result = previewer.Calculate(Data([Order("PO-LONG", Operation(center, 600))], [center]),
            Now, 1, 3, CancellationToken.None).Value!;

        Assert.Null(result.Orders.Single().ProjectedCompletion);
        Assert.Equal(ProjectedDeliveryStatuses.Unknown, result.Orders.Single().ProjectedDeliveryStatus);
        Assert.Equal(ScheduleUnscheduledReasons.HorizonExceeded, result.Unscheduled.Single().Reason);
    }

    [Fact]
    public void In_progress_work_above_parallel_capacity_is_surfaced_as_current_conflict() {
        var center = Center(1);
        ScheduleOrderInput Running(string number) => Order(number, Operation(center, 180) with {
            Status = ProductionOperationStatuses.InProgress,
            StartedAt = Now
        }) with {
            Status = ProductionOrderStatuses.InProgress,
            StartedAt = Now,
            IsProvisional = false,
            PlanningSource = PlanningSources.LockedSnapshot
        };

        var result = previewer.Calculate(Data([
            Running("PO-A"),
            Running("PO-B"),
            Order("PO-C", Operation(center, 60))
        ], [center]),
            Now.AddHours(1), 1, 3, CancellationToken.None).Value!;

        Assert.Contains(result.Unscheduled,
            item => item.Reason == ScheduleUnscheduledReasons.CurrentCapacityConflict);
        Assert.Equal(3, result.Orders.SelectMany(order => order.Operations).Count());
        Assert.Equal(Now.AddHours(3),
            result.Orders.Single(order => order.Number == "PO-C").Operations.Single().ScheduledStart);
    }

    [Fact]
    public void Projected_delivery_boundary_and_determinism_are_stable() {
        var center = Center(1);
        var onTime = Order("PO-A", Operation(center, 30)) with { DueDate = Now.AddMinutes(30) };
        var late = Order("PO-B", Operation(center, 31)) with { DueDate = Now.AddMinutes(60) };
        var data = Data([onTime, late], [center]);

        var first = previewer.Calculate(data, Now, 1, 3, CancellationToken.None).Value!;
        var second = previewer.Calculate(data, Now, 1, 3, CancellationToken.None).Value!;

        Assert.Equal(ProjectedDeliveryStatuses.OnTime, first.Orders.Single(order => order.Number == "PO-A").ProjectedDeliveryStatus);
        Assert.Equal(ProjectedDeliveryStatuses.Late, first.Orders.Single(order => order.Number == "PO-B").ProjectedDeliveryStatus);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
    }

    private static SchedulePreviewData Data(
        IReadOnlyList<ScheduleOrderInput> orders,
        IReadOnlyList<ScheduleWorkCenterInput> centers) => new("UTC", orders, centers);

    private static ScheduleWorkCenterInput Center(
        int capacity,
        string code = "WC",
        IReadOnlyList<WorkCenterShift>? shifts = null) => new(
        Guid.NewGuid(), code, code, true, capacity,
        shifts ?? [Shift(1, 8, 16)], []);

    private static WorkCenterShift Shift(int day, int start, int end) => new() {
        DayOfWeek = day,
        StartTime = new TimeOnly(start, 0),
        EndTime = new TimeOnly(end, 0)
    };

    private static ScheduleOrderInput Order(string number, params ScheduleOperationInput[] operations) => new(
        Guid.NewGuid(), number, "P", "Product", ProductionOrderStatuses.Planned,
        ProductionOrderPriorities.Normal, ProductionOrderDeliveryStatuses.NoDueDate,
        null, null, PlanningSources.ActiveRouting, true, operations);

    private static ScheduleOperationInput Operation(
        ScheduleWorkCenterInput center,
        int minutes,
        int sequence = 1) => new(
        Guid.NewGuid(), sequence, $"Operation {sequence}", center.Id, center.Code, center.Name,
        0, minutes, ProductionOperationStatuses.Pending, null, null);
}
