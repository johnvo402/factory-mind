using FactoryMind.Application.Features.Chat;
using FactoryMind.Application.Features.Chat.Rag;

namespace FactoryMind.Tests;

public sealed class HybridRagTests {
    [Theory]
    [InlineData("Máy nào đang rảnh?", ChatIntent.Business, BusinessDataScope.Machines)]
    [InlineData("Kho còn bao nhiêu nguyên liệu?", ChatIntent.Business, BusinessDataScope.Materials | BusinessDataScope.Inventory)]
    [InlineData("Hướng dẫn SOP an toàn", ChatIntent.Knowledge, BusinessDataScope.None)]
    [InlineData("Theo SOP, máy nào đang bảo trì?", ChatIntent.Hybrid, BusinessDataScope.Machines)]
    [InlineData("Có nên nhận đơn hàng này?", ChatIntent.Hybrid, BusinessDataScope.ProductionOrders)]
    public void Intent_router_classifies_supported_questions(
        string question,
        ChatIntent expectedIntent,
        BusinessDataScope expectedScopes) {
        var route = new IntentRouter().Route(question);

        Assert.Equal(expectedIntent, route.Intent);
        Assert.Equal(expectedScopes, route.BusinessScopes);
    }

    [Fact]
    public void Intent_router_uses_hybrid_fallback_for_ambiguous_questions() {
        var route = new IntentRouter().Route("Tình hình hôm nay thế nào?");

        Assert.Equal(ChatIntent.Hybrid, route.Intent);
        Assert.Equal(BusinessDataScope.All, route.BusinessScopes);
    }

    [Fact]
    public async Task Business_context_builder_creates_bounded_labeled_evidence() {
        var companyId = Guid.NewGuid();
        var repository = new FakeBusinessContextRepository([
            new BusinessDataRecord(
                Guid.NewGuid(),
                "machine",
                "MC-01 - Cutter",
                "status=available")
        ]);
        var builder = new BusinessContextBuilder(repository);

        var context = await builder.BuildAsync(
            companyId,
            new IntentRoute(ChatIntent.Business, BusinessDataScope.Machines, "available"),
            CancellationToken.None);

        Assert.Contains("[B1] machine: MC-01 - Cutter", context.Prompt);
        var evidence = Assert.Single(context.Evidence);
        Assert.Equal("status=available", evidence.Detail);
        Assert.Equal(companyId, repository.CompanyId);
        Assert.Equal(BusinessDataScope.Machines, repository.Scopes);
        Assert.Equal("available", repository.MachineStatus);
        Assert.Equal(BusinessContextBuilder.LimitPerScope, repository.LimitPerScope);
    }

    [Fact]
    public void Intent_router_extracts_business_status_filters() {
        var router = new IntentRouter();

        var machineRoute = router.Route("Máy nào đang bảo trì?");
        var orderRoute = router.Route("Lệnh sản xuất nào đã hoàn thành?");

        Assert.Equal("maintenance", machineRoute.MachineStatus);
        Assert.Equal("completed", orderRoute.ProductionOrderStatus);
    }

    [Fact]
    public async Task Chat_context_builder_only_runs_business_retrieval_for_business_intent() {
        var knowledge = new FakeKnowledgeContextBuilder();
        var business = new FakeBusinessContextBuilder();
        var builder = new ChatContextBuilder(
            new FixedIntentRouter(ChatIntent.Business, BusinessDataScope.Inventory),
            knowledge,
            business);

        var context = await builder.BuildAsync(Guid.NewGuid(), "Stock?", CancellationToken.None);

        Assert.Equal(0, knowledge.BuildCount);
        Assert.Equal(1, business.BuildCount);
        Assert.Contains("Business context", context.Prompt);
        Assert.Empty(context.Sources);
        Assert.Single(context.BusinessEvidence);
    }

    [Fact]
    public async Task Chat_context_builder_merges_business_and_knowledge_context() {
        var knowledge = new FakeKnowledgeContextBuilder();
        var business = new FakeBusinessContextBuilder();
        var builder = new ChatContextBuilder(
            new FixedIntentRouter(ChatIntent.Hybrid, BusinessDataScope.Machines),
            knowledge,
            business);

        var context = await builder.BuildAsync(Guid.NewGuid(), "Hybrid", CancellationToken.None);

        Assert.Equal(1, knowledge.BuildCount);
        Assert.Equal(1, business.BuildCount);
        Assert.Contains("Business context", context.Prompt);
        Assert.Contains("Knowledge context", context.Prompt);
        Assert.Single(context.Sources);
        Assert.Single(context.BusinessEvidence);
    }

    private sealed class FixedIntentRouter(
        ChatIntent intent,
        BusinessDataScope scopes) : IIntentRouter {
        public IntentRoute Route(string question) => new(intent, scopes);
    }

    private sealed class FakeKnowledgeContextBuilder : IKnowledgeContextBuilder {
        public int BuildCount { get; private set; }

        public Task<KnowledgeContext> BuildAsync(
            Guid companyId,
            string question,
            CancellationToken cancellationToken) {
            BuildCount++;
            return Task.FromResult(new KnowledgeContext(
                "Knowledge context",
                [new CitationResponse(
                    1,
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "Manual",
                    "manual.pdf",
                    1,
                    "Excerpt",
                    0.9)]));
        }
    }

    private sealed class FakeBusinessContextBuilder : IBusinessContextBuilder {
        public int BuildCount { get; private set; }

        public Task<BusinessContext> BuildAsync(
            Guid companyId,
            IntentRoute route,
            CancellationToken cancellationToken) {
            BuildCount++;
            return Task.FromResult(new BusinessContext(
                "Business context",
                [new BusinessEvidenceResponse(
                    1,
                    Guid.NewGuid(),
                    "inventory",
                    "RM-01",
                    "quantity=10")]));
        }
    }

    private sealed class FakeBusinessContextRepository(
        IReadOnlyList<BusinessDataRecord> records) : IBusinessContextRepository {
        public Guid? CompanyId { get; private set; }
        public BusinessDataScope? Scopes { get; private set; }
        public string? MachineStatus { get; private set; }
        public int? LimitPerScope { get; private set; }

        public Task<IReadOnlyList<BusinessDataRecord>> RetrieveAsync(
            Guid companyId,
            BusinessDataScope scopes,
            string? machineStatus,
            string? productionOrderStatus,
            int limitPerScope,
            CancellationToken cancellationToken) {
            CompanyId = companyId;
            Scopes = scopes;
            MachineStatus = machineStatus;
            LimitPerScope = limitPerScope;
            return Task.FromResult(records);
        }
    }
}
