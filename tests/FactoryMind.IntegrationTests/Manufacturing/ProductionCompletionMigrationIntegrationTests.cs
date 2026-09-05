using System.Data.Common;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Infrastructure.Persistence;
using FactoryMind.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryMind.IntegrationTests.Manufacturing;

[Collection(IntegrationTestCollection.Name)]
public sealed class ProductionCompletionMigrationIntegrationTests(PostgreSqlFixture fixture)
    : IntegrationTestBase(fixture) {
    private const string ExecutionMigration = "20260830071334_AddProductionExecutionLifecycle";
    private const string FinishedGoodsMigration =
        "20260830081645_AddFinishedGoodsInventoryAndProductionCompletion";

    [Fact]
    public async Task Fresh_database_has_finished_goods_schema_constraints_indexes_and_restrict_foreign_keys() {
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();

        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
        Assert.Contains(FinishedGoodsMigration, appliedMigrations);
        Assert.Equal("timestamp with time zone", await ScalarAsync(
            dbContext,
            """
            SELECT data_type
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'production_orders'
              AND column_name = 'CompletedAt';
            """));
        Assert.Equal("product_inventory_balances", await ScalarAsync(
            dbContext,
            "SELECT to_regclass('public.product_inventory_balances')::text;"));
        Assert.Equal("product_inventory_transactions", await ScalarAsync(
            dbContext,
            "SELECT to_regclass('public.product_inventory_transactions')::text;"));
        Assert.Equal("CHECK ((\"Quantity\" >= (0)::numeric))", await ScalarAsync(
            dbContext,
            """
            SELECT pg_get_constraintdef(oid)
            FROM pg_constraint
            WHERE conname = 'CK_product_inventory_balances_Quantity_nonnegative';
            """));
        Assert.Equal("CHECK ((\"Quantity\" > (0)::numeric))", await ScalarAsync(
            dbContext,
            """
            SELECT pg_get_constraintdef(oid)
            FROM pg_constraint
            WHERE conname = 'CK_product_inventory_transactions_Quantity_positive';
            """));
        var balanceIndex = await ScalarAsync(
            dbContext,
            """
            SELECT indexdef
            FROM pg_indexes
            WHERE schemaname = 'public'
              AND indexname = 'IX_product_inventory_balances_CompanyId_WarehouseId_ProductId';
            """);
        Assert.Contains("UNIQUE", balanceIndex, StringComparison.Ordinal);
        Assert.Contains("\"CompanyId\", \"WarehouseId\", \"ProductId\"", balanceIndex, StringComparison.Ordinal);
        Assert.Equal("r", await ForeignKeyDeleteActionAsync(
            dbContext,
            "FK_product_inventory_transactions_products_ProductId"));
        Assert.Equal("r", await ForeignKeyDeleteActionAsync(
            dbContext,
            "FK_product_inventory_transactions_warehouses_WarehouseId"));
    }

    [Fact]
    public async Task Migration_preserves_legacy_completed_order_without_fabricating_finished_goods() {
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        var migrator = dbContext.Database.GetService<IMigrator>();
        try {
            await migrator.MigrateAsync(ExecutionMigration);
            await dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE companies CASCADE;");
            var companyId = Guid.Parse("50000000-0000-0000-0000-000000000001");
            var productId = Guid.Parse("50000000-0000-0000-0000-000000000101");
            var warehouseId = Guid.Parse("50000000-0000-0000-0000-000000000201");
            var orderId = Guid.Parse("50000000-0000-0000-0000-000000000301");
            var createdAt = new DateTime(2026, 8, 30, 7, 30, 0, DateTimeKind.Utc);
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO companies ("Id", "Name", "CreatedAt")
                VALUES ({companyId}, {"Legacy Finished Goods Company"}, {createdAt});
                INSERT INTO products ("Id", "CompanyId", "Code", "Name", "CreatedAt", "UpdatedAt")
                VALUES ({productId}, {companyId}, {"P-LEGACY-FG"}, {"Legacy Product"}, {createdAt}, {createdAt});
                INSERT INTO warehouses
                    ("Id", "CompanyId", "Code", "Name", "Description", "IsActive", "CreatedAt", "UpdatedAt")
                VALUES
                    ({warehouseId}, {companyId}, {"WH-LEGACY-FG"}, {"Legacy Warehouse"},
                     {null}, {true}, {createdAt}, {createdAt});
                INSERT INTO production_orders
                    ("Id", "CompanyId", "Number", "ProductId", "Quantity", "Status", "CreatedAt", "UpdatedAt")
                VALUES
                    ({orderId}, {companyId}, {"PO-LEGACY-COMPLETE"}, {productId}, {15m},
                     {ProductionOrderStatuses.Completed}, {createdAt}, {createdAt});
                """);

            await migrator.MigrateAsync(FinishedGoodsMigration);
            await migrator.MigrateAsync();
            dbContext.ChangeTracker.Clear();

            var order = await dbContext.ProductionOrders.SingleAsync(candidate => candidate.Id == orderId);
            Assert.Equal(ProductionOrderStatuses.Completed, order.Status);
            Assert.Null(order.CompletedAt);
            Assert.Empty(await dbContext.ProductInventoryBalances.ToListAsync());
            Assert.Empty(await dbContext.ProductInventoryTransactions.ToListAsync());
            Assert.Contains(FinishedGoodsMigration, await dbContext.Database.GetAppliedMigrationsAsync());
        } finally {
            await migrator.MigrateAsync();
        }
    }

    private static Task<string> ForeignKeyDeleteActionAsync(
        FactoryMindDbContext dbContext,
        string constraintName) => ScalarAsync(
        dbContext,
        $"""
        SELECT confdeltype::text
        FROM pg_constraint
        WHERE conname = '{constraintName}';
        """);

    private static async Task<string> ScalarAsync(FactoryMindDbContext dbContext, string sql) {
        await dbContext.Database.OpenConnectionAsync();
        await using DbCommand command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)
            ?? throw new InvalidOperationException("Schema query returned null.");
    }
}
