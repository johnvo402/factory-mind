using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.Knowledge;
using FactoryMind.Application.Features.Knowledge.SearchKnowledge;

namespace FactoryMind.Tests;

public sealed class KnowledgeSearchHandlerTests {
    [Fact]
    public async Task Search_embeds_trimmed_query_and_scopes_results_to_current_company() {
        var currentUser = new FakeCurrentUser();
        var embeddingClient = new FakeEmbeddingClient();
        var repository = new FakeKnowledgeSearchRepository();
        var candidate = new KnowledgeSearchCandidate(
            Guid.NewGuid(),
            "Safety manual",
            "safety.pdf",
            Guid.NewGuid(),
            0,
            3,
            "Stop the machine before maintenance.",
            VectorRank: 1,
            LexicalRank: 1,
            VectorScore: 0.93,
            LexicalScore: 0.4);
        repository.Results.Add(candidate);
        var retriever = new KnowledgeRetriever(embeddingClient, repository);
        var handler = new SearchKnowledgeQueryHandler(retriever, currentUser);

        var result = await handler.Handle(
            new SearchKnowledgeQuery("  stop machine  ", 7),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("stop machine", embeddingClient.Input);
        Assert.Equal(currentUser.CompanyId, repository.CompanyId);
        Assert.Equal("test-embedding-model", repository.EmbeddingModel);
        Assert.Equal(KnowledgeSearchConstraints.VectorCandidateLimit, repository.VectorLimit);
        Assert.Equal(KnowledgeSearchConstraints.LexicalCandidateLimit, repository.LexicalLimit);
        var match = Assert.Single(result.Value!);
        Assert.Equal(candidate.ChunkId, match.ChunkId);
    }

    private sealed class FakeCurrentUser : ICurrentUser {
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid CompanyId { get; } = Guid.NewGuid();
        public string Role => "User";
    }

    private sealed class FakeEmbeddingClient : IEmbeddingClient {
        public string? Input { get; private set; }

        public Task<EmbeddingBatch> CreateAsync(
            IReadOnlyList<string> inputs,
            EmbeddingPurpose purpose,
            CancellationToken cancellationToken) {
            Assert.Equal(EmbeddingPurpose.Query, purpose);
            Input = Assert.Single(inputs);
            IReadOnlyList<float[]> vectors = [new float[DocumentEmbeddingConstraints.Dimensions]];
            return Task.FromResult(new EmbeddingBatch("test-embedding-model", vectors));
        }
    }

    private sealed class FakeKnowledgeSearchRepository : IKnowledgeSearchRepository {
        public List<KnowledgeSearchCandidate> Results { get; } = [];
        public Guid? CompanyId { get; private set; }
        public string? EmbeddingModel { get; private set; }
        public int? VectorLimit { get; private set; }
        public int? LexicalLimit { get; private set; }

        public Task<IReadOnlyList<KnowledgeSearchCandidate>> RetrieveCandidatesAsync(
            Guid companyId,
            string embeddingModel,
            string normalizedQuery,
            float[] queryEmbedding,
            int vectorLimit,
            int lexicalLimit,
            CancellationToken cancellationToken) {
            CompanyId = companyId;
            EmbeddingModel = embeddingModel;
            VectorLimit = vectorLimit;
            LexicalLimit = lexicalLimit;
            return Task.FromResult<IReadOnlyList<KnowledgeSearchCandidate>>(Results);
        }
    }
}
