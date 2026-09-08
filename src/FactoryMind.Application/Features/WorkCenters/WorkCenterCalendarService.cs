using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;

namespace FactoryMind.Application.Features.WorkCenters;

public sealed record WorkingInterval(DateTime StartUtc, DateTime EndUtc);

public interface IWorkCenterCalendarService {
    Result<IReadOnlyList<WorkingInterval>> Expand(
        IReadOnlyCollection<WorkCenterShift> shifts,
        IReadOnlyCollection<WorkCenterDayOff> daysOff,
        string timeZoneId,
        DateTime rangeStartUtc,
        DateTime rangeEndUtc);
}

public sealed class WorkCenterCalendarService : IWorkCenterCalendarService {
    public Result<IReadOnlyList<WorkingInterval>> Expand(
        IReadOnlyCollection<WorkCenterShift> shifts,
        IReadOnlyCollection<WorkCenterDayOff> daysOff,
        string timeZoneId,
        DateTime rangeStartUtc,
        DateTime rangeEndUtc) {
        if (rangeEndUtc <= rangeStartUtc || !TryGetTimeZone(timeZoneId, out var timeZone)) {
            return Result<IReadOnlyList<WorkingInterval>>.Failure(PlanningCalendarErrors.InvalidTimeZone);
        }

        var utcStart = DateTime.SpecifyKind(rangeStartUtc, DateTimeKind.Utc);
        var utcEnd = DateTime.SpecifyKind(rangeEndUtc, DateTimeKind.Utc);
        var firstDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcStart, timeZone).Date).AddDays(-1);
        var lastDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcEnd, timeZone).Date).AddDays(1);
        var offDates = daysOff.Select(day => day.Date).ToHashSet();
        var byDay = shifts.GroupBy(shift => shift.DayOfWeek).ToDictionary(
            group => group.Key,
            group => group.OrderBy(shift => shift.StartTime).ToList());
        var result = new List<WorkingInterval>();

        for (var date = firstDate; date <= lastDate; date = date.AddDays(1)) {
            if (offDates.Contains(date) || !byDay.TryGetValue((int)date.DayOfWeek, out var dayShifts)) continue;
            foreach (var shift in dayShifts) {
                var localStart = date.ToDateTime(shift.StartTime, DateTimeKind.Unspecified);
                var localEnd = date.ToDateTime(shift.EndTime, DateTimeKind.Unspecified);
                if (timeZone.IsInvalidTime(localStart) || timeZone.IsInvalidTime(localEnd)) {
                    return Result<IReadOnlyList<WorkingInterval>>.Failure(PlanningCalendarErrors.InvalidLocalTime);
                }

                var start = TimeZoneInfo.ConvertTimeToUtc(localStart, timeZone);
                var end = TimeZoneInfo.ConvertTimeToUtc(localEnd, timeZone);
                if (start < utcEnd && end > utcStart) {
                    result.Add(new WorkingInterval(start < utcStart ? utcStart : start, end > utcEnd ? utcEnd : end));
                }
            }
        }

        var ordered = result.OrderBy(interval => interval.StartUtc).ThenBy(interval => interval.EndUtc).ToList();
        if (ordered.Zip(ordered.Skip(1)).Any(pair => pair.First.EndUtc > pair.Second.StartUtc)) {
            return Result<IReadOnlyList<WorkingInterval>>.Failure(PlanningCalendarErrors.InvalidCalendar);
        }
        return Result<IReadOnlyList<WorkingInterval>>.Success(ordered);
    }

    private static bool TryGetTimeZone(string timeZoneId, out TimeZoneInfo timeZone) {
        try {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        } catch (TimeZoneNotFoundException) {
            timeZone = TimeZoneInfo.Utc;
            return false;
        } catch (InvalidTimeZoneException) {
            timeZone = TimeZoneInfo.Utc;
            return false;
        }
    }
}

public static class PlanningCalendarErrors {
    public static readonly Error InvalidTimeZone = new(
        "planning.timezone_invalid", "The company planning timezone is invalid.", 400);
    public static readonly Error InvalidLocalTime = new(
        "planning.calendar_invalid", "A calendar shift falls in an invalid local time.", 400);
    public static readonly Error InvalidCalendar = new(
        "planning.calendar_invalid", "The work center calendar contains overlapping intervals.", 400);
}
