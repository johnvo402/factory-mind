using FactoryMind.Api.Routing;

namespace FactoryMind.Tests;

public sealed class ApiRoutesTests {
    [Fact]
    public void Business_route_groups_keep_the_existing_API_prefix() {
        Assert.Equal("/api", ApiRoutes.Base);
        Assert.Equal("/api/auth", ApiRoutes.Auth.Group);
        Assert.Equal("/api/conversations", ApiRoutes.Conversations.Group);
        Assert.Equal("/api/documents", ApiRoutes.Documents.Group);
        Assert.Equal("/api/dashboard", ApiRoutes.Dashboard.Group);
        Assert.Equal("/api/imports/excel", ApiRoutes.ExcelImports.Group);
        Assert.Equal("/api/settings", ApiRoutes.Settings.Group);
        Assert.Equal("/api/knowledge", ApiRoutes.Knowledge.Group);
        Assert.Equal("/api/inventories", ApiRoutes.Inventories.Group);
        Assert.Equal("/api/machines", ApiRoutes.Machines.Group);
        Assert.Equal("/api/materials", ApiRoutes.Materials.Group);
        Assert.Equal("/api/products", ApiRoutes.Products.Group);
        Assert.Equal("/api/production-orders", ApiRoutes.ProductionOrders.Group);
    }

    [Fact]
    public void Child_routes_are_defined_once_and_compose_with_their_group() {
        Assert.Equal("/api/auth/login", ApiRoutes.Auth.Group + ApiRoutes.Auth.Login);
        Assert.Equal(
            "/api/conversations/{conversationId:guid}/messages/stream",
            ApiRoutes.Conversations.Group + ApiRoutes.Conversations.StreamMessage);
        Assert.Equal(
            "/api/documents/{documentId:guid}/process",
            ApiRoutes.Documents.Group + ApiRoutes.Documents.Process);
        Assert.Equal(
            "/api/documents/reindex",
            ApiRoutes.Documents.Group + ApiRoutes.Documents.Reindex);
        Assert.Equal(
            "/api/dashboard/summary",
            ApiRoutes.Dashboard.Group + ApiRoutes.Dashboard.Summary);
        Assert.Equal(
            "/api/imports/excel/preview",
            ApiRoutes.ExcelImports.Group + ApiRoutes.ExcelImports.Preview);
        Assert.Equal(
            "/api/imports/excel/import",
            ApiRoutes.ExcelImports.Group + ApiRoutes.ExcelImports.Import);
        Assert.Equal("/api/settings/company", ApiRoutes.Settings.Group + ApiRoutes.Settings.Company);
        Assert.Equal("/api/settings/users", ApiRoutes.Settings.Group + ApiRoutes.Settings.Users);
        Assert.Equal(
            "/api/settings/users/{userId:guid}",
            ApiRoutes.Settings.Group + ApiRoutes.Settings.UserById);
        Assert.Equal("/api/settings/ai", ApiRoutes.Settings.Group + ApiRoutes.Settings.Ai);
        Assert.Equal("/api/knowledge/search", ApiRoutes.Knowledge.Group + ApiRoutes.Knowledge.Search);
        Assert.Equal(
            "/api/machines/{machineId:guid}",
            ApiRoutes.Machines.Group + ApiRoutes.Machines.ById);
        Assert.Equal(
            "/api/materials/{materialId:guid}",
            ApiRoutes.Materials.Group + ApiRoutes.Materials.ById);
        Assert.Equal(
            "/api/products/{productId:guid}",
            ApiRoutes.Products.Group + ApiRoutes.Products.ById);
        Assert.Equal(
            "/api/inventories/{inventoryId:guid}",
            ApiRoutes.Inventories.Group + ApiRoutes.Inventories.ById);
        Assert.Equal(
            "/api/production-orders/{productionOrderId:guid}",
            ApiRoutes.ProductionOrders.Group + ApiRoutes.ProductionOrders.ById);
    }
}
