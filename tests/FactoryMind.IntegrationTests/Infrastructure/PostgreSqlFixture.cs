using FactoryMind.Application.Features.Auth;
using FactoryMind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace FactoryMind.IntegrationTests.Infrastructure;

public sealed class PostgreSqlFixture : IAsyncLifetime {
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("pgvector/pgvector:0.8.6-pg17")
        .WithDatabase("factorymind_integration")
        .WithUsername("postgres")
        .WithPassword("factorymind-integration-password")
        .Build();

    public FactoryMindApiFactory ApiFactory { get; private set; } = null!;

    public async Task InitializeAsync() {
        await _database.StartAsync();
        var connectionString = _database.GetConnectionString();
        await using (var connection = new NpgsqlConnection(connectionString)) {
            await connection.OpenAsync();
        }

        ApiFactory = new FactoryMindApiFactory(connectionString);
        await ApiFactory.StartAsync();
    }

    public async Task ResetDatabaseAsync() {
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        await dbContext.Database.MigrateAsync();
        await dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE companies CASCADE;");

        var credentialHasher = scope.ServiceProvider.GetRequiredService<ICredentialHasher>();
        TestData.Seed(dbContext, credentialHasher);
        await dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync() {
        await ApiFactory.DisposeAsync();
        await _database.DisposeAsync();
    }
}
