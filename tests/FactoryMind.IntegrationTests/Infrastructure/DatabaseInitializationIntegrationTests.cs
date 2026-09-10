using FactoryMind.Infrastructure.Persistence;
using FactoryMind.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace FactoryMind.IntegrationTests.Infrastructure;

[Collection(IntegrationTestCollection.Name)]
public sealed class DatabaseInitializationIntegrationTests(PostgreSqlFixture fixture) {
    [Fact]
    public async Task Explicit_migration_is_idempotent_and_bootstraps_one_admin_on_an_empty_database() {
        var databaseName = $"factorymind_migration_{Guid.NewGuid():N}";
        var baseConnection = new NpgsqlConnectionStringBuilder(fixture.ConnectionString);
        var adminConnection = new NpgsqlConnectionStringBuilder(baseConnection.ConnectionString) {
            Database = "postgres"
        };

        await using (var connection = new NpgsqlConnection(adminConnection.ConnectionString)) {
            await connection.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
            await create.ExecuteNonQueryAsync();
        }

        try {
            var databaseConnection = new NpgsqlConnectionStringBuilder(baseConnection.ConnectionString) {
                Database = databaseName
            };
            var options = new DbContextOptionsBuilder<FactoryMindDbContext>()
                .UseNpgsql(databaseConnection.ConnectionString, postgres => postgres.UseVector())
                .Options;

            await using var dbContext = new FactoryMindDbContext(options);
            var initializer = new FactoryMindDatabaseInitializer(
                dbContext,
                new CredentialHasher(),
                Options.Create(new BootstrapAdminSettings {
                    CompanyName = "Migration Test Factory",
                    Name = "Migration Test Admin",
                    Email = "migration-admin@factorymind.test",
                    Password = "FactoryMind@Test#2026"
                }),
                new TestHostEnvironment(Environments.Production));

            await InitializeAsync(initializer);
            await InitializeAsync(initializer);

            Assert.Empty(await dbContext.Database.GetPendingMigrationsAsync());
            Assert.NotEmpty(await dbContext.Database.GetAppliedMigrationsAsync());
            Assert.Equal(1, await dbContext.Companies.CountAsync());
            var admin = Assert.Single(await dbContext.Users.ToListAsync());
            Assert.Equal("migration-admin@factorymind.test", admin.Email);
            Assert.Equal("Admin", admin.Role);
        } finally {
            NpgsqlConnection.ClearAllPools();
            await using var connection = new NpgsqlConnection(adminConnection.ConnectionString);
            await connection.OpenAsync();
            await using var drop = new NpgsqlCommand(
                $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)",
                connection);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task InitializeAsync(FactoryMindDatabaseInitializer initializer) {
        await initializer.MigrateAsync();
        await initializer.BootstrapAsync();
        await initializer.SeedDevelopmentAsync();
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "FactoryMind.IntegrationTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
