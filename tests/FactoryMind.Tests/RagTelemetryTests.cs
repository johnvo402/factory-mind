using System.Diagnostics;
using FactoryMind.Application.Features.Chat;
using FactoryMind.Application.Features.Chat.Rag;
using FactoryMind.Application.Features.Knowledge;

namespace FactoryMind.Tests;

public sealed class RagTelemetryTests {
    [Fact]
    public async Task Chat_context_creates_safe_parent_child_activities_and_low_cardinality_metrics() {
        var companyId = Guid.Parse("73000000-0000-0000-0000-000000000001");
        var knowledgeRepository = new FakeKnowledgeRepository();
        knowledgeRepository.Candidates.Add(new KnowledgeSearchCandidate(
            Guid.Parse("73000000-0000-0000-0000-000000000002"),
            "Private SOP",
            "private.pdf",
            Guid.Parse("73000000-0000-0000-0000-000000000003"),
            0,
            1,
            "document-private-chunk",
            VectorRank: 1,
            LexicalRank: 1));
        var knowledgeBuilder = new KnowledgeContextBuilder(new KnowledgeRetriever(
            new FakeEmbeddingClient(),
            knowledgeRepository));
        var businessBuilder = new BusinessContextBuilder(new FakeBusinessRepository());
        var builder = new ChatContextBuilder(new IntentRouter(), knowledgeBuilder, businessBuilder);
        using var telemetry = new TelemetryTestListener();
        using var testRoot = new Activity("rag-telemetry-test").Start();

        var result = await builder.BuildAsync(
            companyId,
            "PO-001 super-secret-question SOP yêu cầu gì?",
            CancellationToken.None);

        Assert.Single(result.Sources);
        Assert.Single(result.BusinessEvidence);
        var activities = telemetry.Activities
            .Where(activity => activity.TraceId == testRoot.TraceId)
            .ToList();
        var context = Assert.Single(activities, activity =>
            activity.Name == "factorymind.chat.context");
        var route = Assert.Single(activities, activity =>
            activity.Name == "factorymind.chat.route");
        var knowledge = Assert.Single(activities, activity =>
            activity.Name == "factorymind.rag.knowledge");
        var rank = Assert.Single(activities, activity =>
            activity.Name == "factorymind.rag.rank");
        var business = Assert.Single(activities, activity =>
            activity.Name == "factorymind.rag.business");
        Assert.Equal(context.TraceId, route.TraceId);
        Assert.Equal(context.SpanId, route.ParentSpanId);
        Assert.Equal(context.SpanId, knowledge.ParentSpanId);
        Assert.Equal(knowledge.SpanId, rank.ParentSpanId);
        Assert.Equal(context.SpanId, business.ParentSpanId);
        Assert.Contains(telemetry.Measurements, measurement =>
            measurement.Name == "factorymind.rag.results" && measurement.Value == 1);
        Assert.Contains(telemetry.Measurements, measurement =>
            measurement.Name == "factorymind.rag.business.evidence"
            && measurement.Value == 1
            && measurement.HasTags(("intent", "hybrid")));
        Assert.Contains(telemetry.Measurements, measurement =>
            measurement.Name == "factorymind.chat.context.requests"
            && measurement.Value == 1
            && measurement.HasTags(("intent", "hybrid"), ("outcome", "success")));

        var tagText = string.Join('|', telemetry.Measurements
            .SelectMany(measurement => measurement.Tags.Values)
            .Concat(activities.SelectMany(activity => activity.Tags.Values)));
        Assert.DoesNotContain("super-secret-question", tagText, StringComparison.Ordinal);
        Assert.DoesNotContain("document-private-chunk", tagText, StringComparison.Ordinal);
        Assert.DoesNotContain(companyId.ToString(), tagText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer abc", tagText, StringComparison.Ordinal);
    }

    private sealed class FakeEmbeddingClient : IEmbeddingClient {
        public Task<EmbeddingBatch> CreateAsync(
            IReadOnlyList<string> inputs,
            EmbeddingPurpose purpose,
            CancellationToken cancellationToken) => Task.FromResult(new EmbeddingBatch(
                "telemetry-embedding-model",
                [new float[DocumentEmbeddingConstraints.Dimensions]]));
    }

    private sealed class FakeKnowledgeRepository : IKnowledgeSearchRepository {
        public List<KnowledgeSearchCandidate> Candidates { get; } = [];

        public Task<IReadOnlyList<KnowledgeSearchCandidate>> RetrieveCandidatesAsync(
            Guid companyId,
            string embeddingModel,
            string normalizedQuery,
            float[] queryEmbedding,
            int vectorLimit,
            int lexicalLimit,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<KnowledgeSearchCandidate>>(Candidates);
    }

    private sealed class FakeBusinessRepository : IBusinessContextRepository {
        public Task<IReadOnlyList<BusinessDataRecord>> RetrieveAsync(
            Guid companyId,
            string question,
            BusinessDataScope scopes,
            string? machineStatus,
            string? productionOrderStatus,
            int limitPerScope,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<BusinessDataRecord>>([
                new BusinessDataRecord(
                    Guid.Parse("73000000-0000-0000-0000-000000000004"),
                    "production_order",
                    "PO-001",
                    "Status: in_progress.")
            ]);
    }
}
