using FactoryMind.Application.Features.WorkCenters;
using FluentValidation;

namespace FactoryMind.Api.Endpoints;

public sealed record WorkCenterCreateRequest(string Code, string Name, string? Description);
public sealed record WorkCenterUpdateRequest(string Code, string Name, string? Description);
public sealed record WorkCenterShiftRequest(int DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);
public sealed record WorkCenterDayOffRequest(DateOnly Date);
public sealed record ReplaceWorkCenterCalendarRequest(
    int ParallelCapacity,
    IReadOnlyList<WorkCenterShiftRequest> Shifts,
    IReadOnlyList<WorkCenterDayOffRequest> DaysOff);

public sealed class WorkCenterCreateRequestValidator : AbstractValidator<WorkCenterCreateRequest> {
    public WorkCenterCreateRequestValidator() {
        RuleFor(request => request.Code).NotEmpty().MaximumLength(WorkCenterConstraints.MaximumCodeLength);
        RuleFor(request => request.Name).NotEmpty().MaximumLength(WorkCenterConstraints.MaximumNameLength);
        RuleFor(request => request.Description).MaximumLength(WorkCenterConstraints.MaximumDescriptionLength);
    }
}

public sealed class WorkCenterUpdateRequestValidator : AbstractValidator<WorkCenterUpdateRequest> {
    public WorkCenterUpdateRequestValidator() {
        RuleFor(request => request.Code).NotEmpty().MaximumLength(WorkCenterConstraints.MaximumCodeLength);
        RuleFor(request => request.Name).NotEmpty().MaximumLength(WorkCenterConstraints.MaximumNameLength);
        RuleFor(request => request.Description).MaximumLength(WorkCenterConstraints.MaximumDescriptionLength);
    }
}

public sealed class ReplaceWorkCenterCalendarRequestValidator
    : AbstractValidator<ReplaceWorkCenterCalendarRequest> {
    public ReplaceWorkCenterCalendarRequestValidator() {
        RuleFor(request => request.ParallelCapacity)
            .InclusiveBetween(WorkCenterConstraints.MinimumParallelCapacity, WorkCenterConstraints.MaximumParallelCapacity);
        RuleFor(request => request.Shifts).NotNull();
        RuleFor(request => request.DaysOff).NotNull();
        RuleForEach(request => request.Shifts).ChildRules(shift => {
            shift.RuleFor(item => item.DayOfWeek).InclusiveBetween(0, 6);
            shift.RuleFor(item => item).Must(item => item.StartTime < item.EndTime)
                .WithMessage("Shift start time must be before end time.");
        });
    }
}
