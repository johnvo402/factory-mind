using FactoryMind.Application.Features.Chat;
using FactoryMind.Application.Features.Chat.Rag;
using FactoryMind.Application.Features.Knowledge;
using FactoryMind.Domain.Knowledge;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Infrastructure.Persistence;
using FactoryMind.Infrastructure.Persistence.Chat;
using FactoryMind.Infrastructure.Persistence.Knowledge;
using FactoryMind.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pgvector;

namespace FactoryMind.IntegrationTests.Chat;

[Collection(IntegrationTestCollection.Name)]
public sealed class ManufacturingBusinessRagIntegrationTests(PostgreSqlFixture fixture)
    : IntegrationTestBase(fixture) {
    [Fact]
    public async Task Exact_machine_code_is_ranked_first_with_more_than_five_machines() {
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        var workCenter = new WorkCenter {
            CompanyId = TestData.CompanyAId,
            Code = "WC-CNC",
            Name = "CNC Cutting"
        };
        var foreignWorkCenter = new WorkCenter {
            CompanyId = TestData.CompanyBId,
            Code = "WC-CNC",
            Name = "Foreign CNC"
        };
        var foreignMachine = new Machine {
            CompanyId = TestData.CompanyBId,
            Code = "CNC-019",
            Name = "Foreign Machine",
            Status = MachineStatuses.Available,
            WorkCenter = foreignWorkCenter
        };
        dbContext.WorkCenters.AddRange(workCenter, foreignWorkCenter);
        dbContext.Machines.Add(foreignMachine);
        dbContext.Machines.AddRange(Enumerable.Range(1, 20).Select(index => new Machine {
            CompanyId = TestData.CompanyAId,
            Code = $"CNC-{index:000}",
            Name = $"CNC Machine {index:000}",
            Status = MachineStatuses.Available,
            WorkCenter = workCenter
        }));
        await dbContext.SaveChangesAsync();
        var repository = new EfBusinessContextRepository(dbContext);

        var records = await repository.RetrieveAsync(
            TestData.CompanyAId,
            "CNC-019 đang ở Work Center nào?",
            BusinessDataScope.Machines,
            null,
            null,
            5,
            CancellationToken.None);

        Assert.Equal(5, records.Count);
        Assert.StartsWith("CNC-019", records[0].Title, StringComparison.Ordinal);
        Assert.Contains("WC-CNC - CNC Cutting", records[0].Detail);
        Assert.DoesNotContain(records, record => record.EntityId == foreignMachine.Id);
    }

    [Fact]
    public async Task Production_execution_evidence_contains_current_operation_work_center_and_machine() {
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        var setup = SeedExecution(dbContext);
        await dbContext.SaveChangesAsync();
        var repository = new EfBusinessContextRepository(dbContext);

        var records = await repository.RetrieveAsync(
            TestData.CompanyAId,
            "PO-001 đang ở đâu và máy nào đang chạy?",
            BusinessDataScope.ProductionOrders
                | BusinessDataScope.ProductionOperations
                | BusinessDataScope.Machines,
            null,
            null,
            5,
            CancellationToken.None);

        var order = Assert.Single(records, record => record.EntityType == "production_order");
        Assert.Contains("PO-001", order.Title);
        Assert.Contains("Painting", order.Detail);
        Assert.Contains("PAINT", order.Detail);
        Assert.Contains("PAINT-02", order.Detail);
        Assert.Contains("in_progress", order.Detail);
        var machine = Assert.Single(records, record =>
            record.EntityType == "machine" && record.Title.StartsWith("PAINT-02", StringComparison.Ordinal));
        Assert.Contains("Current operation: Painting", machine.Detail);
        Assert.Contains("Production Order: PO-001", machine.Detail);
        Assert.Contains(setup.StartedAt.ToString("dd/MM/yyyy"), machine.Detail);
        var operation = Assert.Single(records, record =>
            record.EntityType == "production_operation"
            && record.Title.Contains("20 Painting", StringComparison.Ordinal));
        Assert.Contains("Setup estimate: 5 minutes", operation.Detail);
        Assert.Contains("Run estimate: 30 minutes", operation.Detail);
    }

    [Fact]
    public async Task Hybrid_execution_and_sop_question_builds_separate_business_and_knowledge_evidence() {
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        SeedExecution(dbContext);
        var userId = await dbContext.Users
            .Where(user => user.CompanyId == TestData.CompanyAId)
            .Select(user => user.Id)
            .FirstAsync();
        var document = new KnowledgeDocument {
            CompanyId = TestData.CompanyAId,
            UploadedByUserId = userId,
            Title = "Painting SOP",
            FileName = "painting-sop.pdf",
            ContentType = "application/pdf",
            Path = "tests/painting-sop.pdf",
            Size = 100,
            Status = DocumentStatuses.Ready,
            PageCount = 1,
            ChunkCount = 1
        };
        var chunk = new DocumentChunk {
            Document = document,
            CompanyId = TestData.CompanyAId,
            Sequence = 0,
            PageNumber = 1,
            Content = "Painting SOP requires a coating thickness check before packaging."
        };
        dbContext.AddRange(document, chunk);
        dbContext.DocumentEmbeddings.Add(new DocumentEmbeddingRecord {
            DocumentChunkId = chunk.Id,
            CompanyId = TestData.CompanyAId,
            Model = "hybrid-test-model",
            Dimensions = DocumentEmbeddingConstraints.Dimensions,
            Embedding = new Vector(QueryVector())
        });
        await dbContext.SaveChangesAsync();
        var knowledge = new KnowledgeContextBuilder(new KnowledgeRetriever(
            new FixedEmbeddingClient(),
            new EfKnowledgeSearchRepository(dbContext)));
        var business = new BusinessContextBuilder(new EfBusinessContextRepository(dbContext));
        var builder = new ChatContextBuilder(new IntentRouter(), knowledge, business);

        var context = await builder.BuildAsync(
            TestData.CompanyAId,
            "PO-001 đang Painting, SOP yêu cầu bước kiểm tra nào?",
            CancellationToken.None);

        Assert.NotEmpty(context.BusinessEvidence);
        Assert.NotEmpty(context.Sources);
        Assert.Contains("[B1]", context.Prompt);
        Assert.Contains("[S1]", context.Prompt);
        Assert.Contains("PO-001", context.Prompt);
        Assert.Contains("coating thickness", context.Prompt);
    }

    [Fact]
    public async Task Work_center_and_routing_evidence_is_bounded_and_execution_aware() {
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        SeedExecution(dbContext);
        await dbContext.SaveChangesAsync();
        var repository = new EfBusinessContextRepository(dbContext);

        var workCenters = await repository.RetrieveAsync(
            TestData.CompanyAId,
            "Work center PAINT đang chạy gì?",
            BusinessDataScope.WorkCenters,
            null,
            null,
            5,
            CancellationToken.None);
        var painting = Assert.Single(workCenters, record => record.Title.StartsWith("PAINT -", StringComparison.Ordinal));
        Assert.Contains("1 running", painting.Detail);
        Assert.Contains("PO-001 / Painting / PAINT-02", painting.Detail);

        var routings = await repository.RetrieveAsync(
            TestData.CompanyAId,
            "Routing PROD-PAINT gồm các công đoạn nào?",
            BusinessDataScope.Routings,
            null,
            null,
            5,
            CancellationToken.None);
        var routing = Assert.Single(routings);
        Assert.Contains("revision 3", routing.Title);
        Assert.Contains("10 Cutting @ CUT", routing.Detail);
        Assert.Contains("20 Painting @ PAINT", routing.Detail);
        Assert.Contains("setup 5m, run 30m", routing.Detail);
    }

    private static ExecutionSetup SeedExecution(FactoryMindDbContext dbContext) {
        var product = new Product {
            CompanyId = TestData.CompanyAId,
            Code = "PROD-PAINT",
            Name = "Painted Product"
        };
        var cutting = new WorkCenter {
            CompanyId = TestData.CompanyAId,
            Code = "CUT",
            Name = "Cutting"
        };
        var painting = new WorkCenter {
            CompanyId = TestData.CompanyAId,
            Code = "PAINT",
            Name = "Painting"
        };
        var packaging = new WorkCenter {
            CompanyId = TestData.CompanyAId,
            Code = "PACK",
            Name = "Packaging"
        };
        var machine = new Machine {
            CompanyId = TestData.CompanyAId,
            Code = "PAINT-02",
            Name = "Painting Machine 02",
            Status = MachineStatuses.Running,
            WorkCenter = painting
        };
        var routing = new Routing {
            CompanyId = TestData.CompanyAId,
            Product = product,
            Revision = 3,
            Status = RoutingStatuses.Active
        };
        routing.Operations.Add(new RoutingOperation {
            Sequence = 10,
            Name = "Cutting",
            WorkCenter = cutting,
            SetupTimeMinutes = 4,
            RunTimeMinutes = 15
        });
        routing.Operations.Add(new RoutingOperation {
            Sequence = 20,
            Name = "Painting",
            WorkCenter = painting,
            SetupTimeMinutes = 5,
            RunTimeMinutes = 30
        });
        routing.Operations.Add(new RoutingOperation {
            Sequence = 30,
            Name = "Packaging",
            WorkCenter = packaging,
            SetupTimeMinutes = 3,
            RunTimeMinutes = 10
        });
        var startedAt = new DateTime(2026, 9, 6, 6, 0, 0, DateTimeKind.Utc);
        var order = new ProductionOrder {
            CompanyId = TestData.CompanyAId,
            Number = "PO-001",
            Product = product,
            Routing = routing,
            Quantity = 25,
            Status = ProductionOrderStatuses.InProgress,
            ReleasedAt = startedAt.AddHours(-1),
            StartedAt = startedAt.AddMinutes(-45)
        };
        order.Operations.Add(Operation(order, cutting, 10, "Cutting", ProductionOperationStatuses.Completed,
            startedAt.AddMinutes(-40), startedAt.AddMinutes(-20)));
        order.Operations.Add(new ProductionOrderOperation {
            CompanyId = TestData.CompanyAId,
            ProductionOrder = order,
            Sequence = 20,
            Name = "Painting",
            WorkCenter = painting,
            WorkCenterCode = painting.Code,
            WorkCenterName = painting.Name,
            Machine = machine,
            MachineCode = machine.Code,
            MachineName = machine.Name,
            SetupTimeMinutes = 5,
            RunTimeMinutes = 30,
            Status = ProductionOperationStatuses.InProgress,
            StartedAt = startedAt
        });
        order.Operations.Add(Operation(order, packaging, 30, "Packaging", ProductionOperationStatuses.Pending));
        dbContext.AddRange(product, cutting, painting, packaging, machine, routing, order);
        return new ExecutionSetup(startedAt);
    }

    private static ProductionOrderOperation Operation(
        ProductionOrder order,
        WorkCenter workCenter,
        int sequence,
        string name,
        string status,
        DateTime? startedAt = null,
        DateTime? completedAt = null) => new() {
            CompanyId = TestData.CompanyAId,
            ProductionOrder = order,
            Sequence = sequence,
            Name = name,
            WorkCenter = workCenter,
            WorkCenterCode = workCenter.Code,
            WorkCenterName = workCenter.Name,
            SetupTimeMinutes = 2,
            RunTimeMinutes = 10,
            Status = status,
            StartedAt = startedAt,
            CompletedAt = completedAt
        };

    private static float[] QueryVector() {
        var values = new float[DocumentEmbeddingConstraints.Dimensions];
        values[0] = 1f;
        return values;
    }

    private sealed class FixedEmbeddingClient : IEmbeddingClient {
        public Task<EmbeddingBatch> CreateAsync(
            IReadOnlyList<string> inputs,
            EmbeddingPurpose purpose,
            CancellationToken cancellationToken) =>
            Task.FromResult(new EmbeddingBatch("hybrid-test-model", [QueryVector()]));
    }

    private sealed record ExecutionSetup(DateTime StartedAt);
}
