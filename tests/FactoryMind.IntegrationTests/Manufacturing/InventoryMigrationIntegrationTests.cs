using FactoryMind.Domain.Manufacturing;
using FactoryMind.Infrastructure.Persistence;
using FactoryMind.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryMind.IntegrationTests.Manufacturing;

[Collection(IntegrationTestCollection.Name)]
public sealed class InventoryMigrationIntegrationTests(PostgreSqlFixture fixture)
    : IntegrationTestBase(fixture) {
    private const string PreviousMigration = "20260731195935_AddChatBusinessEvidence";
    private const string LedgerMigration = "20260828074732_IntroduceWarehouseInventoryLedger";

    [Fact]
    public async Task Migration_preserves_legacy_inventory_as_balance_and_opening_transaction() {
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        var migrator = dbContext.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);
        await dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE companies CASCADE;");

        var companyId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        var materialId = Guid.Parse("30000000-0000-0000-0000-000000000101");
        var inventoryId = Guid.Parse("30000000-0000-0000-0000-000000000201");
        var createdAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        const string companyName = "Legacy Factory";
        const string materialCode = "MAT-LEGACY";
        const string materialName = "Legacy Steel";
        const string unit = "kg";
        const string warehouseName = "Main Warehouse";
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO companies ("Id", "Name", "CreatedAt")
            VALUES ({companyId}, {companyName}, {createdAt});
            INSERT INTO materials ("Id", "CompanyId", "Code", "Name", "Unit", "CreatedAt", "UpdatedAt")
            VALUES ({materialId}, {companyId}, {materialCode}, {materialName}, {unit}, {createdAt}, {createdAt});
            INSERT INTO inventories
                ("Id", "CompanyId", "MaterialId", "Warehouse", "Quantity", "CreatedAt", "UpdatedAt")
            VALUES
                ({inventoryId}, {companyId}, {materialId}, {warehouseName}, {42.5m}, {createdAt}, {createdAt});
            """);

        await migrator.MigrateAsync(LedgerMigration);
        dbContext.ChangeTracker.Clear();

        var warehouse = await dbContext.Warehouses.SingleAsync();
        Assert.Equal("WH-LEGACY-001", warehouse.Code);
        Assert.Equal("Main Warehouse", warehouse.Name);
        var balance = await dbContext.InventoryBalances.SingleAsync();
        Assert.Equal(inventoryId, balance.Id);
        Assert.Equal(42.5m, balance.Quantity);
        var transaction = await dbContext.InventoryTransactions.SingleAsync();
        Assert.Equal(InventoryTransactionType.AdjustmentIncrease, transaction.Type);
        Assert.Equal(42.5m, transaction.SignedQuantity());
        Assert.Equal(inventoryId, transaction.ReferenceId);
    }
}
