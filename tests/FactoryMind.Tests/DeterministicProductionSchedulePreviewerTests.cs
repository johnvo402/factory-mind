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
    public void Order_and_priority_filters_select_from_canonical_schedule_without_removing_contention() {
        var center = Center(1);
        var firstOrder = Order("PO-A", Operation(center, 60)) with { Priority = ProductionOrderPriorities.Urgent };
        var targetOrder = Order("PO-B", Operation(center, 60));
        var data = Data([firstOrder, targetOrder], [center]);
        var canonical = previewer.Calculate(data, Now, 1, 3, CancellationToken.None).Value!;

        var filtered = SchedulePreviewViews.ApplyFilters(
            canonical, data, ProductionOrderPriorities.Normal, targetOrder.Id, null);

        var canonicalTarget = canonical.Orders.Single(order => order.Id == targetOrder.Id);
        Assert.Equal(canonicalTarget.ProjectedCompletion, filtered.Orders.Single().ProjectedCompletion);
        Assert.Equal(Now.AddHours(2), filtered.Orders.Single().ProjectedCompletion);
        Assert.Equal(1, filtered.Summary.OrdersConsidered);
        Assert.Equal(canonical.WorkCenters.Single().ScheduledMinutes, filtered.WorkCenters.Single().ScheduledMinutes);
    }

    [Fact]
    public void Work_center_filter_preserves_indirect_upstream_contention() {
        var cut = Center(1, "CUT");
        var paint = Center(1, "PAINT");
        var targetOrder = Order("PO-A", Operation(cut, 60, 1), Operation(paint, 60, 2));
        var competingOrder = Order("PO-B", Operation(cut, 60)) with { Priority = ProductionOrderPriorities.Urgent };
        var data = Data([targetOrder, competingOrder], [cut, paint]);
        var canonical = previewer.Calculate(data, Now, 1, 3, CancellationToken.None).Value!;

        var filtered = SchedulePreviewViews.ApplyFilters(canonical, data, null, null, paint.Id);

        Assert.Equal([targetOrder.Id], filtered.Orders.Select(order => order.Id).ToArray());
        Assert.Equal([paint.Id], filtered.WorkCenters.Select(center => center.Id).ToArray());
        Assert.Equal(Now.AddHours(2), filtered.Orders.Single().Operations.Single(operation =>
            operation.WorkCenterId == paint.Id).ScheduledStart);
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
    public void In_progress_predecessor_without_calendar_blocks_successor() {
        var unavailable = Center(1, "NO-CALENDAR", []);
        var valid = Center(1, "VALID");
        var order = ExecutionOrder("PO-BLOCKED",
            Operation(unavailable, 60, 10) with {
                Status = ProductionOperationStatuses.InProgress,
                StartedAt = Now
            },
            Operation(valid, 60, 20));

        var result = previewer.Calculate(Data([order], [unavailable, valid]),
            Now.AddHours(1), 1, 3, CancellationToken.None).Value!;

        Assert.Empty(result.Orders.Single().Operations);
        AssertReasonSequence(result, ScheduleUnscheduledReasons.CalendarMissing);
    }

    [Fact]
    public void In_progress_predecessor_outside_horizon_blocks_successor() {
        var first = Center(1, "LONG");
        var second = Center(1, "VALID");
        var order = ExecutionOrder("PO-HORIZON",
            Operation(first, 600, 10) with {
                Status = ProductionOperationStatuses.InProgress,
                StartedAt = Now
            },
            Operation(second, 60, 20));

        var result = previewer.Calculate(Data([order], [first, second]),
            Now.AddHours(1), 1, 3, CancellationToken.None).Value!;

        Assert.Empty(result.Orders.Single().Operations);
        AssertReasonSequence(result, ScheduleUnscheduledReasons.HorizonExceeded);
    }

    [Fact]
    public void Pending_inactive_predecessor_blocks_successor() {
        var inactive = Center(1, "INACTIVE") with { IsActive = false };
        var valid = Center(1, "VALID");
        var order = Order("PO-INACTIVE", Operation(inactive, 60, 10), Operation(valid, 60, 20));

        var result = previewer.Calculate(Data([order], [inactive, valid]),
            Now, 1, 3, CancellationToken.None).Value!;

        Assert.Empty(result.Orders.Single().Operations);
        AssertReasonSequence(result, ScheduleUnscheduledReasons.WorkCenterInactive);
    }

    [Fact]
    public void Pending_calendar_failure_preserves_root_reason_and_blocks_every_successor() {
        var unavailable = Center(1, "NO-CALENDAR", []);
        var valid = Center(1, "VALID");
        var order = Order("PO-CHAIN",
            Operation(unavailable, 60, 10),
            Operation(valid, 60, 20),
            Operation(valid, 60, 30));

        var result = previewer.Calculate(Data([order], [unavailable, valid]),
            Now, 1, 3, CancellationToken.None).Value!;

        Assert.Empty(result.Orders.Single().Operations);
        Assert.Equal([
            ScheduleUnscheduledReasons.CalendarMissing,
            ScheduleUnscheduledReasons.BlockedByPredecessor,
            ScheduleUnscheduledReasons.BlockedByPredecessor
        ], result.Unscheduled.OrderBy(item => item.OperationName).Select(item => item.Reason).ToArray());
    }

    [Fact]
    public void All_completed_active_orders_use_latest_actual_completion_for_delivery_projection() {
        var center = Center(1);
        var completedAt = Now.AddHours(3);
        ScheduleOrderInput CompletedOrder(string number, DateTime dueDate) => ExecutionOrder(number,
            Operation(center, 60, 10) with {
                Status = ProductionOperationStatuses.Completed,
                StartedAt = Now,
                CompletedAt = Now.AddHours(1)
            },
            Operation(center, 60, 20) with {
                Status = ProductionOperationStatuses.Completed,
                StartedAt = Now.AddHours(2),
                CompletedAt = completedAt
            }) with { DueDate = dueDate };
        var onTime = CompletedOrder("PO-READY-A", Now.AddHours(4));
        var late = CompletedOrder("PO-READY-B", Now.AddHours(2));

        var result = previewer.Calculate(Data([onTime, late], [center]),
            Now.AddHours(5), 1, 3, CancellationToken.None).Value!;

        Assert.All(result.Orders, order => {
            Assert.Equal(completedAt, order.ProjectedCompletion);
            Assert.Empty(order.Operations);
        });
        Assert.Equal(ProjectedDeliveryStatuses.OnTime,
            result.Orders.Single(order => order.Id == onTime.Id).ProjectedDeliveryStatus);
        Assert.Equal(ProjectedDeliveryStatuses.Late,
            result.Orders.Single(order => order.Id == late.Id).ProjectedDeliveryStatus);
    }

    [Fact]
    public void Missing_actual_completion_keeps_ready_order_unknown_and_blocks_any_successor() {
        var center = Center(1);
        var missingTimestamp = Operation(center, 60, 10) with {
            Status = ProductionOperationStatuses.Completed,
            StartedAt = Now,
            CompletedAt = null
        };
        var ready = ExecutionOrder("PO-READY-UNKNOWN", missingTimestamp);
        var invalidChain = ExecutionOrder("PO-CHAIN-UNKNOWN", missingTimestamp with { Id = Guid.NewGuid() },
            Operation(center, 60, 20));

        var result = previewer.Calculate(Data([ready, invalidChain], [center]),
            Now.AddHours(2), 1, 3, CancellationToken.None).Value!;

        Assert.Null(result.Orders.Single(order => order.Id == ready.Id).ProjectedCompletion);
        Assert.Equal(ProjectedDeliveryStatuses.Unknown,
            result.Orders.Single(order => order.Id == ready.Id).ProjectedDeliveryStatus);
        Assert.Empty(result.Orders.Single(order => order.Id == invalidChain.Id).Operations);
        Assert.Equal(ScheduleUnscheduledReasons.BlockedByPredecessor,
            result.Unscheduled.Single(item => item.OrderId == invalidChain.Id).Reason);
    }

    [Fact]
    public void Canonical_workload_limits_are_checked_before_output_filters() {
        var center = Center(1);
        var data = Data([
            Order("PO-A", Operation(center, 60)),
            Order("PO-B", Operation(center, 60))
        ], [center]);

        Assert.True(SchedulePreviewWorkload.ExceedsLimit(data, new PlanningSettings {
            MaximumOrdersPerPreview = 1,
            MaximumOperationsPerPreview = 10
        }));
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

    private static ScheduleOrderInput ExecutionOrder(string number, params ScheduleOperationInput[] operations) =>
        Order(number, operations) with {
            Status = ProductionOrderStatuses.InProgress,
            StartedAt = Now,
            PlanningSource = PlanningSources.LockedSnapshot,
            IsProvisional = false
        };

    private static void AssertReasonSequence(SchedulePreviewResponse result, string rootReason) {
        Assert.Equal(rootReason, result.Unscheduled.Single(item => item.OperationName == "Operation 10").Reason);
        Assert.Equal(ScheduleUnscheduledReasons.BlockedByPredecessor,
            result.Unscheduled.Single(item => item.OperationName == "Operation 20").Reason);
        Assert.Null(result.Orders.Single().ProjectedCompletion);
    }

    private static ScheduleOperationInput Operation(
        ScheduleWorkCenterInput center,
        int minutes,
        int sequence = 1) => new(
        Guid.NewGuid(), sequence, $"Operation {sequence}", center.Id, center.Code, center.Name,
        0, minutes, ProductionOperationStatuses.Pending, null, null);
}
