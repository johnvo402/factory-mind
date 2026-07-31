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
        var expected = new KnowledgeSearchResult(
            Guid.NewGuid(),
            "Safety manual",
            "safety.pdf",
            Guid.NewGuid(),
            3,
            "Stop the machine before maintenance.",
            0.93);
        repository.Results.Add(expected);
        var retriever = new KnowledgeRetriever(embeddingClient, repository);
        var handler = new SearchKnowledgeQueryHandler(retriever, currentUser);

        var result = await handler.Handle(
            new SearchKnowledgeQuery("  stop machine  ", 7),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("stop machine", embeddingClient.Input);
        Assert.Equal(currentUser.CompanyId, repository.CompanyId);
        Assert.Equal("test-embedding-model", repository.EmbeddingModel);
        Assert.Equal(7, repository.Limit);
        Assert.Equal(expected, Assert.Single(result.Value!));
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
            CancellationToken cancellationToken) {
            Input = Assert.Single(inputs);
            IReadOnlyList<float[]> vectors = [new float[DocumentEmbeddingConstraints.Dimensions]];
            return Task.FromResult(new EmbeddingBatch("test-embedding-model", vectors));
        }
    }

    private sealed class FakeKnowledgeSearchRepository : IKnowledgeSearchRepository {
        public List<KnowledgeSearchResult> Results { get; } = [];
        public Guid? CompanyId { get; private set; }
        public string? EmbeddingModel { get; private set; }
        public int? Limit { get; private set; }

        public Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(
            Guid companyId,
            string embeddingModel,
            float[] queryEmbedding,
            int limit,
            CancellationToken cancellationToken) {
            CompanyId = companyId;
            EmbeddingModel = embeddingModel;
            Limit = limit;
            return Task.FromResult<IReadOnlyList<KnowledgeSearchResult>>(Results);
        }
    }
}
