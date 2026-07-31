using FactoryMind.Application.Features.Settings;
using FactoryMind.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace FactoryMind.Infrastructure.Persistence.Settings;

public sealed class EfSettingsRepository(
    FactoryMindDbContext dbContext) : ISettingsRepository {
    public Task<Company?> GetCompanyAsync(Guid companyId, CancellationToken cancellationToken) =>
        dbContext.Companies.SingleOrDefaultAsync(company => company.Id == companyId, cancellationToken);

    public async Task<IReadOnlyList<User>> GetUsersAsync(
        Guid companyId,
        CancellationToken cancellationToken) => await dbContext.Users
            .AsNoTracking()
            .Where(user => user.CompanyId == companyId)
            .OrderBy(user => user.Name)
            .ThenBy(user => user.Email)
            .ToListAsync(cancellationToken);

    public Task<User?> GetUserAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken) => dbContext.Users.SingleOrDefaultAsync(
            user => user.Id == userId && user.CompanyId == companyId,
            cancellationToken);

    public Task<bool> EmailExistsAsync(
        Guid companyId,
        string email,
        Guid? excludedUserId,
        CancellationToken cancellationToken) => dbContext.Users.AnyAsync(
            user => user.CompanyId == companyId
                && user.Email == email
                && (!excludedUserId.HasValue || user.Id != excludedUserId.Value),
            cancellationToken);

    public void Add(User user) => dbContext.Users.Add(user);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
