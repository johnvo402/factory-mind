using System.Diagnostics;
using FactoryMind.Application.Common.Authorization;
using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.WorkCenters;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;
using FactoryMind.Shared.Observability;
using Mediator;

namespace FactoryMind.Application.Features.ProductionOrders;

public static class PlanningSources {
    public const string ActiveRouting = "active_routing";
    public const string ActiveRoutingMissing = "active_routing_missing";
    public const string LockedSnapshot = "locked_snapshot";
    public const string OperationSnapshotMissing = "operation_snapshot_missing";
}

public static class ProjectedDeliveryStatuses {
    public const string Unknown = "unknown";
    public const string OnTime = "projected_on_time";
    public const string Late = "projected_late";
}

public static class ScheduleUnscheduledReasons {
    public const string ActiveRoutingMissing = "active_routing_missing";
    public const string OperationSnapshotMissing = "operation_snapshot_missing";
    public const string WorkCenterMissing = "work_center_missing";
    public const string WorkCenterInactive = "work_center_inactive";
    public const string CalendarMissing = "calendar_missing";
    public const string HorizonExceeded = "horizon_exceeded";
    public const string CurrentCapacityConflict = "current_capacity_conflict";
    public const string BlockedByPredecessor = "blocked_by_predecessor";
}

public sealed record SchedulePreviewQuery(
    int? HorizonDays,
    string? Priority = null,
    Guid? OrderId = null,
    Guid? WorkCenterId = null)
    : IRequest<Result<SchedulePreviewResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}

public sealed record SchedulePreviewData(
    string TimeZoneId,
    IReadOnlyList<ScheduleOrderInput> Orders,
    IReadOnlyList<ScheduleWorkCenterInput> WorkCenters);

public sealed record ScheduleOrderInput(
    Guid Id,
    string Number,
    string ProductCode,
    string ProductName,
    string Status,
    string Priority,
    string DeliveryStatus,
    DateTime? DueDate,
    DateTime? StartedAt,
    string PlanningSource,
    bool IsProvisional,
    IReadOnlyList<ScheduleOperationInput> Operations);

public sealed record ScheduleOperationInput(
    Guid Id,
    int Sequence,
    string Name,
    Guid WorkCenterId,
    string WorkCenterCode,
    string WorkCenterName,
    int SetupTimeMinutes,
    int RunTimeMinutes,
    string Status,
    DateTime? StartedAt,
    DateTime? CompletedAt);

public sealed record ScheduleWorkCenterInput(
    Guid Id,
    string Code,
    string Name,
    bool IsActive,
    int ParallelCapacity,
    IReadOnlyList<WorkCenterShift> Shifts,
    IReadOnlyList<WorkCenterDayOff> DaysOff);

public interface ISchedulePreviewRepository {
    Task<SchedulePreviewData?> LoadAsync(
        Guid companyId,
        CancellationToken cancellationToken);
}

public static class SchedulePreviewWorkload {
    public static bool ExceedsLimit(SchedulePreviewData data, PlanningSettings settings) =>
        data.Orders.Count > settings.MaximumOrdersPerPreview
        || data.Orders.Sum(order => order.Operations.Count) > settings.MaximumOperationsPerPreview;
}

public sealed record SchedulePreviewSummary(
    int OrdersConsidered,
    int OrdersScheduled,
    int ProjectedOnTime,
    int ProjectedLate,
    int Unscheduled,
    int CapacityConstrainedWorkCenters,
    int OperationsScheduled);

public sealed record ScheduleOperationPreview(
    Guid Id,
    int Sequence,
    string Name,
    Guid WorkCenterId,
    string WorkCenterCode,
    string WorkCenterName,
    int Lane,
    int StandardDurationMinutes,
    int PlannedDurationMinutes,
    DateTime ScheduledStart,
    DateTime ScheduledEnd,
    string PlanningSource,
    bool IsProvisional,
    bool IsInProgress);

public sealed record ScheduleOrderPreview(
    Guid Id,
    string Number,
    string ProductCode,
    string ProductName,
    string Status,
    string Priority,
    string DeliveryStatus,
    DateTime? DueDate,
    string PlanningSource,
    bool IsProvisional,
    DateTime? ProjectedStart,
    DateTime? ProjectedCompletion,
    string ProjectedDeliveryStatus,
    int? ProjectedLatenessMinutes,
    IReadOnlyList<ScheduleOperationPreview> Operations);

public sealed record WorkCenterCapacityPreview(
    Guid Id,
    string Code,
    string Name,
    int ParallelCapacity,
    int AvailableCapacityMinutes,
    int ScheduledMinutes,
    int UnscheduledDemandMinutes,
    decimal? PlannedLoadPercent,
    int ScheduledOperationCount,
    int UnscheduledOperationCount,
    bool HasCapacityConstraint,
    bool IsActive,
    bool HasCalendar);

public sealed record UnscheduledOperationPreview(
    Guid OrderId,
    string OrderNumber,
    Guid? OperationId,
    string? OperationName,
    Guid? WorkCenterId,
    string? WorkCenterCode,
    int DemandMinutes,
    string Reason);

public sealed record SchedulePreviewResponse(
    DateTime GeneratedAt,
    DateTime HorizonStart,
    DateTime HorizonEnd,
    string TimeZoneId,
    SchedulePreviewSummary Summary,
    IReadOnlyList<ScheduleOrderPreview> Orders,
    IReadOnlyList<WorkCenterCapacityPreview> WorkCenters,
    IReadOnlyList<UnscheduledOperationPreview> Unscheduled);

public static class SchedulePreviewViews {
    public static SchedulePreviewResponse ApplyFilters(
        SchedulePreviewResponse canonical,
        SchedulePreviewData workload,
        string? priority,
        Guid? orderId,
        Guid? workCenterId) {
        var relevantOrderIds = workCenterId.HasValue
            ? workload.Orders
                .Where(order => order.Operations.Any(operation => operation.WorkCenterId == workCenterId.Value))
                .Select(order => order.Id)
                .ToHashSet()
            : null;
        var orders = canonical.Orders
            .Where(order => priority is null || order.Priority == priority)
            .Where(order => !orderId.HasValue || order.Id == orderId.Value)
            .Where(order => relevantOrderIds is null || relevantOrderIds.Contains(order.Id))
            .ToList();
        var returnedOrderIds = orders.Select(order => order.Id).ToHashSet();
        var workCenters = canonical.WorkCenters
            .Where(center => !workCenterId.HasValue || center.Id == workCenterId.Value)
            .ToList();
        var unscheduled = canonical.Unscheduled
            .Where(item => returnedOrderIds.Contains(item.OrderId))
            .ToList();
        var summary = new SchedulePreviewSummary(
            orders.Count,
            orders.Count(order => order.ProjectedCompletion.HasValue),
            orders.Count(order => order.ProjectedDeliveryStatus == ProjectedDeliveryStatuses.OnTime),
            orders.Count(order => order.ProjectedDeliveryStatus == ProjectedDeliveryStatuses.Late),
            unscheduled.Select(item => item.OrderId).Distinct().Count(),
            workCenters.Count(center => center.HasCapacityConstraint),
            orders.Sum(order => order.Operations.Count));
        return canonical with {
            Summary = summary,
            Orders = orders,
            WorkCenters = workCenters,
            Unscheduled = unscheduled
        };
    }
}

public interface IProductionSchedulePreviewer {
    Result<SchedulePreviewResponse> Calculate(
        SchedulePreviewData data,
        DateTime generatedAt,
        int horizonDays,
        int dueSoonDays,
        CancellationToken cancellationToken);
}

public sealed class SchedulePreviewQueryHandler(
    ISchedulePreviewRepository repository,
    IProductionSchedulePreviewer previewer,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    PlanningSettings settings)
    : IRequestHandler<SchedulePreviewQuery, Result<SchedulePreviewResponse>> {
    public async ValueTask<Result<SchedulePreviewResponse>> Handle(
        SchedulePreviewQuery query,
        CancellationToken cancellationToken) {
        var horizonDays = query.HorizonDays ?? settings.DefaultScheduleHorizonDays;
        if (horizonDays < 1 || horizonDays > settings.MaximumScheduleHorizonDays) {
            return Result<SchedulePreviewResponse>.Failure(PlanningErrors.InvalidHorizon);
        }
        if (query.Priority is not null && !ProductionOrderPriorities.All.Contains(query.Priority)) {
            return Result<SchedulePreviewResponse>.Failure(PlanningErrors.InvalidFilter);
        }

        using var activity = FactoryMindTelemetry.ActivitySource.StartActivity("planning.schedule_preview");
        var started = Stopwatch.GetTimestamp();
        var data = await repository.LoadAsync(currentUser.CompanyId, cancellationToken);
        if (data is null) return Result<SchedulePreviewResponse>.Failure(PlanningCalendarErrors.InvalidTimeZone);
        if (SchedulePreviewWorkload.ExceedsLimit(data, settings)) {
            return Result<SchedulePreviewResponse>.Failure(PlanningErrors.PreviewTooLarge);
        }

        var result = previewer.Calculate(
            data,
            timeProvider.GetUtcNow().UtcDateTime,
            horizonDays,
            settings.DueSoonDays,
            cancellationToken);
        var outcome = result.IsSuccess ? "success" : result.Error?.Code ?? "failure";
        FactoryMindTelemetry.PlanningPreviewDuration.Record(
            Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            FactoryMindTelemetry.Tags(("horizon_days", horizonDays), ("outcome", outcome)));
        if (result.Value is { } preview) {
            FactoryMindTelemetry.PlanningPreviewOrders.Record(preview.Summary.OrdersScheduled);
            FactoryMindTelemetry.PlanningPreviewOperations.Record(preview.Summary.OperationsScheduled);
            FactoryMindTelemetry.PlanningPreviewUnscheduled.Record(preview.Summary.Unscheduled);
            activity?.SetTag("planning.orders_scheduled", preview.Summary.OrdersScheduled);
            activity?.SetTag("planning.operations_scheduled", preview.Summary.OperationsScheduled);
            activity?.SetTag("planning.unscheduled", preview.Summary.Unscheduled);
            activity?.SetTag("planning.horizon_days", horizonDays);
        }
        activity?.SetTag("planning.outcome", outcome);
        return result.IsFailure
            ? result
            : Result<SchedulePreviewResponse>.Success(SchedulePreviewViews.ApplyFilters(
                result.Value!, data, query.Priority, query.OrderId, query.WorkCenterId));
    }
}

public static class PlanningErrors {
    public static readonly Error InvalidHorizon = new(
        "planning.invalid_horizon", "The schedule preview horizon is outside the allowed range.", 400);
    public static readonly Error PreviewTooLarge = new(
        "planning.preview_too_large", "The schedule preview workload exceeds the configured limit.", 400);
    public static readonly Error InvalidFilter = new(
        "planning.invalid_filter", "A schedule preview filter is invalid.", 400);
}

public sealed class DeterministicProductionSchedulePreviewer(IWorkCenterCalendarService calendars)
    : IProductionSchedulePreviewer {
    public Result<SchedulePreviewResponse> Calculate(
        SchedulePreviewData data,
        DateTime generatedAt,
        int horizonDays,
        int dueSoonDays,
        CancellationToken cancellationToken) {
        generatedAt = DateTime.SpecifyKind(generatedAt, DateTimeKind.Utc);
        var horizonEnd = generatedAt.AddDays(horizonDays);
        var workCenters = data.WorkCenters.OrderBy(item => item.Code).ThenBy(item => item.Id).ToList();
        var states = new Dictionary<Guid, CenterState>();
        foreach (var center in workCenters) {
            var expanded = calendars.Expand(center.Shifts, center.DaysOff, data.TimeZoneId, generatedAt, horizonEnd);
            if (expanded.IsFailure) return Result<SchedulePreviewResponse>.Failure(expanded.Error!);
            states[center.Id] = new CenterState(center, expanded.Value!);
        }

        var scheduledByOrder = data.Orders.ToDictionary(
            order => order.Id,
            _ => new List<ScheduleOperationPreview>());
        var unscheduled = new List<UnscheduledOperationPreview>();
        SeedInProgress(data, states, scheduledByOrder, unscheduled, generatedAt, horizonEnd, cancellationToken);

        var ordered = data.Orders
            .OrderBy(order => RiskRank(order, generatedAt, dueSoonDays))
            .ThenBy(PriorityRank)
            .ThenBy(order => order.DueDate is null)
            .ThenBy(order => order.DueDate)
            .ThenBy(order => order.Number, StringComparer.Ordinal)
            .ThenBy(order => order.Id)
            .ToList();
        foreach (var order in ordered) {
            cancellationToken.ThrowIfCancellationRequested();
            if (order.Operations.Count == 0) {
                AddMissingOperations(order, unscheduled);
                continue;
            }

            var earliest = generatedAt;
            var predecessorBlocked = false;
            foreach (var operation in order.Operations.OrderBy(item => item.Sequence).ThenBy(item => item.Id)) {
                if (operation.Status == ProductionOperationStatuses.Completed) {
                    if (!operation.CompletedAt.HasValue) {
                        predecessorBlocked = true;
                    } else if (!predecessorBlocked && operation.CompletedAt > earliest) {
                        earliest = operation.CompletedAt.Value;
                    }
                    continue;
                }
                if (operation.Status == ProductionOperationStatuses.InProgress) {
                    var existing = scheduledByOrder[order.Id].SingleOrDefault(item => item.Id == operation.Id);
                    if (existing is null) {
                        predecessorBlocked = true;
                    } else if (!predecessorBlocked) {
                        earliest = existing.ScheduledEnd;
                    }
                    continue;
                }
                if (predecessorBlocked) {
                    AddUnscheduled(order, operation, ScheduleUnscheduledReasons.BlockedByPredecessor, unscheduled);
                    continue;
                }
                var failure = ValidateCenter(operation, states, out var state);
                if (failure is not null) {
                    AddUnscheduled(order, operation, failure, unscheduled);
                    predecessorBlocked = true;
                    continue;
                }
                var duration = operation.SetupTimeMinutes + operation.RunTimeMinutes;
                var placement = state!.Place(earliest, duration, horizonEnd);
                if (placement is null) {
                    AddUnscheduled(order, operation, ScheduleUnscheduledReasons.HorizonExceeded, unscheduled);
                    predecessorBlocked = true;
                    continue;
                }
                var preview = ToPreview(order, operation, placement.Value, duration, false);
                scheduledByOrder[order.Id].Add(preview);
                state.ScheduledMinutes += duration;
                state.ScheduledOperationCount++;
                earliest = preview.ScheduledEnd;
            }
        }

        var orderPreviews = ordered.Select(order => ToOrderPreview(
            order, scheduledByOrder[order.Id], unscheduled, generatedAt)).ToList();
        var capacity = workCenters.Select(center => {
            var state = states[center.Id];
            var available = checked((int)Math.Min(int.MaxValue,
                state.Intervals.Sum(interval => (long)(interval.EndUtc - interval.StartUtc).TotalMinutes)
                * center.ParallelCapacity));
            var centerUnscheduled = unscheduled.Where(item => item.WorkCenterId == center.Id).ToList();
            var unscheduledDemand = centerUnscheduled.Sum(item => item.DemandMinutes);
            var hasCapacityConstraint = centerUnscheduled.Any(item => item.Reason is
                ScheduleUnscheduledReasons.HorizonExceeded or
                ScheduleUnscheduledReasons.CurrentCapacityConflict);
            decimal? load = available == 0 ? null : Math.Round(state.ScheduledMinutes * 100m / available, 1);
            return new WorkCenterCapacityPreview(
                center.Id, center.Code, center.Name, center.ParallelCapacity, available,
                state.ScheduledMinutes, unscheduledDemand, load, state.ScheduledOperationCount,
                centerUnscheduled.Count, hasCapacityConstraint, center.IsActive, center.Shifts.Count > 0);
        }).ToList();
        var summary = new SchedulePreviewSummary(
            ordered.Count,
            orderPreviews.Count(order => order.ProjectedCompletion.HasValue),
            orderPreviews.Count(order => order.ProjectedDeliveryStatus == ProjectedDeliveryStatuses.OnTime),
            orderPreviews.Count(order => order.ProjectedDeliveryStatus == ProjectedDeliveryStatuses.Late),
            unscheduled.Select(item => item.OrderId).Distinct().Count(),
            capacity.Count(item => item.HasCapacityConstraint),
            scheduledByOrder.Values.Sum(items => items.Count));
        return Result<SchedulePreviewResponse>.Success(new SchedulePreviewResponse(
            generatedAt, generatedAt, horizonEnd, data.TimeZoneId, summary,
            orderPreviews, capacity, unscheduled));
    }

    private void SeedInProgress(
        SchedulePreviewData data,
        IReadOnlyDictionary<Guid, CenterState> states,
        IReadOnlyDictionary<Guid, List<ScheduleOperationPreview>> scheduledByOrder,
        ICollection<UnscheduledOperationPreview> unscheduled,
        DateTime generatedAt,
        DateTime horizonEnd,
        CancellationToken cancellationToken) {
        var active = data.Orders.SelectMany(order => order.Operations
                .Where(operation => operation.Status == ProductionOperationStatuses.InProgress)
                .Select(operation => (Order: order, Operation: operation)))
            .OrderBy(item => item.Operation.StartedAt).ThenBy(item => item.Operation.Id)
            .GroupBy(item => item.Operation.WorkCenterId);
        foreach (var group in active) {
            var used = 0;
            foreach (var item in group) {
                cancellationToken.ThrowIfCancellationRequested();
                var failure = ValidateCenter(item.Operation, states, out var state);
                if (failure is not null) {
                    AddUnscheduled(item.Order, item.Operation, failure, unscheduled);
                    continue;
                }
                var standard = item.Operation.SetupTimeMinutes + item.Operation.RunTimeMinutes;
                var startedAt = item.Operation.StartedAt ?? item.Order.StartedAt ?? generatedAt;
                var elapsedResult = calendars.Expand(
                    state!.Input.Shifts, state.Input.DaysOff, data.TimeZoneId, startedAt, generatedAt);
                if (elapsedResult.IsFailure) continue;
                var elapsed = (int)elapsedResult.Value!.Sum(interval => (interval.EndUtc - interval.StartUtc).TotalMinutes);
                var remaining = Math.Max(standard - elapsed, 0);
                used++;
                if (used > state.Input.ParallelCapacity) {
                    AddUnscheduled(item.Order, item.Operation, ScheduleUnscheduledReasons.CurrentCapacityConflict, unscheduled, remaining);
                    var conflictProjection = state.ProjectWorkingTime(generatedAt, remaining, horizonEnd);
                    if (conflictProjection.HasValue) {
                        var conflictLane = (used - 1) % state.Input.ParallelCapacity + 1;
                        state.ReserveConflict(conflictLane, generatedAt, conflictProjection.Value);
                        scheduledByOrder[item.Order.Id].Add(ToPreview(
                            item.Order, item.Operation,
                            (conflictLane, startedAt, conflictProjection.Value), standard, true, remaining));
                        state.ScheduledMinutes += remaining;
                        state.ScheduledOperationCount++;
                    }
                    continue;
                }
                var lane = used;
                var placement = state.PlaceOnLane(lane, generatedAt, remaining, horizonEnd);
                if (placement is null) {
                    AddUnscheduled(item.Order, item.Operation, ScheduleUnscheduledReasons.HorizonExceeded, unscheduled, remaining);
                    continue;
                }
                var preview = ToPreview(item.Order, item.Operation,
                    (lane, startedAt, placement.Value.End), standard, true, remaining);
                scheduledByOrder[item.Order.Id].Add(preview);
                state.ScheduledMinutes += remaining;
                state.ScheduledOperationCount++;
            }
        }
    }

    private static string? ValidateCenter(
        ScheduleOperationInput operation,
        IReadOnlyDictionary<Guid, CenterState> states,
        out CenterState? state) {
        if (!states.TryGetValue(operation.WorkCenterId, out state)) return ScheduleUnscheduledReasons.WorkCenterMissing;
        if (!state.Input.IsActive) return ScheduleUnscheduledReasons.WorkCenterInactive;
        if (state.Input.Shifts.Count == 0) return ScheduleUnscheduledReasons.CalendarMissing;
        return null;
    }

    private static void AddMissingOperations(
        ScheduleOrderInput order,
        ICollection<UnscheduledOperationPreview> unscheduled) {
        var reason = order.PlanningSource == PlanningSources.ActiveRoutingMissing
            ? ScheduleUnscheduledReasons.ActiveRoutingMissing
            : ScheduleUnscheduledReasons.OperationSnapshotMissing;
        unscheduled.Add(new UnscheduledOperationPreview(
            order.Id, order.Number, null, null, null, null, 0, reason));
    }

    private static void AddUnscheduled(
        ScheduleOrderInput order,
        ScheduleOperationInput operation,
        string reason,
        ICollection<UnscheduledOperationPreview> unscheduled,
        int? demand = null) => unscheduled.Add(new UnscheduledOperationPreview(
        order.Id, order.Number, operation.Id, operation.Name, operation.WorkCenterId,
        operation.WorkCenterCode, demand ?? operation.SetupTimeMinutes + operation.RunTimeMinutes, reason));

    private static ScheduleOperationPreview ToPreview(
        ScheduleOrderInput order,
        ScheduleOperationInput operation,
        (int Lane, DateTime Start, DateTime End) placement,
        int standardDuration,
        bool inProgress,
        int? plannedDuration = null) => new(
        operation.Id, operation.Sequence, operation.Name, operation.WorkCenterId,
        operation.WorkCenterCode, operation.WorkCenterName, placement.Lane,
        standardDuration, plannedDuration ?? standardDuration, placement.Start, placement.End,
        order.PlanningSource, order.IsProvisional, inProgress);

    private static ScheduleOrderPreview ToOrderPreview(
        ScheduleOrderInput order,
        IReadOnlyList<ScheduleOperationPreview> operations,
        IReadOnlyCollection<UnscheduledOperationPreview> unscheduled,
        DateTime generatedAt) {
        var failed = unscheduled.Any(item => item.OrderId == order.Id);
        var expected = order.Operations.Count(operation => operation.Status != ProductionOperationStatuses.Completed);
        var complete = !failed && operations.Count == expected;
        var allOperationsCompleted = order.Operations.Count > 0
            && order.Operations.All(operation => operation.Status == ProductionOperationStatuses.Completed);
        var allCompletionTimesValid = allOperationsCompleted
            && order.Operations.All(operation => operation.CompletedAt.HasValue);
        var actualStart = order.StartedAt
            ?? order.Operations.Where(operation => operation.StartedAt.HasValue)
                .Select(operation => operation.StartedAt)
                .Min();
        DateTime? start = allOperationsCompleted
            ? actualStart
            : operations.Count == 0 ? null : operations.Min(operation => operation.ScheduledStart);
        DateTime? end = allCompletionTimesValid
            ? order.Operations.Max(operation => operation.CompletedAt)
            : complete && operations.Count > 0 ? operations.Max(operation => operation.ScheduledEnd) : null;
        var projectedStatus = order.DueDate is null || end is null
            ? ProjectedDeliveryStatuses.Unknown
            : end <= order.DueDate ? ProjectedDeliveryStatuses.OnTime : ProjectedDeliveryStatuses.Late;
        int? lateness = projectedStatus == ProjectedDeliveryStatuses.Late
            ? (int)Math.Ceiling((end!.Value - order.DueDate!.Value).TotalMinutes)
            : projectedStatus == ProjectedDeliveryStatuses.OnTime ? 0 : null;
        return new ScheduleOrderPreview(
            order.Id, order.Number, order.ProductCode, order.ProductName, order.Status, order.Priority,
            order.DeliveryStatus, order.DueDate, order.PlanningSource, order.IsProvisional,
            order.Status == ProductionOrderStatuses.InProgress ? order.StartedAt ?? start : start,
            end, projectedStatus, lateness, operations.OrderBy(item => item.Sequence).ToList());
    }

    private static int RiskRank(ScheduleOrderInput order, DateTime now, int dueSoonDays) =>
        order.DueDate is not { } due ? 3
        : due < now ? 0
        : due <= now.Date.AddDays(dueSoonDays + 1).AddTicks(-1) ? 1 : 2;

    private static int PriorityRank(ScheduleOrderInput order) => order.Priority switch {
        ProductionOrderPriorities.Urgent => 0,
        ProductionOrderPriorities.High => 1,
        ProductionOrderPriorities.Normal => 2,
        _ => 3
    };

    private sealed class CenterState {
        private readonly List<List<(DateTime Start, DateTime End)>> lanes;
        public CenterState(ScheduleWorkCenterInput input, IReadOnlyList<WorkingInterval> intervals) {
            Input = input;
            Intervals = intervals;
            lanes = Enumerable.Range(0, input.ParallelCapacity)
                .Select(_ => new List<(DateTime Start, DateTime End)>()).ToList();
        }
        public ScheduleWorkCenterInput Input { get; }
        public IReadOnlyList<WorkingInterval> Intervals { get; }
        public int ScheduledMinutes { get; set; }
        public int ScheduledOperationCount { get; set; }

        public (int Lane, DateTime Start, DateTime End)? Place(DateTime earliest, int minutes, DateTime horizonEnd) {
            var candidates = Enumerable.Range(1, lanes.Count)
                .Select(lane => PlaceCandidate(lane, earliest, minutes, horizonEnd))
                .Where(candidate => candidate.HasValue).Select(candidate => candidate!.Value)
                .OrderBy(candidate => candidate.End).ThenBy(candidate => candidate.Start).ThenBy(candidate => candidate.Lane)
                .ToList();
            if (candidates.Count == 0) return null;
            var selected = candidates[0];
            Reserve(selected.Lane, selected.Start, selected.End);
            return selected;
        }

        public (DateTime Start, DateTime End)? PlaceOnLane(int lane, DateTime earliest, int minutes, DateTime horizonEnd) {
            var candidate = PlaceCandidate(lane, earliest, minutes, horizonEnd);
            if (!candidate.HasValue) return null;
            Reserve(lane, candidate.Value.Start, candidate.Value.End);
            return (candidate.Value.Start, candidate.Value.End);
        }

        public DateTime? ProjectWorkingTime(DateTime earliest, int minutes, DateTime horizonEnd) {
            if (minutes == 0) return earliest;
            var consumed = 0;
            foreach (var interval in Intervals) {
                var start = interval.StartUtc > earliest ? interval.StartUtc : earliest;
                if (interval.EndUtc <= start) continue;
                var available = (int)(interval.EndUtc - start).TotalMinutes;
                if (consumed + available >= minutes) return start.AddMinutes(minutes - consumed);
                consumed += available;
            }
            return null;
        }

        public void ReserveConflict(int lane, DateTime start, DateTime end) => Reserve(lane, start, end);

        private (int Lane, DateTime Start, DateTime End)? PlaceCandidate(
            int lane, DateTime earliest, int minutes, DateTime horizonEnd) {
            if (minutes == 0) return (lane, earliest, earliest);
            var reservations = lanes[lane - 1].OrderBy(item => item.Start).ToList();
            var candidate = earliest;
            while (candidate < horizonEnd) {
                var nextReservation = reservations.FirstOrDefault(item => item.End > candidate);
                var stop = nextReservation == default ? horizonEnd : nextReservation.Start;
                var consumed = 0;
                DateTime? start = null;
                foreach (var interval in Intervals) {
                    var segmentStart = interval.StartUtc > candidate ? interval.StartUtc : candidate;
                    var segmentEnd = interval.EndUtc < stop ? interval.EndUtc : stop;
                    if (segmentEnd <= segmentStart) continue;
                    start ??= segmentStart;
                    var available = (int)(segmentEnd - segmentStart).TotalMinutes;
                    if (consumed + available >= minutes) {
                        return (lane, start.Value, segmentStart.AddMinutes(minutes - consumed));
                    }
                    consumed += available;
                }
                if (nextReservation == default) return null;
                candidate = nextReservation.End > candidate ? nextReservation.End : candidate.AddTicks(1);
            }
            return null;
        }

        private void Reserve(int lane, DateTime start, DateTime end) {
            if (end > start) lanes[lane - 1].Add((start, end));
        }
    }
}
