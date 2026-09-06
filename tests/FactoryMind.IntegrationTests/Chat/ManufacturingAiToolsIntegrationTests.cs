using System.Text.Json;
using System.Runtime.CompilerServices;
using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.Chat;
using FactoryMind.Application.Features.Chat.Rag;
using FactoryMind.Application.Features.Chat.SendMessage;
using FactoryMind.Application.Features.Chat.Tools;
using FactoryMind.Application.Features.Knowledge;
using FactoryMind.Domain.Chat;
using FactoryMind.Domain.Knowledge;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Infrastructure.AI;
using FactoryMind.Infrastructure.Persistence;
using FactoryMind.Infrastructure.Persistence.Knowledge;
using FactoryMind.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pgvector;

namespace FactoryMind.IntegrationTests.Chat;

[Collection(IntegrationTestCollection.Name)]
public sealed class ManufacturingAiToolsIntegrationTests(PostgreSqlFixture fixture)
    : IntegrationTestBase(fixture) {
    [Fact]
    public async Task Machine_and_production_order_tools_are_tenant_scoped_and_execution_aware() {
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        var setup = SeedExecution(dbContext);
        await dbContext.SaveChangesAsync();
        var registry = scope.ServiceProvider.GetRequiredService<IManufacturingToolRegistry>();

        var machine = await ExecuteAsync(
            registry,
            TestData.CompanyAId,
            "get_machine_status",
            """{"code":"PAINT-02"}""");
        var machineEvidence = Assert.Single(machine.Records);
        Assert.Equal(setup.CompanyAMachineId, machineEvidence.EntityId);
        Assert.Contains("PAINT-02 - Company A Paint Machine", machineEvidence.Title);
        Assert.Contains("Status running", machineEvidence.Detail);
        Assert.Contains("Work Center PAINT - Company A Painting", machineEvidence.Detail);
        Assert.Contains("Production Order PO-001", machineEvidence.Detail);
        Assert.Contains("Painting", machineEvidence.Detail);
        Assert.DoesNotContain("Company B", machineEvidence.Detail);

        var order = await ExecuteAsync(
            registry,
            TestData.CompanyAId,
            "get_production_order_status",
            """{"number":"PO-001"}""");
        var orderEvidence = Assert.Single(order.Records);
        Assert.Equal(setup.CompanyAOrderId, orderEvidence.EntityId);
        Assert.Contains("current operation 20 Painting", orderEvidence.Detail);
        Assert.Contains("Work Center PAINT", orderEvidence.Detail);
        Assert.Contains("Machine PAINT-02", orderEvidence.Detail);
        Assert.Contains("next operation 30 Packaging", orderEvidence.Detail);
        Assert.Contains("operations completed 1/3", orderEvidence.Detail);
        Assert.DoesNotContain("delay", orderEvidence.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("blocked", orderEvidence.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Company B", orderEvidence.Detail);

        var list = await ExecuteAsync(
            registry,
            TestData.CompanyAId,
            "list_machines",
            """{"status":"running","workCenterCode":"PAINT","limit":20}""");
        Assert.Single(list.Records, record => record.EntityId == setup.CompanyAMachineId);
        Assert.DoesNotContain(list.Records, record => record.EntityId == setup.CompanyBMachineId);

        var workCenter = await ExecuteAsync(
            registry,
            TestData.CompanyAId,
            "get_work_center_status",
            """{"code":"PAINT"}""");
        var workCenterEvidence = Assert.Single(workCenter.Records);
        Assert.Contains("running=1", workCenterEvidence.Detail);
        Assert.Contains("PO PO-001, Painting, machine PAINT-02", workCenterEvidence.Detail);
        Assert.DoesNotContain("Company B", workCenterEvidence.Detail);

        var orders = await ExecuteAsync(
            registry,
            TestData.CompanyAId,
            "list_production_orders",
            """{"status":"in_progress","productCode":"PROD-PAINT","limit":10}""");
        var listedOrder = Assert.Single(orders.Records);
        Assert.Equal(setup.CompanyAOrderId, listedOrder.EntityId);
        Assert.Contains("current operation 20 Painting", listedOrder.Detail);
    }

    [Fact]
    public async Task Material_inventory_aggregates_active_tenant_warehouses_and_rejects_foreign_warehouse() {
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        var inventory = SeedInventory(dbContext);
        await dbContext.SaveChangesAsync();
        var registry = scope.ServiceProvider.GetRequiredService<IManufacturingToolRegistry>();

        var aggregate = await ExecuteAsync(
            registry,
            TestData.CompanyAId,
            "get_material_inventory",
            """{"materialCode":"RM-001"}""");
        var evidence = Assert.Single(aggregate.Records);
        Assert.Equal(inventory.CompanyAMaterialId, evidence.EntityId);
        Assert.Contains("WH-A1", evidence.Detail);
        Assert.Contains("WH-A2", evidence.Detail);
        Assert.Contains("total available quantity 15 kg", evidence.Detail);
        Assert.DoesNotContain("Foreign", evidence.Detail);

        var oneWarehouse = await ExecuteAsync(
            registry,
            TestData.CompanyAId,
            "get_material_inventory",
            """{"materialCode":"RM-001","warehouseCode":"WH-A1"}""");
        var oneEvidence = Assert.Single(oneWarehouse.Records);
        Assert.Contains("WH-A1", oneEvidence.Detail);
        Assert.DoesNotContain("WH-A2", oneEvidence.Detail);
        Assert.Contains("total available quantity 8 kg", oneEvidence.Detail);

        var foreign = await ExecuteAsync(
            registry,
            TestData.CompanyAId,
            "get_material_inventory",
            """{"materialCode":"RM-001","warehouseCode":"FOREIGN-WH"}""");
        Assert.Equal(ToolExecutionStatuses.NotFound, foreign.Status);
        Assert.Empty(foreign.Records);
    }

    [Fact]
    public async Task Material_readiness_reuses_production_formula_and_is_not_applicable_in_progress() {
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        SeedReadiness(dbContext);
        await dbContext.SaveChangesAsync();
        var registry = scope.ServiceProvider.GetRequiredService<IManufacturingToolRegistry>();

        var planned = await ExecuteAsync(
            registry,
            TestData.CompanyAId,
            "get_production_order_material_readiness",
            """{"number":"PO-READY"}""");
        Assert.Equal(ToolExecutionStatuses.Success, planned.Status);
        var summary = Assert.Single(planned.Records, record =>
            record.EntityType == "production_order_material_readiness");
        Assert.Contains("allMaterialsSufficient=False", summary.Detail);
        Assert.Contains("excludes reservations, competing orders, future receipts, scheduling, and transfer lead time", summary.Detail);
        var materialA = Assert.Single(planned.Records, record => record.Title.Contains("MAT-A", StringComparison.Ordinal));
        Assert.Contains("required quantity 5", materialA.Detail);
        Assert.Contains("available quantity 8", materialA.Detail);
        Assert.Contains("shortage quantity 0", materialA.Detail);
        Assert.Contains("isSufficient=True", materialA.Detail);
        var materialB = Assert.Single(planned.Records, record => record.Title.Contains("MAT-B", StringComparison.Ordinal));
        Assert.Contains("required quantity 10", materialB.Detail);
        Assert.Contains("available quantity 7", materialB.Detail);
        Assert.Contains("shortage quantity 3", materialB.Detail);
        Assert.Contains("isSufficient=False", materialB.Detail);

        var inProgress = await ExecuteAsync(
            registry,
            TestData.CompanyAId,
            "get_production_order_material_readiness",
            """{"number":"PO-CONSUMED"}""");
        Assert.Equal(ToolExecutionStatuses.NotApplicable, inProgress.Status);
        var notApplicable = Assert.Single(inProgress.Records);
        Assert.Contains("materialReadinessApplicable=false", notApplicable.Detail);
        Assert.Contains("already in progress", notApplicable.Detail);
        Assert.DoesNotContain("shortage", notApplicable.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Injection_like_identifier_is_literal_and_all_registered_tools_are_read_only() {
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        var setup = SeedExecution(dbContext);
        SeedInventory(dbContext);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var registry = scope.ServiceProvider.GetRequiredService<IManufacturingToolRegistry>();

        var injection = await ExecuteAsync(
            registry,
            TestData.CompanyAId,
            "get_production_order_status",
            """{"number":"PO-001' OR 1=1 --"}""");
        Assert.Equal(ToolExecutionStatuses.NotFound, injection.Status);

        var calls = new Dictionary<string, string> {
            ["get_production_order_status"] = """{"number":"PO-001"}""",
            ["get_machine_status"] = """{"code":"PAINT-02"}""",
            ["list_machines"] = """{"limit":10}""",
            ["get_work_center_status"] = """{"code":"PAINT"}""",
            ["get_material_inventory"] = """{"materialCode":"RM-001"}""",
            ["get_production_order_material_readiness"] = """{"number":"PO-001"}""",
            ["list_production_orders"] = """{"limit":10}"""
        };
        foreach (var call in calls) {
            await ExecuteAsync(registry, TestData.CompanyAId, call.Key, call.Value);
        }

        Assert.False(dbContext.ChangeTracker.HasChanges());
        Assert.Equal(MachineStatuses.Running, await dbContext.Machines.AsNoTracking()
            .Where(machine => machine.Id == setup.CompanyAMachineId)
            .Select(machine => machine.Status)
            .SingleAsync());
        Assert.Equal(ProductionOrderStatuses.InProgress, await dbContext.ProductionOrders.AsNoTracking()
            .Where(order => order.Id == setup.CompanyAOrderId)
            .Select(order => order.Status)
            .SingleAsync());
        Assert.Equal(ProductionOperationStatuses.InProgress, await dbContext.ProductionOrderOperations.AsNoTracking()
            .Where(operation => operation.ProductionOrderId == setup.CompanyAOrderId && operation.Sequence == 20)
            .Select(operation => operation.Status)
            .SingleAsync());
        Assert.Equal(8m, await dbContext.InventoryBalances.AsNoTracking()
            .Where(balance => balance.CompanyId == TestData.CompanyAId
                && balance.Material!.Code == "RM-001"
                && balance.Warehouse!.Code == "WH-A1")
            .Select(balance => balance.Quantity)
            .SingleAsync());
    }

    [Fact]
    public async Task End_to_end_chat_uses_tool_evidence_in_unchanged_stream_and_persists_cited_business_evidence() {
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        SeedExecution(dbContext);
        var userId = await dbContext.Users
            .Where(user => user.CompanyId == TestData.CompanyAId)
            .Select(user => user.Id)
            .FirstAsync();
        var conversation = new Conversation {
            CompanyId = TestData.CompanyAId,
            UserId = userId
        };
        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync();
        var planner = new FixedPlanner(new AiToolCall(
            "get_machine_status",
            Parse("""{"code":"PAINT-02"}""")));
        var finalClient = new CapturingChatClient(
            "PAINT-02 is executing Painting for PO-001 [B1].");
        var router = new IntentRouter();
        var handler = new SendMessageCommandHandler(
            scope.ServiceProvider.GetRequiredService<IConversationRepository>(),
            finalClient,
            new ChatContextBuilder(
                router,
                scope.ServiceProvider.GetRequiredService<IKnowledgeContextBuilder>(),
                scope.ServiceProvider.GetRequiredService<IBusinessContextBuilder>()),
            new AiToolOrchestrator(
                router,
                planner,
                scope.ServiceProvider.GetRequiredService<IManufacturingToolRegistry>()),
            new FixedCurrentUser(userId));

        var result = await handler.Handle(
            new SendMessageCommand(conversation.Id, "Máy PAINT-02 đang làm lệnh nào?"),
            CancellationToken.None);
        var updates = new List<ChatStreamUpdate>();
        await foreach (var update in result.Value!.Updates) {
            updates.Add(update);
        }

        Assert.Equal(1, planner.CallCount);
        Assert.Contains("[B1] machine: PAINT-02", finalClient.Prompt[0].Content);
        Assert.DoesNotContain("functionDeclarations", finalClient.Prompt[0].Content, StringComparison.Ordinal);
        Assert.Contains(updates, update => update is ChatTokenUpdate);
        var evidenceUpdate = Assert.Single(updates.OfType<ChatBusinessEvidenceUpdate>());
        var cited = Assert.Single(evidenceUpdate.BusinessEvidence);
        Assert.Equal("machine", cited.EntityType);
        Assert.Contains("PAINT-02", cited.Title);

        dbContext.ChangeTracker.Clear();
        var assistant = await dbContext.Messages.AsNoTracking()
            .Include(message => message.BusinessEvidence)
            .SingleAsync(message => message.ConversationId == conversation.Id && message.Role == ChatRoles.Assistant);
        var persisted = Assert.Single(assistant.BusinessEvidence);
        Assert.Equal(1, persisted.ReferenceNumber);
        Assert.Equal(cited.EntityId, persisted.EntityId);
    }

    [Fact]
    public async Task Hybrid_tool_and_knowledge_context_contains_business_and_sop_evidence() {
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
            Content = "Painting SOP requires checking coating thickness before packaging."
        };
        dbContext.AddRange(document, chunk);
        dbContext.DocumentEmbeddings.Add(new DocumentEmbeddingRecord {
            DocumentChunkId = chunk.Id,
            CompanyId = TestData.CompanyAId,
            Model = "tool-hybrid-test",
            Dimensions = DocumentEmbeddingConstraints.Dimensions,
            Embedding = new Vector(QueryVector())
        });
        await dbContext.SaveChangesAsync();
        var question = "PO-001 đang ở công đoạn nào và theo SOP phải kiểm tra gì?";
        var router = new IntentRouter();
        var toolRecords = await new AiToolOrchestrator(
            router,
            new FixedPlanner(new AiToolCall(
                "get_production_order_status",
                Parse("""{"number":"PO-001"}"""))),
            scope.ServiceProvider.GetRequiredService<IManufacturingToolRegistry>()).CollectAsync(
                TestData.CompanyAId,
                question,
                [],
                CancellationToken.None);
        var knowledgeBuilder = new KnowledgeContextBuilder(new KnowledgeRetriever(
            new FixedEmbeddingClient(),
            scope.ServiceProvider.GetRequiredService<IKnowledgeSearchRepository>()));
        var context = await new ChatContextBuilder(
            router,
            knowledgeBuilder,
            scope.ServiceProvider.GetRequiredService<IBusinessContextBuilder>()).BuildAsync(
                TestData.CompanyAId,
                question,
                toolRecords,
                CancellationToken.None);

        Assert.Contains("[B1]", context.Prompt);
        Assert.Contains("current operation 20 Painting", context.Prompt);
        Assert.Contains("[S1]", context.Prompt);
        Assert.Contains("coating thickness", context.Prompt);
        Assert.NotEmpty(context.BusinessEvidence);
        Assert.NotEmpty(context.Sources);
    }

    private static async Task<ToolExecutionResult> ExecuteAsync(
        IManufacturingToolRegistry registry,
        Guid companyId,
        string name,
        string arguments) =>
        await registry.ExecuteAsync(
            companyId,
            new AiToolCall(name, Parse(arguments)),
            CancellationToken.None);

    private static JsonElement Parse(string json) {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static ExecutionSetup SeedExecution(FactoryMindDbContext dbContext) {
        var productA = new Product {
            CompanyId = TestData.CompanyAId,
            Code = "PROD-PAINT",
            Name = "Company A Product"
        };
        var productB = new Product {
            CompanyId = TestData.CompanyBId,
            Code = "PROD-PAINT",
            Name = "Company B Product"
        };
        var workCenterA = new WorkCenter {
            CompanyId = TestData.CompanyAId,
            Code = "PAINT",
            Name = "Company A Painting"
        };
        var workCenterB = new WorkCenter {
            CompanyId = TestData.CompanyBId,
            Code = "PAINT",
            Name = "Company B Secret Painting"
        };
        var machineA = new Machine {
            CompanyId = TestData.CompanyAId,
            Code = "PAINT-02",
            Name = "Company A Paint Machine",
            Status = MachineStatuses.Running,
            WorkCenter = workCenterA
        };
        var machineB = new Machine {
            CompanyId = TestData.CompanyBId,
            Code = "PAINT-02",
            Name = "Company B Secret Machine",
            Status = MachineStatuses.Running,
            WorkCenter = workCenterB
        };
        var routingA = new Routing {
            CompanyId = TestData.CompanyAId,
            Product = productA,
            Revision = 3,
            Status = RoutingStatuses.Active
        };
        var routingB = new Routing {
            CompanyId = TestData.CompanyBId,
            Product = productB,
            Revision = 9,
            Status = RoutingStatuses.Active
        };
        var orderA = new ProductionOrder {
            CompanyId = TestData.CompanyAId,
            Number = "PO-001",
            Product = productA,
            Routing = routingA,
            Quantity = 25,
            Status = ProductionOrderStatuses.InProgress,
            ReleasedAt = DateTime.UtcNow.AddHours(-2),
            StartedAt = DateTime.UtcNow.AddHours(-1)
        };
        var orderB = new ProductionOrder {
            CompanyId = TestData.CompanyBId,
            Number = "PO-001",
            Product = productB,
            Routing = routingB,
            Quantity = 999,
            Status = ProductionOrderStatuses.InProgress
        };
        orderA.Operations.Add(Operation(orderA, workCenterA, 10, "Cutting", ProductionOperationStatuses.Completed));
        orderA.Operations.Add(Operation(
            orderA,
            workCenterA,
            20,
            "Painting",
            ProductionOperationStatuses.InProgress,
            machineA));
        orderA.Operations.Add(Operation(orderA, workCenterA, 30, "Packaging", ProductionOperationStatuses.Pending));
        orderB.Operations.Add(Operation(
            orderB,
            workCenterB,
            20,
            "Secret Operation",
            ProductionOperationStatuses.InProgress,
            machineB));
        dbContext.AddRange(productA, productB, workCenterA, workCenterB, machineA, machineB, routingA, routingB, orderA, orderB);
        return new ExecutionSetup(orderA.Id, machineA.Id, machineB.Id);
    }

    private static ProductionOrderOperation Operation(
        ProductionOrder order,
        WorkCenter workCenter,
        int sequence,
        string name,
        string status,
        Machine? machine = null) => new() {
            CompanyId = order.CompanyId,
            ProductionOrder = order,
            Sequence = sequence,
            Name = name,
            WorkCenter = workCenter,
            WorkCenterCode = workCenter.Code,
            WorkCenterName = workCenter.Name,
            Machine = machine,
            MachineCode = machine?.Code,
            MachineName = machine?.Name,
            Status = status,
            StartedAt = status == ProductionOperationStatuses.InProgress ? DateTime.UtcNow.AddMinutes(-30) : null,
            CompletedAt = status == ProductionOperationStatuses.Completed ? DateTime.UtcNow.AddMinutes(-40) : null
        };

    private static InventorySetup SeedInventory(FactoryMindDbContext dbContext) {
        var materialA = new Material {
            CompanyId = TestData.CompanyAId,
            Code = "RM-001",
            Name = "Company A Resin",
            Unit = "kg"
        };
        var materialB = new Material {
            CompanyId = TestData.CompanyBId,
            Code = "RM-001",
            Name = "Foreign Secret Resin",
            Unit = "kg"
        };
        var warehouseA1 = new Warehouse {
            CompanyId = TestData.CompanyAId,
            Code = "WH-A1",
            Name = "Warehouse A1",
            IsActive = true
        };
        var warehouseA2 = new Warehouse {
            CompanyId = TestData.CompanyAId,
            Code = "WH-A2",
            Name = "Warehouse A2",
            IsActive = true
        };
        var warehouseB = new Warehouse {
            CompanyId = TestData.CompanyBId,
            Code = "FOREIGN-WH",
            Name = "Foreign Secret Warehouse",
            IsActive = true
        };
        dbContext.AddRange(materialA, materialB, warehouseA1, warehouseA2, warehouseB);
        dbContext.InventoryBalances.AddRange(
            new InventoryBalance {
                CompanyId = TestData.CompanyAId,
                Material = materialA,
                Warehouse = warehouseA1,
                Quantity = 8
            },
            new InventoryBalance {
                CompanyId = TestData.CompanyAId,
                Material = materialA,
                Warehouse = warehouseA2,
                Quantity = 7
            },
            new InventoryBalance {
                CompanyId = TestData.CompanyBId,
                Material = materialB,
                Warehouse = warehouseB,
                Quantity = 999
            });
        return new InventorySetup(materialA.Id);
    }

    private static void SeedReadiness(FactoryMindDbContext dbContext) {
        var product = new Product {
            CompanyId = TestData.CompanyAId,
            Code = "READY-PROD",
            Name = "Readiness Product"
        };
        var materialA = new Material {
            CompanyId = TestData.CompanyAId,
            Code = "MAT-A",
            Name = "Material A",
            Unit = "kg"
        };
        var materialB = new Material {
            CompanyId = TestData.CompanyAId,
            Code = "MAT-B",
            Name = "Material B",
            Unit = "kg"
        };
        var bom = new BillOfMaterial {
            CompanyId = TestData.CompanyAId,
            Product = product,
            Revision = 1,
            OutputQuantity = 1,
            Status = BillOfMaterialStatuses.Active
        };
        bom.Items.Add(new BomItem { Material = materialA, Quantity = 5 });
        bom.Items.Add(new BomItem { Material = materialB, Quantity = 10 });
        var warehouse = new Warehouse {
            CompanyId = TestData.CompanyAId,
            Code = "READY-WH",
            Name = "Readiness Warehouse",
            IsActive = true
        };
        dbContext.AddRange(
            product,
            materialA,
            materialB,
            bom,
            warehouse,
            new ProductionOrder {
                CompanyId = TestData.CompanyAId,
                Number = "PO-READY",
                Product = product,
                Quantity = 1,
                Status = ProductionOrderStatuses.Planned
            },
            new ProductionOrder {
                CompanyId = TestData.CompanyAId,
                Number = "PO-CONSUMED",
                Product = product,
                BillOfMaterial = bom,
                Quantity = 1,
                Status = ProductionOrderStatuses.InProgress
            },
            new InventoryBalance {
                CompanyId = TestData.CompanyAId,
                Material = materialA,
                Warehouse = warehouse,
                Quantity = 8
            },
            new InventoryBalance {
                CompanyId = TestData.CompanyAId,
                Material = materialB,
                Warehouse = warehouse,
                Quantity = 7
            });
    }

    private sealed class FixedPlanner(AiToolCall call) : IAiToolPlanner {
        public int CallCount { get; private set; }

        public Task<AiToolPlan> PlanAsync(
            IReadOnlyList<ChatPromptMessage> messages,
            IReadOnlyList<AiToolDefinition> tools,
            CancellationToken cancellationToken) {
            CallCount++;
            return Task.FromResult(new AiToolPlan([call]));
        }
    }

    private sealed class CapturingChatClient(string answer) : IChatCompletionClient {
        public IReadOnlyList<ChatPromptMessage> Prompt { get; private set; } = [];

        public async IAsyncEnumerable<string> StreamAsync(
            IReadOnlyList<ChatPromptMessage> messages,
            [EnumeratorCancellation] CancellationToken cancellationToken) {
            Prompt = messages;
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return answer;
        }
    }

    private sealed class FixedCurrentUser(Guid userId) : ICurrentUser {
        public Guid UserId => userId;
        public Guid CompanyId => TestData.CompanyAId;
        public string Role => "User";
    }

    private sealed class FixedEmbeddingClient : IEmbeddingClient {
        public Task<EmbeddingBatch> CreateAsync(
            IReadOnlyList<string> inputs,
            EmbeddingPurpose purpose,
            CancellationToken cancellationToken) =>
            Task.FromResult(new EmbeddingBatch("tool-hybrid-test", [QueryVector()]));
    }

    private static float[] QueryVector() {
        var vector = new float[DocumentEmbeddingConstraints.Dimensions];
        vector[0] = 1f;
        return vector;
    }

    private sealed record ExecutionSetup(
        Guid CompanyAOrderId,
        Guid CompanyAMachineId,
        Guid CompanyBMachineId);
    private sealed record InventorySetup(Guid CompanyAMaterialId);
}
