using FactoryMind.Application.Features.Auth;
using FactoryMind.Domain.Identity;
using FactoryMind.Domain.Manufacturing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FactoryMind.Infrastructure.Persistence;

public sealed class FactoryMindDatabaseInitializer(
    FactoryMindDbContext dbContext,
    ICredentialHasher credentialHasher,
    IOptions<BootstrapAdminSettings> bootstrapOptions,
    IHostEnvironment environment) {
    public async Task InitializeAsync(CancellationToken cancellationToken = default) {
        await dbContext.Database.MigrateAsync(cancellationToken);

        var company = await dbContext.Companies
            .OrderBy(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var hasUsers = await dbContext.Users.AnyAsync(cancellationToken);
        BootstrapAdminSettings? bootstrap = null;
        if (company is null || !hasUsers) {
            bootstrap = ResolveBootstrapSettings();
        }

        if (company is null) {
            company = new Company { Name = bootstrap!.CompanyName };
            dbContext.Companies.Add(company);
        }

        if (!hasUsers) {
            dbContext.Users.Add(new User {
                Company = company,
                Name = bootstrap!.Name,
                Email = bootstrap.Email.Trim().ToLowerInvariant(),
                PasswordHash = credentialHasher.HashPassword(bootstrap.Password),
                Role = UserRoles.Admin
            });
        }

        if (!environment.IsDevelopment()) {
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
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

    private BootstrapAdminSettings ResolveBootstrapSettings() {
        if (environment.IsDevelopment()) {
            return new BootstrapAdminSettings {
                CompanyName = "FactoryMind Demo",
                Name = "FactoryMind Admin",
                Email = "admin@factorymind.local",
                Password = "Demo@123"
            };
        }

        var settings = bootstrapOptions.Value;
        if (string.IsNullOrWhiteSpace(settings.CompanyName)
            || string.IsNullOrWhiteSpace(settings.Name)
            || string.IsNullOrWhiteSpace(settings.Email)
            || string.IsNullOrWhiteSpace(settings.Password)
            || settings.Password.Length < 12) {
            throw new InvalidOperationException(
                "Production bootstrap Admin settings are missing or unsafe.");
        }

        return settings;
    }
}
