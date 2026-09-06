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
public sealed class MachineAssignmentMigrationIntegrationTests(PostgreSqlFixture fixture)
    : IntegrationTestBase(fixture) {
    private const string PreviousMigration =
        "20260905151959_AddRoutingWorkCentersAndProductionOperations";
    private const string MachineAssignmentMigration =
        "20260906065401_AddMachineWorkCenterAndOperationAssignment";

    [Fact]
    public async Task Fresh_database_has_machine_assignment_schema_constraints_and_restrict_foreign_keys() {
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        Assert.Contains(MachineAssignmentMigration, await dbContext.Database.GetAppliedMigrationsAsync());
        Assert.Equal("YES", await ColumnNullableAsync(dbContext, "machines", "WorkCenterId"));
        foreach (var column in new[] { "MachineId", "MachineCode", "MachineName" }) {
            Assert.Equal("YES", await ColumnNullableAsync(
                dbContext, "production_order_operations", column));
        }

        var occupancyIndex = await ScalarAsync(dbContext, """
            SELECT indexdef FROM pg_indexes
            WHERE indexname = 'IX_production_order_operations_one_in_progress_per_machine';
            """);
        Assert.Contains("UNIQUE", occupancyIndex, StringComparison.Ordinal);
        Assert.Contains("in_progress", occupancyIndex, StringComparison.Ordinal);
        Assert.Contains("MachineId", occupancyIndex, StringComparison.Ordinal);
        Assert.Equal("r", await ForeignKeyDeleteActionAsync(
            dbContext, "machines", "work_centers"));
        Assert.Equal("r", await ForeignKeyDeleteActionAsync(
            dbContext, "production_order_operations", "machines"));
        Assert.Contains("Machine_snapshot_consistent", await ScalarAsync(dbContext, """
            SELECT conname FROM pg_constraint
            WHERE conrelid = 'production_order_operations'::regclass
              AND conname = 'CK_production_order_operations_Machine_snapshot_consistent';
            """), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Migration_preserves_legacy_machines_and_operations_without_fabricated_assignments() {
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        var migrator = dbContext.Database.GetService<IMigrator>();
        try {
            await migrator.MigrateAsync(PreviousMigration);
            await dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE companies CASCADE;");
            var companyId = Guid.Parse("61000000-0000-0000-0000-000000000001");
            var productId = Guid.Parse("61000000-0000-0000-0000-000000000101");
            var workCenterId = Guid.Parse("61000000-0000-0000-0000-000000000201");
            var createdAt = new DateTime(2026, 9, 6, 0, 0, 0, DateTimeKind.Utc);
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO companies ("Id", "Name", "CreatedAt")
                VALUES ({companyId}, {"Legacy Machine Company"}, {createdAt});
                INSERT INTO products ("Id", "CompanyId", "Code", "Name", "CreatedAt", "UpdatedAt")
                VALUES ({productId}, {companyId}, {"P-LEGACY-MACHINE"}, {"Legacy Product"}, {createdAt}, {createdAt});
                INSERT INTO work_centers
                    ("Id", "CompanyId", "Code", "Name", "IsActive", "CreatedAt", "UpdatedAt")
                VALUES
                    ({workCenterId}, {companyId}, {"WC-LEGACY"}, {"Legacy Work Center"}, TRUE, {createdAt}, {createdAt});
                """);
            foreach (var status in new[] {
                         MachineStatuses.Available,
                         MachineStatuses.Running,
                         MachineStatuses.Maintenance,
                         MachineStatuses.Offline
                     }) {
                await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO machines
                        ("Id", "CompanyId", "Code", "Name", "Status", "CreatedAt", "UpdatedAt")
                    VALUES
                        ({Guid.NewGuid()}, {companyId}, {$"M-{status}"}, {$"Machine {status}"},
                         {status}, {createdAt}, {createdAt});
                    """);
            }
            foreach (var status in new[] {
                         ProductionOperationStatuses.Pending,
                         ProductionOperationStatuses.InProgress,
                         ProductionOperationStatuses.Completed
                     }) {
                var orderId = Guid.NewGuid();
                await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO production_orders
                        ("Id", "CompanyId", "Number", "ProductId", "Quantity", "Status", "CreatedAt", "UpdatedAt")
                    VALUES
                        ({orderId}, {companyId}, {$"PO-{status}"}, {productId}, {1m},
                         {ProductionOrderStatuses.InProgress}, {createdAt}, {createdAt});
                    INSERT INTO production_order_operations
                        ("Id", "CompanyId", "ProductionOrderId", "Sequence", "Name", "WorkCenterId",
                         "WorkCenterCode", "WorkCenterName", "SetupTimeMinutes", "RunTimeMinutes", "Status", "CreatedAt")
                    VALUES
                        ({Guid.NewGuid()}, {companyId}, {orderId}, {10}, {$"Operation {status}"}, {workCenterId},
                         {"WC-LEGACY"}, {"Legacy Work Center"}, {0}, {1}, {status}, {createdAt});
                    """);
            }

            await migrator.MigrateAsync(MachineAssignmentMigration);
            dbContext.ChangeTracker.Clear();
            Assert.Equal(4, await dbContext.Machines.CountAsync());
            Assert.All(await dbContext.Machines.ToListAsync(), machine => Assert.Null(machine.WorkCenterId));
            Assert.Equal(3, await dbContext.ProductionOrderOperations.CountAsync());
            Assert.All(await dbContext.ProductionOrderOperations.ToListAsync(), operation => {
                Assert.Null(operation.MachineId);
                Assert.Null(operation.MachineCode);
                Assert.Null(operation.MachineName);
            });
        } finally {
            await migrator.MigrateAsync();
        }
    }

    private static Task<string> ColumnNullableAsync(
        FactoryMindDbContext dbContext,
        string table,
        string column) => ScalarAsync(dbContext, $"""
            SELECT is_nullable FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = '{table}' AND column_name = '{column}';
            """);

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
