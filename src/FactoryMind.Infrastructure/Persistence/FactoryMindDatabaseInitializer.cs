using FactoryMind.Application.Features.Auth;
using FactoryMind.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace FactoryMind.Infrastructure.Persistence;

public sealed class FactoryMindDatabaseInitializer(FactoryMindDbContext dbContext, ICredentialHasher credentialHasher)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);

        if (await dbContext.Users.AnyAsync(cancellationToken)) return;

        var company = new Company { Name = "FactoryMind Demo" };
        dbContext.Companies.Add(company);
        dbContext.Users.Add(new User
        {
            Company = company,
            Name = "FactoryMind Admin",
            Email = "admin@factorymind.local",
            PasswordHash = credentialHasher.HashPassword("Demo@123"),
            Role = "Admin"
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
