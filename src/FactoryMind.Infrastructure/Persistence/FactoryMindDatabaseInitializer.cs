using FactoryMind.Application.Features.Auth;
using FactoryMind.Domain.Identity;
using FactoryMind.Domain.Manufacturing;
using Microsoft.EntityFrameworkCore;

namespace FactoryMind.Infrastructure.Persistence;

public sealed class FactoryMindDatabaseInitializer(FactoryMindDbContext dbContext, ICredentialHasher credentialHasher) {
    public async Task InitializeAsync(CancellationToken cancellationToken = default) {
        await dbContext.Database.MigrateAsync(cancellationToken);

        var company = await dbContext.Companies
            .OrderBy(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (company is null) {
            company = new Company { Name = "FactoryMind Demo" };
            dbContext.Companies.Add(company);
        }

        if (!await dbContext.Users.AnyAsync(cancellationToken)) {
            dbContext.Users.Add(new User {
                Company = company,
                Name = "FactoryMind Admin",
                Email = "admin@factorymind.local",
                PasswordHash = credentialHasher.HashPassword("Demo@123"),
                Role = UserRoles.Admin
            });
        }

        if (!await dbContext.Machines.AnyAsync(
                machine => machine.CompanyId == company.Id,
                cancellationToken)) {
            dbContext.Machines.Add(new Machine {
                Company = company,
                Code = "M-001",
                Name = "Injection Molding HA250",
                Status = MachineStatuses.Available
            });
        }

        var demoMaterial = await dbContext.Materials.FirstOrDefaultAsync(
            material => material.CompanyId == company.Id,
            cancellationToken);
        if (demoMaterial is null) {
            demoMaterial = new Material {
                Company = company,
                Code = "MAT-PP",
                Name = "Polypropylene Resin",
                Unit = "kg"
            };
            dbContext.Materials.Add(demoMaterial);
        }

        if (!await dbContext.Inventories.AnyAsync(
                inventory => inventory.CompanyId == company.Id,
                cancellationToken)) {
            dbContext.Inventories.Add(new Inventory {
                Company = company,
                Material = demoMaterial,
                Warehouse = "Main Warehouse",
                Quantity = 1200m
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
