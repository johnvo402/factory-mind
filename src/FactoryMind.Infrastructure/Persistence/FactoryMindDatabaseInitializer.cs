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
    private const string DevelopmentDemoPassword = "Demo@123";

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

        await SeedDevelopmentDataAsync(company, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedDevelopmentDataAsync(Company company, CancellationToken cancellationToken) {
        await SeedUsersAsync(company, cancellationToken);
        await SeedMachinesAsync(company, cancellationToken);
        var materials = await SeedMaterialsAsync(company, cancellationToken);
        var products = await SeedProductsAsync(company, cancellationToken);
        var warehouses = await SeedWarehousesAsync(company, cancellationToken);
        await SeedInventoriesAsync(company, materials, warehouses, cancellationToken);
        await SeedProductionOrdersAsync(company, products, cancellationToken);
    }

    private async Task SeedUsersAsync(Company company, CancellationToken cancellationToken) {
        var existingEmails = (await dbContext.Users
                .Where(user => user.CompanyId == company.Id)
                .Select(user => user.Email)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var users = new[] {
            new { Name = "Production Manager", Email = "manager@factorymind.local", Role = UserRoles.Manager },
            new { Name = "Factory Operator", Email = "operator@factorymind.local", Role = UserRoles.User }
        };

        foreach (var user in users.Where(user => !existingEmails.Contains(user.Email))) {
            dbContext.Users.Add(new User {
                Company = company,
                Name = user.Name,
                Email = user.Email,
                PasswordHash = credentialHasher.HashPassword(DevelopmentDemoPassword),
                Role = user.Role
            });
        }
    }

    private async Task SeedMachinesAsync(Company company, CancellationToken cancellationToken) {
        var existingCodes = (await dbContext.Machines
                .Where(machine => machine.CompanyId == company.Id)
                .Select(machine => machine.Code)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var machines = new[] {
            new { Code = "M-001", Name = "Injection Molding HA250", Status = MachineStatuses.Available },
            new { Code = "M-002", Name = "CNC Milling VM850", Status = MachineStatuses.Running },
            new { Code = "M-003", Name = "Automatic Packaging Line PK-10", Status = MachineStatuses.Maintenance },
            new { Code = "M-004", Name = "Air Compressor AC-75", Status = MachineStatuses.Available },
            new { Code = "M-005", Name = "Industrial Chiller CH-20", Status = MachineStatuses.Offline },
            new { Code = "M-006", Name = "Robotic Welding Cell RW-02", Status = MachineStatuses.Running }
        };

        foreach (var machine in machines.Where(machine => !existingCodes.Contains(machine.Code))) {
            dbContext.Machines.Add(new Machine {
                Company = company,
                Code = machine.Code,
                Name = machine.Name,
                Status = machine.Status
            });
        }
    }

    private async Task<IReadOnlyDictionary<string, Material>> SeedMaterialsAsync(
        Company company,
        CancellationToken cancellationToken) {
        var materials = (await dbContext.Materials
                .Where(material => material.CompanyId == company.Id)
                .ToListAsync(cancellationToken))
            .ToDictionary(material => material.Code, StringComparer.OrdinalIgnoreCase);
        var seeds = new[] {
            new { Code = "MAT-PP", Name = "Polypropylene Resin", Unit = "kg" },
            new { Code = "MAT-ABS", Name = "ABS Resin", Unit = "kg" },
            new { Code = "MAT-STEEL304", Name = "Stainless Steel 304", Unit = "kg" },
            new { Code = "MAT-BOX", Name = "Carton Box", Unit = "piece" },
            new { Code = "MAT-LUBE", Name = "Industrial Lubricant", Unit = "liter" }
        };

        foreach (var seed in seeds.Where(seed => !materials.ContainsKey(seed.Code))) {
            var material = new Material {
                Company = company,
                Code = seed.Code,
                Name = seed.Name,
                Unit = seed.Unit
            };
            dbContext.Materials.Add(material);
            materials.Add(material.Code, material);
        }

        return materials;
    }

    private async Task<IReadOnlyDictionary<string, Product>> SeedProductsAsync(
        Company company,
        CancellationToken cancellationToken) {
        var products = (await dbContext.Products
                .Where(product => product.CompanyId == company.Id)
                .ToListAsync(cancellationToken))
            .ToDictionary(product => product.Code, StringComparer.OrdinalIgnoreCase);
        var seeds = new[] {
            new { Code = "PRD-HOUSING", Name = "Motor Housing" },
            new { Code = "PRD-COVER", Name = "Control Panel Cover" },
            new { Code = "PRD-GEAR", Name = "Precision Gear" },
            new { Code = "PRD-KIT", Name = "Assembly Kit" }
        };

        foreach (var seed in seeds.Where(seed => !products.ContainsKey(seed.Code))) {
            var product = new Product {
                Company = company,
                Code = seed.Code,
                Name = seed.Name
            };
            dbContext.Products.Add(product);
            products.Add(product.Code, product);
        }

        return products;
    }

    private async Task SeedInventoriesAsync(
        Company company,
        IReadOnlyDictionary<string, Material> materials,
        IReadOnlyDictionary<string, Warehouse> warehouses,
        CancellationToken cancellationToken) {
        if (await dbContext.InventoryBalances.AnyAsync(
            balance => balance.CompanyId == company.Id, cancellationToken)) {
            return;
        }

        var seeds = new[] {
            new { MaterialCode = "MAT-PP", WarehouseCode = "WH-RAW", Quantity = 1200m },
            new { MaterialCode = "MAT-PP", WarehouseCode = "WH-WIP", Quantity = 400m },
            new { MaterialCode = "MAT-ABS", WarehouseCode = "WH-RAW", Quantity = 650m },
            new { MaterialCode = "MAT-STEEL304", WarehouseCode = "WH-RAW", Quantity = 2400m },
            new { MaterialCode = "MAT-BOX", WarehouseCode = "WH-FG", Quantity = 5000m },
            new { MaterialCode = "MAT-LUBE", WarehouseCode = "WH-WIP", Quantity = 180m }
        };

        foreach (var seed in seeds) {
            var material = materials[seed.MaterialCode];
            var warehouse = warehouses[seed.WarehouseCode];
            var now = DateTime.UtcNow;
            dbContext.InventoryBalances.Add(new InventoryBalance {
                Company = company,
                Warehouse = warehouse,
                Material = material,
                Quantity = seed.Quantity,
                UpdatedAt = now
            });
            dbContext.InventoryTransactions.Add(new InventoryTransaction {
                Company = company,
                Warehouse = warehouse,
                Material = material,
                Type = InventoryTransactionType.Receipt,
                Quantity = seed.Quantity,
                ReferenceType = "DevelopmentSeed",
                Note = "Opening demo stock.",
                CreatedAt = now
            });
        }
    }

    private async Task<IReadOnlyDictionary<string, Warehouse>> SeedWarehousesAsync(
        Company company,
        CancellationToken cancellationToken) {
        var warehouses = (await dbContext.Warehouses
                .Where(warehouse => warehouse.CompanyId == company.Id)
                .ToListAsync(cancellationToken))
            .ToDictionary(warehouse => warehouse.Code, StringComparer.OrdinalIgnoreCase);
        var seeds = new[] {
            new { Code = "WH-RAW", Name = "Raw Materials", Description = "Raw material receiving and storage." },
            new { Code = "WH-FG", Name = "Finished Goods", Description = "Completed goods awaiting dispatch." },
            new { Code = "WH-WIP", Name = "Work In Progress", Description = "Materials currently staged for production." }
        };
        foreach (var seed in seeds.Where(seed => !warehouses.ContainsKey(seed.Code))) {
            var warehouse = new Warehouse {
                Company = company,
                Code = seed.Code,
                Name = seed.Name,
                Description = seed.Description,
                IsActive = true
            };
            dbContext.Warehouses.Add(warehouse);
            warehouses.Add(warehouse.Code, warehouse);
        }
        return warehouses;
    }

    private async Task SeedProductionOrdersAsync(
        Company company,
        IReadOnlyDictionary<string, Product> products,
        CancellationToken cancellationToken) {
        var existingNumbers = (await dbContext.ProductionOrders
                .Where(order => order.CompanyId == company.Id)
                .Select(order => order.Number)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seeds = new[] {
            new { Number = "PO-2026-001", ProductCode = "PRD-HOUSING", Quantity = 500m, Status = ProductionOrderStatuses.InProgress },
            new { Number = "PO-2026-002", ProductCode = "PRD-COVER", Quantity = 300m, Status = ProductionOrderStatuses.Planned },
            new { Number = "PO-2026-003", ProductCode = "PRD-GEAR", Quantity = 1200m, Status = ProductionOrderStatuses.Completed },
            new { Number = "PO-2026-004", ProductCode = "PRD-KIT", Quantity = 250m, Status = ProductionOrderStatuses.Planned },
            new { Number = "PO-2026-005", ProductCode = "PRD-HOUSING", Quantity = 100m, Status = ProductionOrderStatuses.Cancelled }
        };

        foreach (var seed in seeds.Where(seed => !existingNumbers.Contains(seed.Number))) {
            dbContext.ProductionOrders.Add(new ProductionOrder {
                Company = company,
                Number = seed.Number,
                Product = products[seed.ProductCode],
                Quantity = seed.Quantity,
                Status = seed.Status
            });
        }
    }

    private BootstrapAdminSettings ResolveBootstrapSettings() {
        if (environment.IsDevelopment()) {
            return new BootstrapAdminSettings {
                CompanyName = "FactoryMind Demo",
                Name = "FactoryMind Admin",
                Email = "admin@factorymind.local",
                Password = DevelopmentDemoPassword
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
