using FactoryMind.Application.Features.ProductionOrders;
using FactoryMind.Application.Features.WorkCenters;
using FactoryMind.Domain.Manufacturing;

namespace FactoryMind.Tests;

public sealed class WorkCenterCalendarServiceTests {
    private readonly WorkCenterCalendarService service = new();

    [Fact]
    public void Expands_monday_shift_in_utc() {
        var result = service.Expand(
            [Shift(1, 8, 12)], [], "UTC",
            Utc(2026, 9, 7, 0), Utc(2026, 9, 8, 0));

        Assert.True(result.IsSuccess);
        var interval = Assert.Single(result.Value!);
        Assert.Equal(Utc(2026, 9, 7, 8), interval.StartUtc);
        Assert.Equal(Utc(2026, 9, 7, 12), interval.EndUtc);
    }

    [Fact]
    public void Expands_multiple_shifts_and_day_off_overrides_all() {
        var shifts = new[] { Shift(1, 8, 12), Shift(1, 13, 17) };
        var normal = service.Expand(shifts, [], "UTC", Utc(2026, 9, 7, 0), Utc(2026, 9, 8, 0));
        var off = service.Expand(shifts, [DayOff(2026, 9, 7)], "UTC", Utc(2026, 9, 7, 0), Utc(2026, 9, 8, 0));

        Assert.Equal(2, normal.Value!.Count);
        Assert.Empty(off.Value!);
    }

    [Fact]
    public void Converts_asia_ho_chi_minh_local_shift_to_utc() {
        var result = service.Expand(
            [Shift(1, 8, 17)], [], "Asia/Ho_Chi_Minh",
            Utc(2026, 9, 7, 0), Utc(2026, 9, 8, 0));

        var interval = Assert.Single(result.Value!);
        Assert.Equal(Utc(2026, 9, 7, 1), interval.StartUtc);
        Assert.Equal(Utc(2026, 9, 7, 10), interval.EndUtc);
    }

    [Fact]
    public void Clips_intervals_to_range_boundaries_and_returns_no_calendar() {
        var clipped = service.Expand(
            [Shift(1, 8, 17)], [], "UTC",
            Utc(2026, 9, 7, 10), Utc(2026, 9, 7, 12));
        var empty = service.Expand([], [], "UTC", Utc(2026, 9, 7, 0), Utc(2026, 9, 8, 0));

        Assert.Equal(Utc(2026, 9, 7, 10), Assert.Single(clipped.Value!).StartUtc);
        Assert.Empty(empty.Value!);
    }

    [Fact]
    public void Rejects_unknown_timezone() {
        var result = service.Expand([], [], "Mars/Olympus", Utc(2026, 9, 7, 0), Utc(2026, 9, 8, 0));
        Assert.Equal("planning.timezone_invalid", result.Error?.Code);
    }

    private static WorkCenterShift Shift(int day, int start, int end) => new() {
        DayOfWeek = day,
        StartTime = new TimeOnly(start, 0),
        EndTime = new TimeOnly(end, 0)
    };

    private static WorkCenterDayOff DayOff(int year, int month, int day) => new() {
        Date = new DateOnly(year, month, day)
    };

    private static DateTime Utc(int year, int month, int day, int hour) =>
        new(year, month, day, hour, 0, 0, DateTimeKind.Utc);
}
