using FactoryMind.Application.Common.Authorization;
using FactoryMind.Application.Common.Identity;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.WorkCenters;

public sealed record GetWorkCenterCalendarQuery(Guid WorkCenterId)
    : IRequest<Result<WorkCenterCalendarResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}

public sealed record ReplaceWorkCenterCalendarCommand(
    Guid WorkCenterId,
    int ParallelCapacity,
    IReadOnlyList<WorkCenterShiftInput> Shifts,
    IReadOnlyList<WorkCenterDayOffInput> DaysOff)
    : IRequest<Result<WorkCenterCalendarResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}

public sealed class GetWorkCenterCalendarQueryHandler(
    IWorkCenterCalendarRepository repository,
    ICurrentUser currentUser)
    : IRequestHandler<GetWorkCenterCalendarQuery, Result<WorkCenterCalendarResponse>> {
    public async ValueTask<Result<WorkCenterCalendarResponse>> Handle(
        GetWorkCenterCalendarQuery query,
        CancellationToken cancellationToken) {
        var (workCenter, timeZoneId) = await repository.GetAsync(
            query.WorkCenterId, currentUser.CompanyId, cancellationToken);
        return workCenter is null || timeZoneId is null
            ? Result<WorkCenterCalendarResponse>.Failure(WorkCenterErrors.NotFound)
            : Result<WorkCenterCalendarResponse>.Success(ToResponse(workCenter, timeZoneId));
    }

    internal static WorkCenterCalendarResponse ToResponse(
        Domain.Manufacturing.WorkCenter workCenter,
        string timeZoneId) => new(
        workCenter.Id,
        workCenter.Code,
        workCenter.ParallelCapacity,
        timeZoneId,
        workCenter.Shifts
            .OrderBy(shift => shift.DayOfWeek).ThenBy(shift => shift.StartTime)
            .Select(shift => new WorkCenterShiftInput(shift.DayOfWeek, shift.StartTime, shift.EndTime))
            .ToList(),
        workCenter.DaysOff.OrderBy(day => day.Date)
            .Select(day => new WorkCenterDayOffInput(day.Date)).ToList());
}

public sealed class ReplaceWorkCenterCalendarCommandHandler(
    IWorkCenterCalendarRepository repository,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : IRequestHandler<ReplaceWorkCenterCalendarCommand, Result<WorkCenterCalendarResponse>> {
    public async ValueTask<Result<WorkCenterCalendarResponse>> Handle(
        ReplaceWorkCenterCalendarCommand command,
        CancellationToken cancellationToken) {
        if (!WorkCenterCalendarValidation.IsValid(command)) {
            return Result<WorkCenterCalendarResponse>.Failure(WorkCenterErrors.CalendarInvalid);
        }

        var (workCenter, timeZoneId) = await repository.GetAsync(
            command.WorkCenterId, currentUser.CompanyId, cancellationToken);
        if (workCenter is null || timeZoneId is null) {
            return Result<WorkCenterCalendarResponse>.Failure(WorkCenterErrors.NotFound);
        }

        await repository.ReplaceAsync(
            workCenter,
            currentUser.CompanyId,
            command.ParallelCapacity,
            command.Shifts,
            command.DaysOff,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
        var refreshed = await repository.GetAsync(
            command.WorkCenterId, currentUser.CompanyId, cancellationToken);
        return Result<WorkCenterCalendarResponse>.Success(
            GetWorkCenterCalendarQueryHandler.ToResponse(refreshed.WorkCenter!, refreshed.TimeZoneId!));
    }
}

public static class WorkCenterCalendarValidation {
    public static bool IsValid(ReplaceWorkCenterCalendarCommand command) {
        if (command.ParallelCapacity is < WorkCenterConstraints.MinimumParallelCapacity
            or > WorkCenterConstraints.MaximumParallelCapacity
            || command.Shifts.Any(shift => shift.DayOfWeek is < 0 or > 6 || shift.StartTime >= shift.EndTime)
            || command.DaysOff.Select(day => day.Date).Distinct().Count() != command.DaysOff.Count) {
            return false;
        }

        return command.Shifts.GroupBy(shift => shift.DayOfWeek).All(group => {
            var ordered = group.OrderBy(shift => shift.StartTime).ToList();
            return ordered.Zip(ordered.Skip(1)).All(pair => pair.First.EndTime <= pair.Second.StartTime);
        });
    }
}
