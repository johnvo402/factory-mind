using FactoryMind.Application.Features.WorkCenters;

namespace FactoryMind.Tests;

public sealed class WorkCenterCalendarValidationTests {
    [Fact]
    public void Accepts_multiple_non_overlapping_shifts() {
        var command = Command([
            new WorkCenterShiftInput(1, new TimeOnly(8, 0), new TimeOnly(12, 0)),
            new WorkCenterShiftInput(1, new TimeOnly(13, 0), new TimeOnly(17, 0))
        ]);
        Assert.True(WorkCenterCalendarValidation.IsValid(command));
    }

    [Fact]
    public void Rejects_overlap_invalid_capacity_and_duplicate_day_off() {
        Assert.False(WorkCenterCalendarValidation.IsValid(Command([
            new WorkCenterShiftInput(1, new TimeOnly(8, 0), new TimeOnly(12, 0)),
            new WorkCenterShiftInput(1, new TimeOnly(11, 0), new TimeOnly(17, 0))
        ])));
        Assert.False(WorkCenterCalendarValidation.IsValid(Command([], 0)));
        Assert.False(WorkCenterCalendarValidation.IsValid(new ReplaceWorkCenterCalendarCommand(
            Guid.NewGuid(), 1, [], [
                new WorkCenterDayOffInput(new DateOnly(2026, 9, 8)),
                new WorkCenterDayOffInput(new DateOnly(2026, 9, 8))
            ])));
    }

    private static ReplaceWorkCenterCalendarCommand Command(
        IReadOnlyList<WorkCenterShiftInput> shifts,
        int capacity = 1) => new(Guid.NewGuid(), capacity, shifts, []);
}
