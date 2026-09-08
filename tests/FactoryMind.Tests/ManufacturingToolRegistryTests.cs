using System.Text.Json;
using FactoryMind.Application.Features.Boms;
using FactoryMind.Application.Features.Chat;
using FactoryMind.Application.Features.ProductionOrders;
using FactoryMind.Infrastructure.AI;
using FactoryMind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactoryMind.Tests;

public sealed class ManufacturingToolRegistryTests {
    private static readonly string[] ApprovedNames = [
        "get_production_order_status",
        "get_machine_status",
        "list_machines",
        "get_work_center_status",
        "get_material_inventory",
        "get_production_order_material_readiness",
        "list_production_orders"
    ];

    [Fact]
    public void Registry_contains_only_the_seven_approved_read_only_tools() {
        using var dbContext = CreateDbContext();
        var registry = CreateRegistry(dbContext);

        Assert.Equal(ApprovedNames, registry.Definitions.Select(definition => definition.Name));
        Assert.All(registry.Definitions, definition => {
            Assert.Contains("Read-only", definition.Description, StringComparison.OrdinalIgnoreCase);
            Assert.False(definition.Parameters.GetProperty("additionalProperties").GetBoolean());
            Assert.DoesNotContain("companyId", definition.Parameters.GetRawText(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("tenantId", definition.Parameters.GetRawText(), StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Unknown_tool_never_executes_and_returns_controlled_outcome() {
        await using var dbContext = CreateDbContext();
        var registry = CreateRegistry(dbContext);

        var result = await registry.ExecuteAsync(
            Guid.NewGuid(),
            new AiToolCall("delete_machine", Parse("""{"code":"CNC-02"}""")),
            CancellationToken.None);

        Assert.Equal(ToolExecutionStatuses.UnknownTool, result.Status);
        Assert.Empty(result.Records);
    }

    [Theory]
    [InlineData("execute_sql")]
    [InlineData("query_database")]
    [InlineData("update_machine")]
    [InlineData("start_operation")]
    [InlineData("complete_operation")]
    [InlineData("release_order")]
    [InlineData("adjust_inventory")]
    public async Task Mutation_dynamic_and_internal_tool_names_are_rejected(string toolName) {
        await using var dbContext = CreateDbContext();
        var registry = CreateRegistry(dbContext);

        var result = await registry.ExecuteAsync(
            Guid.NewGuid(),
            new AiToolCall(toolName, Parse("{}")),
            CancellationToken.None);

        Assert.Equal(ToolExecutionStatuses.UnknownTool, result.Status);
        Assert.Empty(result.Records);
    }

    [Theory]
    [InlineData("get_production_order_status", "{\"number\":\"PO-001\",\"companyId\":\"foreign\"}")]
    [InlineData("get_machine_status", "{\"code\":\"CNC-02\",\"companyId\":\"foreign\"}")]
    [InlineData("list_machines", "{\"tenantId\":\"foreign\"}")]
    [InlineData("get_work_center_status", "{\"code\":\"PAINT\",\"userId\":\"foreign\"}")]
    [InlineData("get_material_inventory", "{\"materialCode\":\"RM-001\",\"companyId\":\"foreign\"}")]
    [InlineData("get_production_order_material_readiness", "{\"number\":\"PO-001\",\"tenantId\":\"foreign\"}")]
    [InlineData("list_production_orders", "{\"companyId\":\"foreign\"}")]
    public async Task Every_registered_tool_rejects_model_supplied_identity_fields(string toolName, string json) {
        await using var dbContext = CreateDbContext();
        var registry = CreateRegistry(dbContext);

        var result = await registry.ExecuteAsync(
            Guid.NewGuid(),
            new AiToolCall(toolName, Parse(json)),
            CancellationToken.None);

        Assert.Equal(ToolExecutionStatuses.InvalidArguments, result.Status);
        Assert.Empty(result.Records);
    }

    [Theory]
    [InlineData("{\"code\":123}")]
    [InlineData("{\"code\":\"CNC-02\",\"companyId\":\"foreign-company\"}")]
    [InlineData("{\"code\":\"CNC-02\",\"tenantId\":\"foreign-tenant\"}")]
    public async Task Machine_tool_strictly_rejects_malformed_or_identity_arguments(string json) {
        await using var dbContext = CreateDbContext();
        var tool = new GetMachineStatusTool(dbContext);

        var result = await tool.ExecuteAsync(Guid.NewGuid(), Parse(json), CancellationToken.None);

        Assert.Equal(ToolExecutionStatuses.InvalidArguments, result.Status);
    }

    [Theory]
    [InlineData("{\"limit\":0}")]
    [InlineData("{\"limit\":21}")]
    [InlineData("{\"status\":\"broken\"}")]
    [InlineData("{\"limit\":10,\"where\":\"1=1\"}")]
    public async Task List_machine_tool_rejects_unbounded_invalid_or_dynamic_filter_arguments(string json) {
        await using var dbContext = CreateDbContext();
        var tool = new ListMachinesTool(dbContext);

        var result = await tool.ExecuteAsync(Guid.NewGuid(), Parse(json), CancellationToken.None);

        Assert.Equal(ToolExecutionStatuses.InvalidArguments, result.Status);
    }

    [Theory]
    [InlineData("{\"priority\":\"critical\"}")]
    [InlineData("{\"deliveryStatus\":\"likely_late\"}")]
    [InlineData("{\"status\":\"queued\"}")]
    [InlineData("{\"limit\":21}")]
    [InlineData("{\"sortBy\":\"DueDate; DROP TABLE production_orders\"}")]
    public async Task List_production_order_tool_rejects_unbounded_or_unapproved_planning_filters(string json) {
        await using var dbContext = CreateDbContext();
        var tool = new ListProductionOrdersTool(dbContext, RiskCalculator());

        var result = await tool.ExecuteAsync(Guid.NewGuid(), Parse(json), CancellationToken.None);

        Assert.Equal(ToolExecutionStatuses.InvalidArguments, result.Status);
        Assert.Empty(result.Records);
    }

    [Fact]
    public void Production_order_list_schema_exposes_only_bounded_planning_filters() {
        using var dbContext = CreateDbContext();
        var definition = CreateRegistry(dbContext).Definitions.Single(item =>
            item.Name == "list_production_orders");
        var properties = definition.Parameters.GetProperty("properties");

        Assert.Contains("active", properties.GetProperty("status").GetProperty("enum")
            .EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(4, properties.GetProperty("priority").GetProperty("enum").GetArrayLength());
        Assert.Equal(7, properties.GetProperty("deliveryStatus").GetProperty("enum").GetArrayLength());
        Assert.Equal(20, properties.GetProperty("limit").GetProperty("maximum").GetInt32());
    }

    private static ManufacturingToolRegistry CreateRegistry(FactoryMindDbContext dbContext) => new(
        new GetProductionOrderStatusTool(dbContext, RiskCalculator()),
        new GetMachineStatusTool(dbContext),
        new ListMachinesTool(dbContext),
        new GetWorkCenterStatusTool(dbContext),
        new GetMaterialInventoryTool(dbContext),
        new GetProductionOrderMaterialReadinessTool(dbContext, new MaterialRequirementCalculator()),
        new ListProductionOrdersTool(dbContext, RiskCalculator()));

    private static IProductionOrderDeliveryRiskCalculator RiskCalculator() =>
        new ProductionOrderDeliveryRiskCalculator(TimeProvider.System, new PlanningSettings());

    private static FactoryMindDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<FactoryMindDbContext>()
            .UseNpgsql("Host=localhost;Database=unused")
            .Options);

    private static JsonElement Parse(string json) {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
