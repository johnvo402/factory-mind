using System.Text.Json;
using FactoryMind.Application.Features.Boms;
using FactoryMind.Application.Features.Chat;
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

    private static ManufacturingToolRegistry CreateRegistry(FactoryMindDbContext dbContext) => new(
        new GetProductionOrderStatusTool(dbContext),
        new GetMachineStatusTool(dbContext),
        new ListMachinesTool(dbContext),
        new GetWorkCenterStatusTool(dbContext),
        new GetMaterialInventoryTool(dbContext),
        new GetProductionOrderMaterialReadinessTool(dbContext, new MaterialRequirementCalculator()),
        new ListProductionOrdersTool(dbContext));

    private static FactoryMindDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<FactoryMindDbContext>()
            .UseNpgsql("Host=localhost;Database=unused")
            .Options);

    private static JsonElement Parse(string json) {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
