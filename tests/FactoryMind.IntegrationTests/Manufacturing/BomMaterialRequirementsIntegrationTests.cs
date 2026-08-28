using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FactoryMind.Api.Endpoints;
using FactoryMind.Api.Routing;
using FactoryMind.Application.Features.Boms;
using FactoryMind.Application.Features.Inventories;
using FactoryMind.Application.Features.Materials;
using FactoryMind.Application.Features.Products;
using FactoryMind.Application.Features.ProductionOrders;
using FactoryMind.Application.Features.Warehouses;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.IntegrationTests.Infrastructure;
using FactoryMind.Shared.Contracts;

namespace FactoryMind.IntegrationTests.Manufacturing;

[Collection(IntegrationTestCollection.Name)]
public sealed class BomMaterialRequirementsIntegrationTests(PostgreSqlFixture fixture)
    : IntegrationTestBase(fixture) {
    [Fact]
    public async Task Product_requirements_report_shortage_then_sufficiency_across_warehouses() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var product = await CreateProductAsync(Client, "P001", "Industrial Table");
        var steel = await CreateMaterialAsync(Client, "STEEL", "Steel", "kg");
        var plastic = await CreateMaterialAsync(Client, "PLASTIC", "Plastic", "kg");
        var warehouseA = await CreateWarehouseAsync(Client, "WH-A", "Warehouse A");
        var warehouseB = await CreateWarehouseAsync(Client, "WH-B", "Warehouse B");
        await ReceiveAsync(Client, warehouseA.Id, steel.Id, 200m);
        await ReceiveAsync(Client, warehouseB.Id, steel.Id, 100m);
        await ReceiveAsync(Client, warehouseA.Id, plastic.Id, 40m);
        var bom = await CreateBomAsync(Client, product.Id, 1m, [
            new BomItemRequest(steel.Id, 2.5m, null),
            new BomItemRequest(plastic.Id, 0.5m, null)
        ]);
        await ActivateBomAsync(Client, product.Id, bom.Id);

        var shortage = await GetProductRequirementsAsync(Client, product.Id, 100m);

        Assert.False(shortage.CanProduce);
        var steelRequirement = shortage.Materials.Single(item => item.MaterialId == steel.Id);
        Assert.Equal(250m, steelRequirement.RequiredQuantity);
        Assert.Equal(300m, steelRequirement.AvailableQuantity);
        Assert.Equal(0m, steelRequirement.ShortageQuantity);
        var plasticRequirement = shortage.Materials.Single(item => item.MaterialId == plastic.Id);
        Assert.Equal(50m, plasticRequirement.RequiredQuantity);
        Assert.Equal(40m, plasticRequirement.AvailableQuantity);
        Assert.Equal(10m, plasticRequirement.ShortageQuantity);

        await ReceiveAsync(Client, warehouseB.Id, plastic.Id, 20m);
        var sufficient = await GetProductRequirementsAsync(Client, product.Id, 100m);

        Assert.True(sufficient.CanProduce);
        Assert.Equal(60m, sufficient.Materials.Single(item => item.MaterialId == plastic.Id).AvailableQuantity);
    }

    [Fact]
    public async Task Requirements_use_active_revision_and_activation_archives_previous_revision() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var product = await CreateProductAsync(Client, "P-REV", "Revision Product");
        var steel = await CreateMaterialAsync(Client, "STEEL-REV", "Revision Steel", "kg");
        var revisionOne = await CreateBomAsync(Client, product.Id, 1m, [
            new BomItemRequest(steel.Id, 1m, null)
        ]);
        await ActivateBomAsync(Client, product.Id, revisionOne.Id);
        var revisionTwo = await CreateBomAsync(Client, product.Id, 1m, [
            new BomItemRequest(steel.Id, 2m, null)
        ]);

        var beforeActivation = await GetProductRequirementsAsync(Client, product.Id, 10m);
        Assert.Equal(1, beforeActivation.BomRevision);
        Assert.Equal(10m, Assert.Single(beforeActivation.Materials).RequiredQuantity);

        await ActivateBomAsync(Client, product.Id, revisionTwo.Id);
        var afterActivation = await GetProductRequirementsAsync(Client, product.Id, 10m);
        Assert.Equal(2, afterActivation.BomRevision);
        Assert.Equal(20m, Assert.Single(afterActivation.Materials).RequiredQuantity);
        var boms = await GetBomsAsync(Client, product.Id);
        Assert.Single(boms, bom => bom.Status == BillOfMaterialStatuses.Active);
        Assert.Equal(BillOfMaterialStatuses.Archived, boms.Single(bom => bom.Revision == 1).Status);
    }

    [Fact]
    public async Task Cross_tenant_product_and_material_are_not_disclosed() {
        using var companyAClient = CreateClient();
        using var companyBClient = CreateClient();
        await LoginAsync(companyAClient, TestData.CompanyAAdminEmail);
        await LoginAsync(companyBClient, TestData.CompanyBAdminEmail);
        var productA = await CreateProductAsync(companyAClient, "P-A", "Company A Product");
        var materialA = await CreateMaterialAsync(companyAClient, "MAT-A", "Company A Material", "kg");
        var materialB = await CreateMaterialAsync(companyBClient, "MAT-B", "Company B Material", "kg");

        using var crossMaterialResponse = await companyAClient.PostAsJsonAsync(
            BomsRoute(productA.Id),
            new BomRequest(1m, [new BomItemRequest(materialB.Id, 1m, null)]));
        Assert.Equal(HttpStatusCode.NotFound, crossMaterialResponse.StatusCode);

        var bom = await CreateBomAsync(companyAClient, productA.Id, 1m, [
            new BomItemRequest(materialA.Id, 1m, null)
        ]);
        await ActivateBomAsync(companyAClient, productA.Id, bom.Id);

        using var listResponse = await companyBClient.GetAsync(BomsRoute(productA.Id));
        using var requirementResponse = await companyBClient.GetAsync(
            ProductRequirementsRoute(productA.Id, 10m));
        Assert.Equal(HttpStatusCode.NotFound, listResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, requirementResponse.StatusCode);
    }

    [Fact]
    public async Task Production_order_preview_uses_order_quantity_without_mutating_inventory() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var product = await CreateProductAsync(Client, "P-ORDER", "Order Product");
        var steel = await CreateMaterialAsync(Client, "MAT-ORDER", "Order Steel", "kg");
        var warehouse = await CreateWarehouseAsync(Client, "WH-ORDER", "Order Warehouse");
        await ReceiveAsync(Client, warehouse.Id, steel.Id, 100m);
        var bom = await CreateBomAsync(Client, product.Id, 1m, [
            new BomItemRequest(steel.Id, 2m, null)
        ]);
        await ActivateBomAsync(Client, product.Id, bom.Id);
        var order = await CreateProductionOrderAsync(Client, "PO-001", product.Id, 25m);
        var balancesBefore = await GetBalancesAsync(Client);
        var transactionCountBefore = await GetTransactionCountAsync(Client);

        var envelope = await Client.GetFromJsonAsync<ApiResponse<MaterialRequirementsResponse>>(
            ProductionOrderRequirementsRoute(order.Id));
        var requirements = envelope?.Data
            ?? throw new InvalidOperationException("Production order requirements response did not contain data.");

        Assert.Equal(25m, requirements.RequestedQuantity);
        Assert.Equal(50m, Assert.Single(requirements.Materials).RequiredQuantity);
        Assert.True(requirements.CanProduce);
        var balancesAfter = await GetBalancesAsync(Client);
        Assert.Equal(balancesBefore.Single().Quantity, balancesAfter.Single().Quantity);
        Assert.Equal(transactionCountBefore, await GetTransactionCountAsync(Client));
    }

    [Fact]
    public async Task Product_and_material_referenced_by_bom_cannot_be_deleted() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var product = await CreateProductAsync(Client, "P-HISTORY", "Historical Product");
        var material = await CreateMaterialAsync(Client, "MAT-HISTORY", "Historical Material", "kg");
        await CreateBomAsync(Client, product.Id, 1m, [new BomItemRequest(material.Id, 1m, null)]);

        using var productDelete = await Client.DeleteAsync(ProductByIdRoute(product.Id));
        using var materialDelete = await Client.DeleteAsync(MaterialByIdRoute(material.Id));

        Assert.Equal(HttpStatusCode.Conflict, productDelete.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, materialDelete.StatusCode);
    }

    private static string ProductsRoute => ApiRoutes.Products.Group + ApiRoutes.Products.Root;
    private static string ProductionOrdersRoute =>
        ApiRoutes.ProductionOrders.Group + ApiRoutes.ProductionOrders.Root;

    private static string ProductByIdRoute(Guid productId) => ApiRoutes.Products.Group +
        ApiRoutes.Products.ById.Replace("{productId:guid}", productId.ToString(), StringComparison.Ordinal);

    private static string MaterialByIdRoute(Guid materialId) => ApiRoutes.Materials.Group +
        ApiRoutes.Materials.ById.Replace("{materialId:guid}", materialId.ToString(), StringComparison.Ordinal);

    private static string BomsRoute(Guid productId) => ApiRoutes.Products.Group +
        ApiRoutes.Products.Boms.Replace("{productId:guid}", productId.ToString(), StringComparison.Ordinal);

    private static string ActivateBomRoute(Guid productId, Guid bomId) => ApiRoutes.Products.Group +
        ApiRoutes.Products.ActivateBom
            .Replace("{productId:guid}", productId.ToString(), StringComparison.Ordinal)
            .Replace("{bomId:guid}", bomId.ToString(), StringComparison.Ordinal);

    private static string ProductRequirementsRoute(Guid productId, decimal quantity) =>
        ApiRoutes.Products.Group + ApiRoutes.Products.MaterialRequirements
            .Replace("{productId:guid}", productId.ToString(), StringComparison.Ordinal) +
        $"?quantity={quantity.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    private static string ProductionOrderRequirementsRoute(Guid productionOrderId) =>
        ApiRoutes.ProductionOrders.Group + ApiRoutes.ProductionOrders.MaterialRequirements.Replace(
            "{productionOrderId:guid}",
            productionOrderId.ToString(),
            StringComparison.Ordinal);

    private static async Task<ProductResponse> CreateProductAsync(
        HttpClient client,
        string code,
        string name) {
        using var response = await client.PostAsJsonAsync(ProductsRoute, new ProductRequest(code, name));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<ProductResponse>>())!.Data!;
    }

    private static async Task<MaterialResponse> CreateMaterialAsync(
        HttpClient client,
        string code,
        string name,
        string unit) {
        using var response = await client.PostAsJsonAsync(MaterialsRoute, new MaterialRequest(code, name, unit));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<MaterialResponse>>())!.Data!;
    }

    private static async Task<WarehouseResponse> CreateWarehouseAsync(
        HttpClient client,
        string code,
        string name) {
        using var response = await client.PostAsJsonAsync(
            WarehousesRoute,
            new WarehouseCreateRequest(code, name, null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<WarehouseResponse>>())!.Data!;
    }

    private static async Task ReceiveAsync(
        HttpClient client,
        Guid warehouseId,
        Guid materialId,
        decimal quantity) {
        using var response = await client.PostAsJsonAsync(
            InventoryRoute(ApiRoutes.Inventories.Receive),
            new InventoryMovementRequest(warehouseId, materialId, quantity, null, null, null));
        response.EnsureSuccessStatusCode();
    }

    private static async Task<BomResponse> CreateBomAsync(
        HttpClient client,
        Guid productId,
        decimal outputQuantity,
        IReadOnlyList<BomItemRequest> items) {
        using var response = await client.PostAsJsonAsync(
            BomsRoute(productId),
            new BomRequest(outputQuantity, items));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<BomResponse>>())!.Data!;
    }

    private static async Task ActivateBomAsync(HttpClient client, Guid productId, Guid bomId) {
        using var response = await client.PostAsJsonAsync(ActivateBomRoute(productId, bomId), new { });
        response.EnsureSuccessStatusCode();
    }

    private static async Task<MaterialRequirementsResponse> GetProductRequirementsAsync(
        HttpClient client,
        Guid productId,
        decimal quantity) {
        var envelope = await client.GetFromJsonAsync<ApiResponse<MaterialRequirementsResponse>>(
            ProductRequirementsRoute(productId, quantity));
        return envelope?.Data
            ?? throw new InvalidOperationException("Product requirements response did not contain data.");
    }

    private static async Task<IReadOnlyList<BomResponse>> GetBomsAsync(HttpClient client, Guid productId) {
        var envelope = await client.GetFromJsonAsync<ApiResponse<IReadOnlyList<BomResponse>>>(
            BomsRoute(productId));
        return envelope?.Data ?? [];
    }

    private static async Task<ProductionOrderResponse> CreateProductionOrderAsync(
        HttpClient client,
        string number,
        Guid productId,
        decimal quantity) {
        using var response = await client.PostAsJsonAsync(
            ProductionOrdersRoute,
            new ProductionOrderRequest(number, productId, quantity, ProductionOrderStatuses.Planned));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<ProductionOrderResponse>>())!.Data!;
    }

    private static async Task<IReadOnlyList<InventoryBalanceResponse>> GetBalancesAsync(HttpClient client) {
        var envelope = await client.GetFromJsonAsync<ApiResponse<IReadOnlyList<InventoryBalanceResponse>>>(
            InventoriesRoute);
        return envelope?.Data ?? [];
    }

    private static async Task<int> GetTransactionCountAsync(HttpClient client) {
        using var response = await client.GetAsync(InventoryRoute(ApiRoutes.Inventories.Transactions));
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        return document.RootElement.GetProperty("data").GetProperty("totalCount").GetInt32();
    }
}
