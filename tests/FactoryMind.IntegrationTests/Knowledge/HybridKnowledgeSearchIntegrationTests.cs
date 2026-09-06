using FactoryMind.Application.Features.Knowledge;
using FactoryMind.Domain.Knowledge;
using FactoryMind.Infrastructure.Persistence;
using FactoryMind.Infrastructure.Persistence.Knowledge;
using FactoryMind.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pgvector;

namespace FactoryMind.IntegrationTests.Knowledge;

[Collection(IntegrationTestCollection.Name)]
public sealed class HybridKnowledgeSearchIntegrationTests(PostgreSqlFixture fixture)
    : IntegrationTestBase(fixture) {
    [Fact]
    public async Task Hybrid_search_preserves_semantic_winner_with_weak_lexical_overlap() {
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        var userId = await UserIdAsync(dbContext, TestData.CompanyAId);
        var expected = AddDocument(
            dbContext,
            TestData.CompanyAId,
            userId,
            "Energy isolation",
            "energy.pdf",
            "Disconnect every energy source and verify zero energy before service.",
            Vector(1f, 0f));
        AddDocument(
            dbContext,
            TestData.CompanyAId,
            userId,
            "Keyword decoy",
            "decoy.pdf",
            "Prevent unexpected energization with a generic checklist.",
            Vector(0f, 1f));
        await dbContext.SaveChangesAsync();

        var results = await SearchAsync(
            dbContext,
            TestData.CompanyAId,
            "prevent unexpected energization",
            Vector(1f, 0f));

        Assert.Contains(results, result => result.ChunkId == expected.Id);
    }

    [Fact]
    public async Task Exact_identifier_is_recovered_by_lexical_search_and_ranked_first() {
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        var userId = await UserIdAsync(dbContext, TestData.CompanyAId);
        var expected = AddDocument(
            dbContext,
            TestData.CompanyAId,
            userId,
            "CNC emergency SOP",
            "cnc-safety.pdf",
            "SOP-CNC-042 requires emergency stop verification before spindle access.",
            Vector(0f, 1f));
        AddDocument(
            dbContext,
            TestData.CompanyAId,
            userId,
            "Semantic decoy",
            "general.pdf",
            "General emergency stop verification.",
            Vector(1f, 0f));
        await dbContext.SaveChangesAsync();

        var results = await SearchAsync(
            dbContext,
            TestData.CompanyAId,
            "SOP-CNC-042 nói gì về emergency stop?",
            Vector(1f, 0f));

        Assert.Equal(expected.Id, results[0].ChunkId);
    }

    [Fact]
    public async Task Rrf_ranks_dual_channel_candidate_above_single_channel_candidates() {
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        var userId = await UserIdAsync(dbContext, TestData.CompanyAId);
        var both = AddDocument(
            dbContext,
            TestData.CompanyAId,
            userId,
            "Lockout standard",
            "lockout.pdf",
            "Lockout verification requires testing for zero energy.",
            Vector(0.99f, 0.1f));
        AddDocument(
            dbContext,
            TestData.CompanyAId,
            userId,
            "Vector only",
            "semantic.pdf",
            "Isolation confirmation guidance.",
            Vector(1f, 0f));
        AddDocument(
            dbContext,
            TestData.CompanyAId,
            userId,
            "Lexical only",
            "lexical.pdf",
            "Lockout verification appears here without semantic alignment.",
            Vector(0f, 1f));
        await dbContext.SaveChangesAsync();

        var results = await SearchAsync(
            dbContext,
            TestData.CompanyAId,
            "lockout verification",
            Vector(1f, 0f));

        Assert.Equal(both.Id, results[0].ChunkId);
    }

    [Fact]
    public async Task Simple_full_text_search_matches_accented_vietnamese_terms() {
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        var userId = await UserIdAsync(dbContext, TestData.CompanyAId);
        var expected = AddDocument(
            dbContext,
            TestData.CompanyAId,
            userId,
            "Hướng dẫn sơn",
            "huong-dan-son.pdf",
            "Hướng dẫn kiểm tra độ dày lớp phủ trước khi đóng gói.",
            Vector(0f, 1f));
        await dbContext.SaveChangesAsync();
        var repository = new EfKnowledgeSearchRepository(dbContext);

        var candidates = await repository.RetrieveCandidatesAsync(
            TestData.CompanyAId,
            "integration-model",
            "hướng dẫn độ dày",
            Vector(1f, 0f),
            20,
            20,
            CancellationToken.None);

        Assert.Contains(candidates, candidate =>
            candidate.ChunkId == expected.Id && candidate.LexicalRank.HasValue);
    }

    [Fact]
    public async Task Search_enforces_tenant_ready_status_and_embedding_model_boundaries() {
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        var companyAUser = await UserIdAsync(dbContext, TestData.CompanyAId);
        var companyBUser = await UserIdAsync(dbContext, TestData.CompanyBId);
        var expected = AddDocument(
            dbContext,
            TestData.CompanyAId,
            companyAUser,
            "Visible",
            "visible.pdf",
            "TENANT-TERM visible ready evidence.",
            Vector(1f, 0f));
        var foreign = AddDocument(
            dbContext,
            TestData.CompanyBId,
            companyBUser,
            "Foreign",
            "foreign.pdf",
            "TENANT-TERM visible ready evidence.",
            Vector(1f, 0f));
        var processing = AddDocument(
            dbContext,
            TestData.CompanyAId,
            companyAUser,
            "Processing",
            "processing.pdf",
            "TENANT-TERM processing evidence.",
            Vector(1f, 0f),
            DocumentStatuses.Processing);
        var failed = AddDocument(
            dbContext,
            TestData.CompanyAId,
            companyAUser,
            "Failed",
            "failed.pdf",
            "TENANT-TERM failed evidence.",
            Vector(1f, 0f),
            DocumentStatuses.Failed);
        var otherModel = AddDocument(
            dbContext,
            TestData.CompanyAId,
            companyAUser,
            "Other model",
            "other-model.pdf",
            "unrelated model-only evidence.",
            Vector(1f, 0f),
            embeddingModel: "other-model");
        await dbContext.SaveChangesAsync();

        var repository = new EfKnowledgeSearchRepository(dbContext);
        var candidates = await repository.RetrieveCandidatesAsync(
            TestData.CompanyAId,
            "integration-model",
            "tenant-term",
            Vector(1f, 0f),
            20,
            20,
            CancellationToken.None);

        Assert.Contains(candidates, candidate => candidate.ChunkId == expected.Id);
        Assert.DoesNotContain(candidates, candidate => candidate.ChunkId == foreign.Id);
        Assert.DoesNotContain(candidates, candidate => candidate.ChunkId == processing.Id);
        Assert.DoesNotContain(candidates, candidate => candidate.ChunkId == failed.Id);
        Assert.DoesNotContain(candidates, candidate =>
            candidate.ChunkId == otherModel.Id && candidate.VectorRank.HasValue);
    }

    [Fact]
    public async Task Reindex_replaces_old_chunks_and_embeddings_and_new_content_is_lexically_searchable() {
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        var userId = await UserIdAsync(dbContext, TestData.CompanyAId);
        var oldChunk = AddDocument(
            dbContext,
            TestData.CompanyAId,
            userId,
            "Reindexed SOP",
            "reindexed.pdf",
            "OLD-CONTENT obsolete procedure.",
            Vector(1f, 0f));
        await dbContext.SaveChangesAsync();
        var documentId = oldChunk.Document!.Id;
        dbContext.ChangeTracker.Clear();
        var document = await dbContext.Documents.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == documentId);
        var newChunk = new DocumentChunk {
            DocumentId = documentId,
            CompanyId = TestData.CompanyAId,
            Sequence = 0,
            PageNumber = 1,
            Content = "NEW-SOP-909 replacement calibration procedure."
        };
        var repository = new EfDocumentRepository(dbContext);

        await repository.CompleteProcessingAsync(
            document,
            [newChunk],
            [new DocumentEmbeddingDraft(
                newChunk.Id,
                TestData.CompanyAId,
                "integration-model",
                Vector(1f, 0f))],
            1,
            DateTime.UtcNow,
            CancellationToken.None);
        dbContext.ChangeTracker.Clear();

        Assert.False(await dbContext.DocumentChunks.AnyAsync(chunk => chunk.Id == oldChunk.Id));
        Assert.False(await dbContext.DocumentEmbeddings.AnyAsync(embedding =>
            embedding.DocumentChunkId == oldChunk.Id));
        Assert.True(await dbContext.DocumentEmbeddings.AnyAsync(embedding =>
            embedding.DocumentChunkId == newChunk.Id));
        var candidates = await new EfKnowledgeSearchRepository(dbContext).RetrieveCandidatesAsync(
            TestData.CompanyAId,
            "integration-model",
            "new-sop-909",
            Vector(0f, 1f),
            20,
            20,
            CancellationToken.None);
        Assert.Contains(candidates, candidate =>
            candidate.ChunkId == newChunk.Id && candidate.LexicalRank.HasValue);
    }

    private static async Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(
        FactoryMindDbContext dbContext,
        Guid companyId,
        string query,
        float[] vector) {
        var repository = new EfKnowledgeSearchRepository(dbContext);
        var candidates = await repository.RetrieveCandidatesAsync(
            companyId,
            "integration-model",
            query.ToLowerInvariant(),
            vector,
            20,
            20,
            CancellationToken.None);
        return HybridKnowledgeRanker.Rank(query, candidates, 8);
    }

    private static DocumentChunk AddDocument(
        FactoryMindDbContext dbContext,
        Guid companyId,
        Guid userId,
        string title,
        string fileName,
        string content,
        float[] embedding,
        string status = DocumentStatuses.Ready,
        string embeddingModel = "integration-model") {
        var document = new KnowledgeDocument {
            CompanyId = companyId,
            UploadedByUserId = userId,
            Title = title,
            FileName = fileName,
            ContentType = "application/pdf",
            Path = $"tests/{companyId:N}/{fileName}",
            Size = content.Length,
            Status = status,
            PageCount = 1,
            ChunkCount = 1
        };
        var chunk = new DocumentChunk {
            Document = document,
            CompanyId = companyId,
            Sequence = 0,
            PageNumber = 1,
            Content = content
        };
        dbContext.AddRange(document, chunk);
        dbContext.DocumentEmbeddings.Add(new DocumentEmbeddingRecord {
            DocumentChunkId = chunk.Id,
            CompanyId = companyId,
            Model = embeddingModel,
            Dimensions = DocumentEmbeddingConstraints.Dimensions,
            Embedding = new Vector(embedding)
        });
        return chunk;
    }

    private static Task<Guid> UserIdAsync(FactoryMindDbContext dbContext, Guid companyId) =>
        dbContext.Users.Where(user => user.CompanyId == companyId).Select(user => user.Id).FirstAsync();

    private static float[] Vector(float first, float second) {
        var values = new float[DocumentEmbeddingConstraints.Dimensions];
        values[0] = first;
        values[1] = second;
        return values;
    }
}
