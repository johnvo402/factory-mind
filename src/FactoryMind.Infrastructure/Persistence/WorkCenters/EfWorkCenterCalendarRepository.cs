using FactoryMind.Application.Features.WorkCenters;
using FactoryMind.Domain.Manufacturing;
using Microsoft.EntityFrameworkCore;

namespace FactoryMind.Infrastructure.Persistence.WorkCenters;

public sealed class EfWorkCenterCalendarRepository(FactoryMindDbContext dbContext)
    : IWorkCenterCalendarRepository {
    public async Task<(WorkCenter? WorkCenter, string? TimeZoneId)> GetAsync(
        Guid workCenterId,
        Guid companyId,
        CancellationToken cancellationToken) {
        var workCenter = await dbContext.WorkCenters
            .AsSplitQuery()
            .Include(item => item.Shifts)
            .Include(item => item.DaysOff)
            .SingleOrDefaultAsync(
                item => item.Id == workCenterId && item.CompanyId == companyId,
                cancellationToken);
        if (workCenter is null) return (null, null);
        var timeZoneId = await dbContext.Companies.AsNoTracking()
            .Where(company => company.Id == companyId)
            .Select(company => company.TimeZoneId)
            .SingleAsync(cancellationToken);
        return (workCenter, timeZoneId);
    }

    public async Task ReplaceAsync(
        WorkCenter workCenter,
        Guid companyId,
        int parallelCapacity,
        IReadOnlyList<WorkCenterShiftInput> shifts,
        IReadOnlyList<WorkCenterDayOffInput> daysOff,
        DateTime updatedAt,
        CancellationToken cancellationToken) {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.WorkCenterShifts.RemoveRange(workCenter.Shifts);
        dbContext.WorkCenterDaysOff.RemoveRange(workCenter.DaysOff);
        workCenter.Shifts.Clear();
        workCenter.DaysOff.Clear();
        workCenter.ParallelCapacity = parallelCapacity;
        workCenter.UpdatedAt = updatedAt;
        var replacementShifts = shifts.Select(shift => new WorkCenterShift {
            CompanyId = companyId,
            WorkCenter = workCenter,
            DayOfWeek = shift.DayOfWeek,
            StartTime = shift.StartTime,
            EndTime = shift.EndTime,
            CreatedAt = updatedAt,
            UpdatedAt = updatedAt
        }).ToList();
        var replacementDaysOff = daysOff.Select(day => new WorkCenterDayOff {
            CompanyId = companyId,
            WorkCenter = workCenter,
            Date = day.Date,
            CreatedAt = updatedAt,
            UpdatedAt = updatedAt
        }).ToList();
        dbContext.WorkCenterShifts.AddRange(replacementShifts);
        dbContext.WorkCenterDaysOff.AddRange(replacementDaysOff);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
