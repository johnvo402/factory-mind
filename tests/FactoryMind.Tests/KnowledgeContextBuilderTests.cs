using FactoryMind.Application.Features.Chat.Rag;
using FactoryMind.Application.Features.Knowledge;

namespace FactoryMind.Tests;

public sealed class KnowledgeContextBuilderTests {
    [Fact]
    public async Task Context_labels_sources_and_stays_within_the_prompt_limit() {
        var repository = new FakeKnowledgeSearchRepository();
        repository.Results.Add(new KnowledgeSearchResult(
            Guid.NewGuid(),
            "Safety manual",
            "safety.pdf",
            Guid.NewGuid(),
            7,
            new string('x', 10_000),
            0.88));
        var embeddingClient = new FakeEmbeddingClient();
        var builder = new KnowledgeContextBuilder(new KnowledgeRetriever(embeddingClient, repository));

        var context = await builder.BuildAsync(Guid.NewGuid(), "  machine safety  ", CancellationToken.None);

        Assert.True(context.Prompt.Length <= KnowledgeContextBuilder.MaximumContextLength);
        Assert.Contains("[S1] Document: Safety manual", context.Prompt);
        Assert.Contains("Page: 7", context.Prompt);
        var source = Assert.Single(context.Sources);
        Assert.Equal(1, source.ReferenceNumber);
        Assert.EndsWith("...", source.Excerpt);
        Assert.Equal("machine safety", embeddingClient.Input);
        Assert.Equal(KnowledgeContextBuilder.SearchLimit, repository.Limit);
    }

    [Fact]
    public async Task Empty_retrieval_tells_the_model_that_no_sources_were_found() {
        var builder = CreateBuilder(new FakeKnowledgeSearchRepository());

        var context = await builder.BuildAsync(Guid.NewGuid(), "question", CancellationToken.None);

        Assert.Empty(context.Sources);
        Assert.Contains("No company knowledge sources were retrieved.", context.Prompt);
    }

    private static KnowledgeContextBuilder CreateBuilder(FakeKnowledgeSearchRepository repository) {
        var retriever = new KnowledgeRetriever(new FakeEmbeddingClient(), repository);
        return new KnowledgeContextBuilder(retriever);
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
            return Task.FromResult(new EmbeddingBatch("test-model", vectors));
        }
    }

    private sealed class FakeKnowledgeSearchRepository : IKnowledgeSearchRepository {
        public List<KnowledgeSearchResult> Results { get; } = [];
        public int? Limit { get; private set; }

        public Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(
            Guid companyId,
            string embeddingModel,
            float[] queryEmbedding,
            int limit,
            CancellationToken cancellationToken) {
            Limit = limit;
            return Task.FromResult<IReadOnlyList<KnowledgeSearchResult>>(Results);
        }
    }
}
