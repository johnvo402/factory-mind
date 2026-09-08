using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;

namespace FactoryMind.Application.Features.WorkCenters;

public static class WorkCenterConstraints {
    public const int MaximumCodeLength = 50;
    public const int MaximumNameLength = 200;
    public const int MaximumDescriptionLength = 500;
    public const int MaximumSearchLength = 200;
    public const int MinimumParallelCapacity = 1;
    public const int MaximumParallelCapacity = 100;
}

public sealed record WorkCenterResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    int ParallelCapacity,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt) {
    public static WorkCenterResponse From(WorkCenter workCenter) => new(
        workCenter.Id,
        workCenter.Code,
        workCenter.Name,
        workCenter.Description,
        workCenter.ParallelCapacity,
        workCenter.IsActive,
        workCenter.CreatedAt,
        workCenter.UpdatedAt);
}

public interface IWorkCenterRepository {
    Task<IReadOnlyList<WorkCenter>> GetByCompanyAsync(
        Guid companyId,
        string? search,
        CancellationToken cancellationToken);
    Task<WorkCenter?> GetByIdAsync(Guid id, Guid companyId, CancellationToken cancellationToken);
    Task<bool> CodeExistsAsync(
        Guid companyId,
        string code,
        Guid? excludedId,
        CancellationToken cancellationToken);
    void Add(WorkCenter workCenter);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record WorkCenterShiftInput(int DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);
public sealed record WorkCenterDayOffInput(DateOnly Date);

public sealed record WorkCenterCalendarResponse(
    Guid WorkCenterId,
    string WorkCenterCode,
    int ParallelCapacity,
    string TimeZoneId,
    IReadOnlyList<WorkCenterShiftInput> Shifts,
    IReadOnlyList<WorkCenterDayOffInput> DaysOff);

public interface IWorkCenterCalendarRepository {
    Task<(WorkCenter? WorkCenter, string? TimeZoneId)> GetAsync(
        Guid workCenterId, Guid companyId, CancellationToken cancellationToken);
    Task ReplaceAsync(
        WorkCenter workCenter,
        Guid companyId,
        int parallelCapacity,
        IReadOnlyList<WorkCenterShiftInput> shifts,
        IReadOnlyList<WorkCenterDayOffInput> daysOff,
        DateTime updatedAt,
        CancellationToken cancellationToken);
}

public static class WorkCenterErrors {
    public static readonly Error NotFound = new(
        "work_centers.not_found", "Work center was not found.", 404);
    public static readonly Error CodeAlreadyExists = new(
        "work_centers.code_already_exists", "A work center with this code already exists.", 409);
    public static readonly Error CalendarInvalid = new(
        "planning.calendar_invalid", "The work center calendar is invalid.", 400);
}
