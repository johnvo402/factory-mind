using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FactoryMind.Api.Endpoints;
using FactoryMind.Api.Routing;
using FactoryMind.Application.Features.Boms;
using FactoryMind.Application.Features.Inventories;
using FactoryMind.Application.Features.Materials;
using FactoryMind.Application.Features.ProductInventories;
using FactoryMind.Application.Features.Products;
using FactoryMind.Application.Features.ProductionOrders;
using FactoryMind.Application.Features.Routings;
using FactoryMind.Application.Features.Warehouses;
using FactoryMind.Application.Features.WorkCenters;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Infrastructure.Persistence;
using FactoryMind.IntegrationTests.Infrastructure;
using FactoryMind.Shared.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryMind.IntegrationTests.Manufacturing;

[Collection(IntegrationTestCollection.Name)]
public sealed class ProductionCompletionIntegrationTests(PostgreSqlFixture fixture)
    : IntegrationTestBase(fixture) {
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task Complete_records_finished_goods_once_without_mutating_raw_materials() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var scenario = await CreateScenarioAsync(Client, "HAPPY", 200m, 2m);
        var order = await PrepareStartedOrderAsync(Client, scenario, "PO-001", 50m);
        var rawBalanceBefore = await GetRawBalanceAsync(Client, scenario);
        var rawHistoryBefore = await GetRawHistoryAsync(Client);
        var consumptionBefore = Assert.Single(rawHistoryBefore.Items, item =>
            item.ReferenceId == order.Id && item.Type == InventoryTransactionType.ProductionConsume);

        Assert.Equal(ProductionOrderStatuses.InProgress, order.Status);
        Assert.Equal(100m, rawBalanceBefore.Quantity);
        Assert.Equal(100m, consumptionBefore.Quantity);

        var completed = await CompleteAsync(Client, order.Id, scenario.FinishedGoodsWarehouse.Id);

        Assert.Equal(ProductionOrderStatuses.Completed, completed.Status);
        Assert.NotNull(completed.CompletedAt);
        Assert.Equal(100m, (await GetRawBalanceAsync(Client, scenario)).Quantity);
        var rawHistoryAfter = await GetRawHistoryAsync(Client);
        Assert.Equal(rawHistoryBefore.TotalCount, rawHistoryAfter.TotalCount);
        Assert.Single(rawHistoryAfter.Items, item =>
            item.ReferenceId == order.Id && item.Type == InventoryTransactionType.ProductionConsume);

        var balance = Assert.Single(await GetProductBalancesAsync(Client));
        Assert.Equal(scenario.Product.Id, balance.ProductId);
        Assert.Equal(scenario.FinishedGoodsWarehouse.Id, balance.WarehouseId);
        Assert.Equal(50m, balance.Quantity);
        var output = Assert.Single((await GetProductHistoryAsync(Client)).Items);
        Assert.Equal(ProductInventoryTransactionType.ProductionOutput, output.Type);
        Assert.Equal(50m, output.Quantity);
        Assert.Equal(50m, output.SignedQuantity);
        Assert.Equal("ProductionOrder", output.ReferenceType);
        Assert.Equal(order.Id, output.ReferenceId);
    }

    [Fact]
    public async Task Double_complete_returns_conflict_and_preserves_first_result() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var scenario = await CreateScenarioAsync(Client, "DOUBLE", 20m, 1m);
        var order = await PrepareStartedOrderAsync(Client, scenario, "PO-DOUBLE", 10m);
        var first = await CompleteAsync(Client, order.Id, scenario.FinishedGoodsWarehouse.Id);

        using var second = await Client.PostAsJsonAsync(
            CompleteRoute(order.Id),
            new CompleteProductionOrderRequest(scenario.FinishedGoodsWarehouse.Id));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var finalOrder = await GetOrderAsync(Client, order.Id);
        Assert.Equal(first.CompletedAt, finalOrder.CompletedAt);
        Assert.Equal(10m, Assert.Single(await GetProductBalancesAsync(Client)).Quantity);
        Assert.Single((await GetProductHistoryAsync(Client)).Items, item => item.ReferenceId == order.Id);
    }

    [Fact]
    public async Task Concurrent_complete_of_same_order_succeeds_once() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        using var secondClient = CreateClient();
        await LoginAsync(secondClient, TestData.CompanyAAdminEmail);
        var scenario = await CreateScenarioAsync(Client, "SAME", 50m, 1m);
        var order = await PrepareStartedOrderAsync(Client, scenario, "PO-SAME", 25m);
        var request = new CompleteProductionOrderRequest(scenario.FinishedGoodsWarehouse.Id);

        var responses = await Task.WhenAll(
            Client.PostAsJsonAsync(CompleteRoute(order.Id), request),
            secondClient.PostAsJsonAsync(CompleteRoute(order.Id), request));

        Assert.Single(responses, response => response.IsSuccessStatusCode);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in responses) response.Dispose();
        Assert.Equal(ProductionOrderStatuses.Completed, (await GetOrderAsync(Client, order.Id)).Status);
        Assert.Equal(25m, Assert.Single(await GetProductBalancesAsync(Client)).Quantity);
        Assert.Single((await GetProductHistoryAsync(Client)).Items, item => item.ReferenceId == order.Id);
    }

    [Fact]
    public async Task Concurrent_complete_of_different_orders_accumulates_same_balance() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        using var secondClient = CreateClient();
        await LoginAsync(secondClient, TestData.CompanyAAdminEmail);
        var scenario = await CreateScenarioAsync(Client, "ACC", 100m, 1m);
        var orderA = await PrepareStartedOrderAsync(Client, scenario, "PO-A", 30m);
        var orderB = await PrepareStartedOrderAsync(Client, scenario, "PO-B", 40m);
        var request = new CompleteProductionOrderRequest(scenario.FinishedGoodsWarehouse.Id);

        var responses = await Task.WhenAll(
            Client.PostAsJsonAsync(CompleteRoute(orderA.Id), request),
            secondClient.PostAsJsonAsync(CompleteRoute(orderB.Id), request));

        Assert.All(responses, response => Assert.True(response.IsSuccessStatusCode));
        foreach (var response in responses) response.Dispose();
        Assert.Equal(70m, Assert.Single(await GetProductBalancesAsync(Client)).Quantity);
        var outputs = (await GetProductHistoryAsync(Client)).Items;
        Assert.Equal(2, outputs.Count);
        Assert.Contains(outputs, item => item.ReferenceId == orderA.Id && item.Quantity == 30m);
        Assert.Contains(outputs, item => item.ReferenceId == orderB.Id && item.Quantity == 40m);
    }

    [Fact]
    public async Task Sequential_complete_accumulates_balance_and_keeps_immutable_history() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var scenario = await CreateScenarioAsync(Client, "SEQ", 100m, 1m);
        var orderOne = await PrepareStartedOrderAsync(Client, scenario, "PO-SEQ-1", 50m);
        var orderTwo = await PrepareStartedOrderAsync(Client, scenario, "PO-SEQ-2", 30m);

        await CompleteAsync(Client, orderOne.Id, scenario.FinishedGoodsWarehouse.Id);
        await CompleteAsync(Client, orderTwo.Id, scenario.FinishedGoodsWarehouse.Id);

        Assert.Equal(80m, Assert.Single(await GetProductBalancesAsync(Client)).Quantity);
        var outputs = (await GetProductHistoryAsync(Client)).Items;
        Assert.Equal(2, outputs.Count);
        Assert.Equal(2, outputs.Select(item => item.Id).Distinct().Count());
        Assert.Contains(outputs, item => item.ReferenceId == orderOne.Id && item.Quantity == 50m);
        Assert.Contains(outputs, item => item.ReferenceId == orderTwo.Id && item.Quantity == 30m);
    }

    [Fact]
    public async Task Finished_goods_completion_and_queries_are_tenant_isolated() {
        using var companyAClient = CreateClient();
        using var companyBClient = CreateClient();
        await LoginAsync(companyAClient, TestData.CompanyAAdminEmail);
        await LoginAsync(companyBClient, TestData.CompanyBAdminEmail);
        var scenario = await CreateScenarioAsync(companyAClient, "TENANT", 20m, 1m);
        var companyBWarehouse = await CreateWarehouseAsync(
            companyBClient,
            "FG-TENANT-B",
            "Company B Finished Goods");
        var order = await PrepareStartedOrderAsync(companyAClient, scenario, "PO-TENANT", 10m);

        using (var crossTenantOrder = await companyBClient.PostAsJsonAsync(
                   CompleteRoute(order.Id),
                   new CompleteProductionOrderRequest(scenario.FinishedGoodsWarehouse.Id))) {
            Assert.Equal(HttpStatusCode.NotFound, crossTenantOrder.StatusCode);
        }
        using (var crossTenantWarehouse = await companyAClient.PostAsJsonAsync(
                   CompleteRoute(order.Id),
                   new CompleteProductionOrderRequest(companyBWarehouse.Id))) {
            Assert.Equal(HttpStatusCode.NotFound, crossTenantWarehouse.StatusCode);
        }
        Assert.Equal(ProductionOrderStatuses.InProgress, (await GetOrderAsync(companyAClient, order.Id)).Status);
        Assert.Empty(await GetProductBalancesAsync(companyAClient));

        await CompleteAsync(companyAClient, order.Id, scenario.FinishedGoodsWarehouse.Id);

        Assert.Empty(await GetProductBalancesAsync(
            companyBClient,
            $"?warehouseId={scenario.FinishedGoodsWarehouse.Id}&productId={scenario.Product.Id}"));
        Assert.Empty((await GetProductHistoryAsync(
            companyBClient,
            $"?warehouseId={scenario.FinishedGoodsWarehouse.Id}&productId={scenario.Product.Id}")).Items);
        Assert.Single(await GetProductBalancesAsync(companyAClient));
        Assert.Single((await GetProductHistoryAsync(companyAClient)).Items);
    }

    [Fact]
    public async Task Inactive_destination_warehouse_rolls_back_completion() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var scenario = await CreateScenarioAsync(Client, "INACTIVE", 20m, 1m);
        var order = await PrepareStartedOrderAsync(Client, scenario, "PO-INACTIVE", 10m);
        var rawBalanceBefore = await GetRawBalanceAsync(Client, scenario);
        var rawHistoryBefore = await GetRawHistoryAsync(Client);
        await DeactivateWarehouseAsync(Client, scenario.FinishedGoodsWarehouse.Id);

        using var response = await Client.PostAsJsonAsync(
            CompleteRoute(order.Id),
            new CompleteProductionOrderRequest(scenario.FinishedGoodsWarehouse.Id));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ProductionOrderStatuses.InProgress, (await GetOrderAsync(Client, order.Id)).Status);
        Assert.Equal(rawBalanceBefore.Quantity, (await GetRawBalanceAsync(Client, scenario)).Quantity);
        Assert.Equal(rawHistoryBefore.TotalCount, (await GetRawHistoryAsync(Client)).TotalCount);
        Assert.Empty(await GetProductBalancesAsync(Client));
        Assert.Empty((await GetProductHistoryAsync(Client)).Items);
    }

    [Fact]
    public async Task Product_inventory_queries_filter_page_and_return_contract_fields_only() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var scenario = await CreateScenarioAsync(Client, "QUERY-1", 100m, 1m);
        var secondProduct = await CreateProductAsync(Client, "P-QUERY-2", "Product QUERY 2");
        var secondBom = await CreateBomAsync(Client, secondProduct.Id, 1m, [
            new BomItemRequest(scenario.Material.Id, 1m, null)
        ]);
        await ActivateBomAsync(Client, secondProduct.Id, secondBom.Id);
        var secondFinishedGoodsWarehouse = await CreateWarehouseAsync(Client, "FG-QUERY-2", "Finished Goods 2");
        var firstOrder = await PrepareStartedOrderAsync(Client, scenario, "PO-QUERY-1", 10m);
        var secondOrder = await PrepareStartedOrderAsync(Client, scenario, "PO-QUERY-2", 20m);
        var thirdOrder = await PrepareStartedOrderAsync(
            Client,
            scenario with { Product = secondProduct },
            "PO-QUERY-3",
            30m);
        await CompleteAsync(Client, firstOrder.Id, scenario.FinishedGoodsWarehouse.Id);
        await CompleteAsync(Client, secondOrder.Id, secondFinishedGoodsWarehouse.Id);
        await CompleteAsync(Client, thirdOrder.Id, scenario.FinishedGoodsWarehouse.Id);

        var warehouseBalances = await GetProductBalancesAsync(
            Client,
            $"?warehouseId={scenario.FinishedGoodsWarehouse.Id}");
        var productBalances = await GetProductBalancesAsync(Client, $"?productId={scenario.Product.Id}");
        var typedHistory = await GetProductHistoryAsync(
            Client,
            "?transactionType=ProductionOutput");
        var firstPage = await GetProductHistoryAsync(Client, "?page=1&pageSize=1");
        var secondPage = await GetProductHistoryAsync(Client, "?page=2&pageSize=1");

        Assert.Equal(2, warehouseBalances.Count);
        Assert.All(warehouseBalances, balance =>
            Assert.Equal(scenario.FinishedGoodsWarehouse.Id, balance.WarehouseId));
        Assert.Equal(2, productBalances.Count);
        Assert.All(productBalances, balance => Assert.Equal(scenario.Product.Id, balance.ProductId));
        Assert.Equal(3, typedHistory.TotalCount);
        Assert.All(typedHistory.Items, item => {
            Assert.Equal(ProductInventoryTransactionType.ProductionOutput, item.Type);
            Assert.Equal(item.Quantity, item.SignedQuantity);
            Assert.Equal("ProductionOrder", item.ReferenceType);
            Assert.NotNull(item.ReferenceId);
        });
        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(1, firstPage.Page);
        Assert.Equal(1, firstPage.PageSize);
        Assert.Single(firstPage.Items);
        Assert.Equal(2, secondPage.Page);
        Assert.Single(secondPage.Items);
        Assert.NotEqual(firstPage.Items[0].Id, secondPage.Items[0].Id);
        await AssertProductInventoryResponsesDoNotLeakEntitiesAsync(Client);
    }

    [Fact]
    public async Task Product_delete_is_blocked_by_finished_goods_balance_or_history() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var warehouse = await CreateWarehouseAsync(Client, "FG-DELETE", "Delete Protection Warehouse");
        var balanceProduct = await CreateProductAsync(Client, "P-BALANCE", "Balance Product");
        var historyProduct = await CreateProductAsync(Client, "P-HISTORY", "History Product");
        using (var scope = ApiFactory.Services.CreateScope()) {
            var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
            dbContext.ProductInventoryBalances.Add(new ProductInventoryBalance {
                CompanyId = TestData.CompanyAId,
                WarehouseId = warehouse.Id,
                ProductId = balanceProduct.Id,
                Quantity = 1m
            });
            dbContext.ProductInventoryTransactions.Add(new ProductInventoryTransaction {
                CompanyId = TestData.CompanyAId,
                WarehouseId = warehouse.Id,
                ProductId = historyProduct.Id,
                Type = ProductInventoryTransactionType.ProductionOutput,
                Quantity = 1m,
                ReferenceType = "MigrationTest",
                ReferenceId = Guid.NewGuid()
            });
            await dbContext.SaveChangesAsync();
        }

        using var balanceDelete = await Client.DeleteAsync(ProductRoute(balanceProduct.Id));
        using var historyDelete = await Client.DeleteAsync(ProductRoute(historyProduct.Id));

        Assert.Equal(HttpStatusCode.Conflict, balanceDelete.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, historyDelete.StatusCode);
        Assert.Contains(await GetProductsAsync(Client), product => product.Id == balanceProduct.Id);
        Assert.Contains(await GetProductsAsync(Client), product => product.Id == historyProduct.Id);
    }

    private static string ProductsRoute => ApiRoutes.Products.Group + ApiRoutes.Products.Root;
    private static string ProductionOrdersRoute =>
        ApiRoutes.ProductionOrders.Group + ApiRoutes.ProductionOrders.Root;
    private static string ProductInventoriesRoute =>
        ApiRoutes.ProductInventories.Group + ApiRoutes.ProductInventories.Root;
    private static string ProductInventoryTransactionsRoute =>
        ApiRoutes.ProductInventories.Group + ApiRoutes.ProductInventories.Transactions;

    private static string ProductRoute(Guid productId) => ApiRoutes.Products.Group +
        ApiRoutes.Products.ById.Replace("{productId:guid}", productId.ToString(), StringComparison.Ordinal);

    private static string BomsRoute(Guid productId) => ApiRoutes.Products.Group +
        ApiRoutes.Products.Boms.Replace("{productId:guid}", productId.ToString(), StringComparison.Ordinal);

    private static string ActivateBomRoute(Guid productId, Guid bomId) => ApiRoutes.Products.Group +
        ApiRoutes.Products.ActivateBom
            .Replace("{productId:guid}", productId.ToString(), StringComparison.Ordinal)
            .Replace("{bomId:guid}", bomId.ToString(), StringComparison.Ordinal);

    private static string OrderRoute(Guid orderId) => ApiRoutes.ProductionOrders.Group +
        ApiRoutes.ProductionOrders.ById.Replace(
            "{productionOrderId:guid}", orderId.ToString(), StringComparison.Ordinal);

    private static string ReleaseRoute(Guid orderId) => ApiRoutes.ProductionOrders.Group +
        ApiRoutes.ProductionOrders.Release.Replace(
            "{productionOrderId:guid}", orderId.ToString(), StringComparison.Ordinal);

    private static string StartRoute(Guid orderId) => ApiRoutes.ProductionOrders.Group +
        ApiRoutes.ProductionOrders.Start.Replace(
            "{productionOrderId:guid}", orderId.ToString(), StringComparison.Ordinal);

    private static string CompleteRoute(Guid orderId) => ApiRoutes.ProductionOrders.Group +
        ApiRoutes.ProductionOrders.Complete.Replace(
            "{productionOrderId:guid}", orderId.ToString(), StringComparison.Ordinal);

    private static string StartOperationRoute(Guid orderId, Guid operationId) =>
        ApiRoutes.ProductionOrders.Group + ApiRoutes.ProductionOrders.StartOperation
            .Replace("{productionOrderId:guid}", orderId.ToString(), StringComparison.Ordinal)
            .Replace("{operationId:guid}", operationId.ToString(), StringComparison.Ordinal);

    private static string CompleteOperationRoute(Guid orderId, Guid operationId) =>
        ApiRoutes.ProductionOrders.Group + ApiRoutes.ProductionOrders.CompleteOperation
            .Replace("{productionOrderId:guid}", orderId.ToString(), StringComparison.Ordinal)
            .Replace("{operationId:guid}", operationId.ToString(), StringComparison.Ordinal);

    private static async Task<CompletionScenario> CreateScenarioAsync(
        HttpClient client,
        string suffix,
        decimal rawQuantity,
        decimal componentQuantity) {
        var product = await CreateProductAsync(client, $"P-{suffix}", $"Product {suffix}");
        var material = await CreateMaterialAsync(client, $"MAT-{suffix}", $"Material {suffix}");
        var rawWarehouse = await CreateWarehouseAsync(client, $"RAW-{suffix}", $"Raw {suffix}");
        var finishedGoodsWarehouse = await CreateWarehouseAsync(client, $"FG-{suffix}", $"Finished {suffix}");
        await ReceiveAsync(client, rawWarehouse.Id, material.Id, rawQuantity);
        var bom = await CreateBomAsync(client, product.Id, 1m, [
            new BomItemRequest(material.Id, componentQuantity, null)
        ]);
        await ActivateBomAsync(client, product.Id, bom.Id);
        return new(product, material, rawWarehouse, finishedGoodsWarehouse, componentQuantity);
    }

    private static async Task<ProductionOrderResponse> PrepareStartedOrderAsync(
        HttpClient client,
        CompletionScenario scenario,
        string number,
        decimal quantity) {
        var order = await CreateProductionOrderAsync(client, number, scenario.Product.Id, quantity);
        await ReleaseAsync(client, order.Id);
        var started = await StartAsync(client, order.Id, [
            new ProductionMaterialAllocationRequest(
                scenario.Material.Id,
                scenario.RawWarehouse.Id,
                quantity * scenario.ComponentQuantity)
        ]);
        foreach (var operation in started.Operations.OrderBy(operation => operation.Sequence)) {
            using var startResponse = await client.PostAsync(
                StartOperationRoute(order.Id, operation.Id), null);
            startResponse.EnsureSuccessStatusCode();
            using var completeResponse = await client.PostAsync(
                CompleteOperationRoute(order.Id, operation.Id), null);
            completeResponse.EnsureSuccessStatusCode();
        }
        return started;
    }

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
        string name) {
        using var response = await client.PostAsJsonAsync(MaterialsRoute, new MaterialRequest(code, name, "kg"));
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

    private static async Task DeactivateWarehouseAsync(HttpClient client, Guid warehouseId) {
        var route = ApiRoutes.Warehouses.Group + ApiRoutes.Warehouses.ById.Replace(
            "{warehouseId:guid}", warehouseId.ToString(), StringComparison.Ordinal);
        using var response = await client.DeleteAsync(route);
        response.EnsureSuccessStatusCode();
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
        using var response = await client.PostAsync(ActivateBomRoute(productId, bomId), null);
        response.EnsureSuccessStatusCode();
        await EnsureActiveRoutingAsync(client, productId);
    }

    private static async Task EnsureActiveRoutingAsync(HttpClient client, Guid productId) {
        var routingsRoute = ApiRoutes.Products.Group + ApiRoutes.Products.Routings.Replace(
            "{productId:guid}", productId.ToString(), StringComparison.Ordinal);
        var existing = await client.GetFromJsonAsync<ApiResponse<IReadOnlyList<RoutingResponse>>>(routingsRoute);
        if (existing?.Data?.Any(routing => routing.Status == RoutingStatuses.Active) == true) {
            return;
        }

        using var workCenterResponse = await client.PostAsJsonAsync(
            ApiRoutes.WorkCenters.Group,
            new WorkCenterCreateRequest($"WC-{productId:N}", "Default Work Center", null));
        workCenterResponse.EnsureSuccessStatusCode();
        var workCenter = (await workCenterResponse.Content
            .ReadFromJsonAsync<ApiResponse<WorkCenterResponse>>())!.Data!;
        using var createResponse = await client.PostAsJsonAsync(routingsRoute, new RoutingRequest([
            new RoutingOperationRequest(10, "Manufacture", workCenter.Id, 0, 1, null)
        ]));
        createResponse.EnsureSuccessStatusCode();
        var routing = (await createResponse.Content.ReadFromJsonAsync<ApiResponse<RoutingResponse>>())!.Data!;
        var activateRoute = ApiRoutes.Products.Group + ApiRoutes.Products.ActivateRouting
            .Replace("{productId:guid}", productId.ToString(), StringComparison.Ordinal)
            .Replace("{routingId:guid}", routing.Id.ToString(), StringComparison.Ordinal);
        using var activateResponse = await client.PostAsync(activateRoute, null);
        activateResponse.EnsureSuccessStatusCode();
    }

    private static async Task<ProductionOrderResponse> CreateProductionOrderAsync(
        HttpClient client,
        string number,
        Guid productId,
        decimal quantity) {
        using var response = await client.PostAsJsonAsync(
            ProductionOrdersRoute,
            new ProductionOrderRequest(number, productId, quantity));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<ProductionOrderResponse>>())!.Data!;
    }

    private static async Task ReleaseAsync(HttpClient client, Guid orderId) {
        using var response = await client.PostAsync(ReleaseRoute(orderId), null);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<ProductionOrderResponse> StartAsync(
        HttpClient client,
        Guid orderId,
        IReadOnlyList<ProductionMaterialAllocationRequest> allocations) {
        using var response = await client.PostAsJsonAsync(
            StartRoute(orderId),
            new StartProductionOrderRequest(allocations));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<ProductionOrderResponse>>())!.Data!;
    }

    private static async Task<ProductionOrderResponse> CompleteAsync(
        HttpClient client,
        Guid orderId,
        Guid warehouseId) {
        using var response = await client.PostAsJsonAsync(
            CompleteRoute(orderId),
            new CompleteProductionOrderRequest(warehouseId));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<ProductionOrderResponse>>())!.Data!;
    }

    private static async Task<ProductionOrderResponse> GetOrderAsync(HttpClient client, Guid orderId) {
        var envelope = await client.GetFromJsonAsync<ApiResponse<IReadOnlyList<ProductionOrderResponse>>>(
            ProductionOrdersRoute);
        return envelope?.Data?.Single(order => order.Id == orderId)
            ?? throw new InvalidOperationException("Production order response did not contain data.");
    }

    private static async Task<IReadOnlyList<ProductResponse>> GetProductsAsync(HttpClient client) {
        var envelope = await client.GetFromJsonAsync<ApiResponse<IReadOnlyList<ProductResponse>>>(ProductsRoute);
        return envelope?.Data ?? [];
    }

    private static async Task<InventoryBalanceResponse> GetRawBalanceAsync(
        HttpClient client,
        CompletionScenario scenario) {
        var envelope = await client.GetFromJsonAsync<ApiResponse<IReadOnlyList<InventoryBalanceResponse>>>(
            $"{InventoriesRoute}?warehouseId={scenario.RawWarehouse.Id}&materialId={scenario.Material.Id}");
        return Assert.Single(envelope?.Data ?? []);
    }

    private static async Task<InventoryTransactionPageResponse> GetRawHistoryAsync(HttpClient client) {
        var envelope = await client.GetFromJsonAsync<ApiResponse<InventoryTransactionPageResponse>>(
            InventoryRoute(ApiRoutes.Inventories.Transactions),
            JsonOptions);
        return envelope?.Data
            ?? throw new InvalidOperationException("Raw inventory history response did not contain data.");
    }

    private static async Task<IReadOnlyList<ProductInventoryBalanceResponse>> GetProductBalancesAsync(
        HttpClient client,
        string query = "") {
        var envelope = await client.GetFromJsonAsync<ApiResponse<IReadOnlyList<ProductInventoryBalanceResponse>>>(
            ProductInventoriesRoute + query,
            JsonOptions);
        return envelope?.Data ?? [];
    }

    private static async Task<ProductInventoryTransactionPageResponse> GetProductHistoryAsync(
        HttpClient client,
        string query = "") {
        var envelope = await client.GetFromJsonAsync<ApiResponse<ProductInventoryTransactionPageResponse>>(
            ProductInventoryTransactionsRoute + query,
            JsonOptions);
        return envelope?.Data
            ?? throw new InvalidOperationException("Product inventory history response did not contain data.");
    }

    private static async Task AssertProductInventoryResponsesDoNotLeakEntitiesAsync(HttpClient client) {
        using var balancesResponse = await client.GetAsync(ProductInventoriesRoute);
        balancesResponse.EnsureSuccessStatusCode();
        using var balancesDocument = JsonDocument.Parse(await balancesResponse.Content.ReadAsStringAsync());
        foreach (var item in balancesDocument.RootElement.GetProperty("data").EnumerateArray()) {
            Assert.False(item.TryGetProperty("companyId", out _));
            Assert.False(item.TryGetProperty("company", out _));
            Assert.False(item.TryGetProperty("warehouse", out _));
            Assert.False(item.TryGetProperty("product", out _));
        }

        using var historyResponse = await client.GetAsync(ProductInventoryTransactionsRoute);
        historyResponse.EnsureSuccessStatusCode();
        using var historyDocument = JsonDocument.Parse(await historyResponse.Content.ReadAsStringAsync());
        foreach (var item in historyDocument.RootElement.GetProperty("data").GetProperty("items").EnumerateArray()) {
            Assert.False(item.TryGetProperty("companyId", out _));
            Assert.False(item.TryGetProperty("company", out _));
            Assert.False(item.TryGetProperty("warehouse", out _));
            Assert.False(item.TryGetProperty("product", out _));
            Assert.False(item.TryGetProperty("createdByUser", out _));
        }
    }

    private static JsonSerializerOptions CreateJsonOptions() {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record CompletionScenario(
        ProductResponse Product,
        MaterialResponse Material,
        WarehouseResponse RawWarehouse,
        WarehouseResponse FinishedGoodsWarehouse,
        decimal ComponentQuantity);
}
