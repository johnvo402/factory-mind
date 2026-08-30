using FactoryMind.Domain.Manufacturing;
using FactoryMind.Infrastructure.Persistence;
using FactoryMind.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryMind.IntegrationTests.Manufacturing;

[Collection(IntegrationTestCollection.Name)]
public sealed class ProductionExecutionMigrationIntegrationTests(PostgreSqlFixture fixture)
    : IntegrationTestBase(fixture) {
    private const string BomMigration = "20260828142309_AddVersionedBomAndMaterialRequirements";
    private const string ExecutionMigration = "20260830071334_AddProductionExecutionLifecycle";

    [Fact]
    public async Task Migration_preserves_legacy_order_and_inventory_then_accepts_six_decimal_stock() {
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        var migrator = dbContext.Database.GetService<IMigrator>();
        try {
            await migrator.MigrateAsync(BomMigration);
            await dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE companies CASCADE;");

            var companyId = Guid.Parse("40000000-0000-0000-0000-000000000001");
            var productId = Guid.Parse("40000000-0000-0000-0000-000000000101");
            var materialId = Guid.Parse("40000000-0000-0000-0000-000000000201");
            var warehouseId = Guid.Parse("40000000-0000-0000-0000-000000000301");
            var orderId = Guid.Parse("40000000-0000-0000-0000-000000000401");
            var balanceId = Guid.Parse("40000000-0000-0000-0000-000000000501");
            var transactionId = Guid.Parse("40000000-0000-0000-0000-000000000601");
            var createdAt = new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc);
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO companies ("Id", "Name", "CreatedAt")
                VALUES ({companyId}, {"Migration Company"}, {createdAt});
                INSERT INTO products ("Id", "CompanyId", "Code", "Name", "CreatedAt", "UpdatedAt")
                VALUES ({productId}, {companyId}, {"P-MIGRATION"}, {"Migration Product"}, {createdAt}, {createdAt});
                INSERT INTO materials ("Id", "CompanyId", "Code", "Name", "Unit", "CreatedAt", "UpdatedAt")
                VALUES ({materialId}, {companyId}, {"MAT-MIGRATION"}, {"Migration Material"}, {"kg"}, {createdAt}, {createdAt});
                INSERT INTO warehouses
                    ("Id", "CompanyId", "Code", "Name", "Description", "IsActive", "CreatedAt", "UpdatedAt")
                VALUES
                    ({warehouseId}, {companyId}, {"WH-MIGRATION"}, {"Migration Warehouse"},
                     {null}, {true}, {createdAt}, {createdAt});
                INSERT INTO production_orders
                    ("Id", "CompanyId", "Number", "ProductId", "Quantity", "Status", "CreatedAt", "UpdatedAt")
                VALUES
                    ({orderId}, {companyId}, {"PO-LEGACY"}, {productId}, {3.125m},
                     {ProductionOrderStatuses.Completed}, {createdAt}, {createdAt});
                INSERT INTO inventory_balances
                    ("Id", "CompanyId", "WarehouseId", "MaterialId", "Quantity", "UpdatedAt")
                VALUES ({balanceId}, {companyId}, {warehouseId}, {materialId}, {42.125m}, {createdAt});
                INSERT INTO inventory_transactions
                    ("Id", "CompanyId", "WarehouseId", "MaterialId", "Type", "Quantity",
                     "ReferenceType", "ReferenceId", "Note", "CreatedByUserId", "CreatedAt")
                VALUES
                    ({transactionId}, {companyId}, {warehouseId}, {materialId}, {"Receipt"}, {42.125m},
                     {"MigrationRegression"}, {orderId}, {null}, {null}, {createdAt});
                """);

            await migrator.MigrateAsync(ExecutionMigration);
            dbContext.ChangeTracker.Clear();

            var order = await dbContext.ProductionOrders.SingleAsync();
            Assert.Equal(ProductionOrderStatuses.Completed, order.Status);
            Assert.Null(order.BillOfMaterialId);
            Assert.Null(order.ReleasedAt);
            Assert.Null(order.StartedAt);
            Assert.Null(order.CancelledAt);
            Assert.Equal(42.125m, (await dbContext.InventoryBalances.SingleAsync()).Quantity);
            Assert.Equal(42.125m, (await dbContext.InventoryTransactions.SingleAsync()).Quantity);

            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE inventory_balances SET "Quantity" = {42.123456m} WHERE "Id" = {balanceId};
                UPDATE inventory_transactions SET "Quantity" = {42.123456m} WHERE "Id" = {transactionId};
                """);
            dbContext.ChangeTracker.Clear();
            Assert.Equal(42.123456m, (await dbContext.InventoryBalances.SingleAsync()).Quantity);
            Assert.Equal(42.123456m, (await dbContext.InventoryTransactions.SingleAsync()).Quantity);
        } finally {
            await migrator.MigrateAsync();
        }
    }
}
