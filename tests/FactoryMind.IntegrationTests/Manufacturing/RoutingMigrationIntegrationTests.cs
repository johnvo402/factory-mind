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
public sealed class RoutingMigrationIntegrationTests(PostgreSqlFixture fixture)
    : IntegrationTestBase(fixture) {
    private const string PreviousMigration =
        "20260830081645_AddFinishedGoodsInventoryAndProductionCompletion";
    private const string RoutingMigration =
        "20260905151959_AddRoutingWorkCentersAndProductionOperations";

    [Fact]
    public async Task Fresh_database_has_routing_schema_indexes_and_restrict_foreign_keys() {
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        var migrations = await dbContext.Database.GetAppliedMigrationsAsync();
        Assert.Contains(RoutingMigration, migrations);
        foreach (var table in new[] {
                     "work_centers", "routings", "routing_operations", "production_order_operations"
                 }) {
            Assert.Equal(table, await ScalarAsync(dbContext, $"SELECT to_regclass('public.{table}')::text;"));
        }
        Assert.Equal("YES", await ScalarAsync(dbContext, """
            SELECT is_nullable FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'production_orders' AND column_name = 'RoutingId';
            """));
        Assert.Contains("UNIQUE", await ScalarAsync(dbContext, """
            SELECT indexdef FROM pg_indexes
            WHERE indexname = 'IX_work_centers_CompanyId_Code';
            """), StringComparison.Ordinal);
        var activeRoutingIndex = await ScalarAsync(dbContext, """
            SELECT indexdef FROM pg_indexes
            WHERE indexname = 'IX_routings_CompanyId_ProductId';
            """);
        Assert.Contains("Status", activeRoutingIndex, StringComparison.Ordinal);
        Assert.Contains("active", activeRoutingIndex, StringComparison.Ordinal);
        Assert.Equal("r", await ForeignKeyDeleteActionAsync(
            dbContext, "production_order_operations", "production_orders"));
        Assert.Equal("r", await ForeignKeyDeleteActionAsync(
            dbContext, "production_order_operations", "work_centers"));
        Assert.Equal("r", await ForeignKeyDeleteActionAsync(
            dbContext, "production_orders", "routings"));
    }

    [Fact]
    public async Task Migration_keeps_legacy_terminal_and_execution_orders_without_fabricated_routing() {
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        var migrator = dbContext.Database.GetService<IMigrator>();
        try {
            await migrator.MigrateAsync(PreviousMigration);
            await dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE companies CASCADE;");
            var companyId = Guid.Parse("60000000-0000-0000-0000-000000000001");
            var productId = Guid.Parse("60000000-0000-0000-0000-000000000101");
            var createdAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO companies ("Id", "Name", "CreatedAt")
                VALUES ({companyId}, {"Legacy Routing Company"}, {createdAt});
                INSERT INTO products ("Id", "CompanyId", "Code", "Name", "CreatedAt", "UpdatedAt")
                VALUES ({productId}, {companyId}, {"P-LEGACY-ROUTE"}, {"Legacy Product"}, {createdAt}, {createdAt});
                """);
            foreach (var status in new[] {
                         ProductionOrderStatuses.Released,
                         ProductionOrderStatuses.InProgress,
                         ProductionOrderStatuses.Completed,
                         ProductionOrderStatuses.Cancelled
                     }) {
                await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO production_orders
                        ("Id", "CompanyId", "Number", "ProductId", "Quantity", "Status", "CreatedAt", "UpdatedAt")
                    VALUES
                        ({Guid.NewGuid()}, {companyId}, {$"PO-{status}"}, {productId}, {1m}, {status}, {createdAt}, {createdAt});
                    """);
            }

            await migrator.MigrateAsync(RoutingMigration);
            dbContext.ChangeTracker.Clear();
            var orders = await dbContext.ProductionOrders.OrderBy(order => order.Number).ToListAsync();
            Assert.Equal(4, orders.Count);
            Assert.All(orders, order => Assert.Null(order.RoutingId));
            Assert.Empty(await dbContext.Routings.ToListAsync());
            Assert.Empty(await dbContext.ProductionOrderOperations.ToListAsync());
        } finally {
            await migrator.MigrateAsync();
        }
    }

    private static Task<string> ForeignKeyDeleteActionAsync(
        FactoryMindDbContext dbContext,
        string dependentTable,
        string principalTable) => ScalarAsync(dbContext, $"""
            SELECT confdeltype::text FROM pg_constraint
            WHERE conrelid = '{dependentTable}'::regclass
              AND confrelid = '{principalTable}'::regclass;
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
