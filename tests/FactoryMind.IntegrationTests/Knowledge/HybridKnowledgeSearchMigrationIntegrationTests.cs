using System.Data.Common;
using FactoryMind.Application.Features.Knowledge;
using FactoryMind.Domain.Knowledge;
using FactoryMind.Infrastructure.Persistence;
using FactoryMind.Infrastructure.Persistence.Knowledge;
using FactoryMind.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Pgvector;

namespace FactoryMind.IntegrationTests.Knowledge;

[Collection(IntegrationTestCollection.Name)]
public sealed class HybridKnowledgeSearchMigrationIntegrationTests(PostgreSqlFixture fixture)
    : IntegrationTestBase(fixture) {
    private const string PreviousMigration =
        "20260906065401_AddMachineWorkCenterAndOperationAssignment";
    private const string HybridMigration = "20260906144511_AddHybridKnowledgeSearch";

    [Fact]
    public async Task Fresh_database_has_simple_full_text_gin_index_and_pgvector_schema() {
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();

        Assert.Contains(HybridMigration, await dbContext.Database.GetAppliedMigrationsAsync());
        var indexDefinition = await ScalarAsync(dbContext, """
            SELECT indexdef FROM pg_indexes
            WHERE schemaname = 'public' AND indexname = 'IX_document_chunks_Content_fts';
            """);
        Assert.Contains("USING gin", indexDefinition, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("to_tsvector('simple'", indexDefinition, StringComparison.OrdinalIgnoreCase);
        var plan = await ExplainLexicalSearchAsync(dbContext);
        Assert.Contains("IX_document_chunks_Content_fts", plan, StringComparison.Ordinal);
        Assert.Equal("vector", await ScalarAsync(dbContext, """
            SELECT typname
            FROM pg_attribute
            JOIN pg_class ON pg_class.oid = pg_attribute.attrelid
            JOIN pg_type ON pg_type.oid = pg_attribute.atttypid
            WHERE pg_class.relname = 'document_embeddings'
              AND pg_attribute.attname = 'Embedding';
            """));
    }

    [Fact]
    public async Task Migration_preserves_legacy_documents_chunks_and_embeddings() {
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        var migrator = dbContext.Database.GetService<IMigrator>();
        try {
            await migrator.MigrateAsync(PreviousMigration);
            await dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE companies CASCADE;");
            var companyId = Guid.Parse("62000000-0000-0000-0000-000000000001");
            var userId = Guid.Parse("62000000-0000-0000-0000-000000000002");
            var documentId = Guid.Parse("62000000-0000-0000-0000-000000000003");
            var chunkId = Guid.Parse("62000000-0000-0000-0000-000000000004");
            var createdAt = new DateTime(2026, 9, 6, 0, 0, 0, DateTimeKind.Utc);
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO companies ("Id", "Name", "CreatedAt")
                VALUES ({companyId}, {"Legacy RAG Company"}, {createdAt});
                INSERT INTO users
                    ("Id", "CompanyId", "Name", "Email", "Role", "PasswordHash", "IsActive", "CreatedAt")
                VALUES ({userId}, {companyId}, {"Legacy User"}, {"legacy-rag@example.test"},
                    {"User"}, {"legacy-password-hash"}, TRUE, {createdAt});
                INSERT INTO documents
                    ("Id", "CompanyId", "UploadedByUserId", "Title", "FileName", "ContentType", "Path",
                     "Size", "Status", "PageCount", "ChunkCount", "CreatedAt")
                VALUES ({documentId}, {companyId}, {userId}, {"Legacy SOP"}, {"legacy.pdf"},
                    {"application/pdf"}, {"legacy/path.pdf"}, {42L}, {DocumentStatuses.Ready}, {1}, {1}, {createdAt});
                INSERT INTO document_chunks
                    ("Id", "DocumentId", "CompanyId", "Sequence", "PageNumber", "Content", "CreatedAt")
                VALUES ({chunkId}, {documentId}, {companyId}, {0}, {1},
                    {"Legacy SOP-CNC-001 content"}, {createdAt});
                """);
            dbContext.DocumentEmbeddings.Add(new DocumentEmbeddingRecord {
                Id = Guid.Parse("62000000-0000-0000-0000-000000000005"),
                DocumentChunkId = chunkId,
                CompanyId = companyId,
                Model = "legacy-model",
                Dimensions = DocumentEmbeddingConstraints.Dimensions,
                Embedding = new Vector(VectorValues())
            });
            await dbContext.SaveChangesAsync();

            await migrator.MigrateAsync(HybridMigration);
            dbContext.ChangeTracker.Clear();

            Assert.Equal("Legacy SOP", (await dbContext.Documents.SingleAsync()).Title);
            Assert.Equal("Legacy SOP-CNC-001 content", (await dbContext.DocumentChunks.SingleAsync()).Content);
            Assert.Equal("legacy-model", (await dbContext.DocumentEmbeddings.SingleAsync()).Model);
        } finally {
            await migrator.MigrateAsync();
        }
    }

    private static float[] VectorValues() {
        var values = new float[DocumentEmbeddingConstraints.Dimensions];
        values[0] = 1f;
        return values;
    }

    private static async Task<string> ScalarAsync(FactoryMindDbContext dbContext, string sql) {
        await dbContext.Database.OpenConnectionAsync();
        await using DbCommand command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)
            ?? throw new InvalidOperationException("Schema query returned null.");
    }

    private static async Task<string> ExplainLexicalSearchAsync(FactoryMindDbContext dbContext) {
        await dbContext.Database.OpenConnectionAsync();
        await dbContext.Database.ExecuteSqlRawAsync("SET enable_seqscan = off;");
        try {
            await using DbCommand command = dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                EXPLAIN
                SELECT "Id" FROM document_chunks
                WHERE to_tsvector('simple', COALESCE("Content", ''))
                    @@ plainto_tsquery('simple', 'SOP-CNC-042');
                """;
            await using var reader = await command.ExecuteReaderAsync();
            var lines = new List<string>();
            while (await reader.ReadAsync()) {
                lines.Add(reader.GetString(0));
            }

            return string.Join('\n', lines);
        } finally {
            await dbContext.Database.ExecuteSqlRawAsync("RESET enable_seqscan;");
        }
    }
}
