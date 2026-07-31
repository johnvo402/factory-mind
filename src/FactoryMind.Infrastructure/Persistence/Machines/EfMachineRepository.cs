using FactoryMind.Application.Features.Machines;
using FactoryMind.Domain.Manufacturing;
using Microsoft.EntityFrameworkCore;

namespace FactoryMind.Infrastructure.Persistence.Machines;

public sealed class EfMachineRepository(FactoryMindDbContext dbContext) : IMachineRepository {
    public async Task<IReadOnlyList<Machine>> GetByCompanyAsync(
        Guid companyId,
        string? search,
        CancellationToken cancellationToken) {
        var query = dbContext.Machines
            .AsNoTracking()
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
        CancellationToken cancellationToken) => dbContext.Machines.SingleOrDefaultAsync(
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

    public void Add(Machine machine) => dbContext.Machines.Add(machine);

    public void Remove(Machine machine) => dbContext.Machines.Remove(machine);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
