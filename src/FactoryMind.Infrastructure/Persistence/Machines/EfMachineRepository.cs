using System.Data;
using FactoryMind.Application.Features.Machines;
using FactoryMind.Domain.Manufacturing;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FactoryMind.Infrastructure.Persistence.Machines;

public sealed class EfMachineRepository(FactoryMindDbContext dbContext) : IMachineRepository {
    public async Task<IReadOnlyList<Machine>> GetByCompanyAsync(
        Guid companyId,
        string? search,
        CancellationToken cancellationToken) {
        var query = dbContext.Machines
            .AsNoTracking()
            .Include(machine => machine.WorkCenter)
            .Where(machine => machine.CompanyId == companyId);

        if (search is not null) {
            var pattern = $"%{search}%";
            query = query.Where(machine =>
                EF.Functions.ILike(machine.Code, pattern) ||
                EF.Functions.ILike(machine.Name, pattern));
        }

        return await query
            .OrderBy(machine => machine.Code)
            .ToListAsync(cancellationToken);
    }

    public Task<Machine?> GetByIdAsync(
        Guid machineId,
        Guid companyId,
        CancellationToken cancellationToken) => dbContext.Machines
            .Include(machine => machine.WorkCenter)
            .SingleOrDefaultAsync(
                machine => machine.Id == machineId && machine.CompanyId == companyId,
                cancellationToken);

    public Task<bool> CodeExistsAsync(
        Guid companyId,
        string code,
        Guid? excludedMachineId,
        CancellationToken cancellationToken) => dbContext.Machines.AnyAsync(
            machine => machine.CompanyId == companyId &&
                machine.Code == code &&
                (!excludedMachineId.HasValue || machine.Id != excludedMachineId.Value),
            cancellationToken);

    public Task<bool> HasActiveOperationAsync(
        Guid machineId,
        Guid companyId,
        CancellationToken cancellationToken) => dbContext.ProductionOrderOperations.AnyAsync(
            operation => operation.CompanyId == companyId &&
                operation.MachineId == machineId &&
                operation.Status == ProductionOperationStatuses.InProgress,
            cancellationToken);

    public Task<bool> HasOperationReferenceAsync(
        Guid machineId,
        Guid companyId,
        CancellationToken cancellationToken) => dbContext.ProductionOrderOperations.AnyAsync(
            operation => operation.CompanyId == companyId && operation.MachineId == machineId,
            cancellationToken);

    public async Task<MachineUpdateResult> TryUpdateAsync(
        Guid machineId,
        Guid companyId,
        string code,
        string name,
        string status,
        Guid? workCenterId,
        DateTime updatedAt,
        CancellationToken cancellationToken) {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        var machine = await dbContext.Machines
            .FromSqlInterpolated($"""
                SELECT * FROM machines
                WHERE "Id" = {machineId}
                  AND "CompanyId" = {companyId}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);
        if (machine is null) {
            await transaction.RollbackAsync(cancellationToken);
            return new(MachineUpdateStatus.NotFound, null);
        }
        if (await HasActiveOperationAsync(machineId, companyId, cancellationToken)) {
            await transaction.RollbackAsync(cancellationToken);
            return new(MachineUpdateStatus.ActiveExecution, null);
        }

        WorkCenter? workCenter = null;
        if (workCenterId.HasValue) {
            workCenter = await dbContext.WorkCenters
                .FromSqlInterpolated($"""
                    SELECT * FROM work_centers
                    WHERE "Id" = {workCenterId.Value}
                      AND "CompanyId" = {companyId}
                    FOR SHARE
                    """)
                .SingleOrDefaultAsync(cancellationToken);
            if (workCenter is null) {
                await transaction.RollbackAsync(cancellationToken);
                return new(MachineUpdateStatus.WorkCenterNotFound, null);
            }
            if (!workCenter.IsActive) {
                await transaction.RollbackAsync(cancellationToken);
                return new(MachineUpdateStatus.WorkCenterInactive, null);
            }
        }
        if (await CodeExistsAsync(companyId, code, machineId, cancellationToken)) {
            await transaction.RollbackAsync(cancellationToken);
            return new(MachineUpdateStatus.CodeAlreadyExists, null);
        }

        machine.Code = code;
        machine.Name = name;
        machine.Status = status;
        machine.WorkCenterId = workCenter?.Id;
        machine.WorkCenter = workCenter;
        machine.UpdatedAt = updatedAt;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(MachineUpdateStatus.Success, machine);
    }

    public async Task<bool> TryDeleteAsync(Machine machine, CancellationToken cancellationToken) {
        dbContext.Machines.Remove(machine);
        try {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        } catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException {
                SqlState: PostgresErrorCodes.ForeignKeyViolation
            }) {
            dbContext.Entry(machine).State = EntityState.Unchanged;
            return false;
        }
    }

    public void Add(Machine machine) => dbContext.Machines.Add(machine);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
