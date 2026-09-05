using System.Net;
using System.Net.Http.Json;
using FactoryMind.Api.Endpoints;
using FactoryMind.Api.Routing;
using FactoryMind.Application.Features.Boms;
using FactoryMind.Application.Features.Materials;
using FactoryMind.Application.Features.Products;
using FactoryMind.Application.Features.ProductionOrders;
using FactoryMind.Application.Features.Routings;
using FactoryMind.Application.Features.Warehouses;
using FactoryMind.Application.Features.WorkCenters;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Infrastructure.Persistence;
using FactoryMind.IntegrationTests.Infrastructure;
using FactoryMind.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryMind.IntegrationTests.Manufacturing;

[Collection(IntegrationTestCollection.Name)]
public sealed class ProductionOperationExecutionIntegrationTests(PostgreSqlFixture fixture)
    : IntegrationTestBase(fixture) {
    [Fact]
    public async Task Release_requires_an_active_routing_and_leaves_no_partial_snapshot() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var product = await PostAsync<ProductResponse>(
            Client, ApiRoutes.Products.Group, new ProductRequest("P-NO-ROUTE", "No Route Product"));
        var material = await PostAsync<MaterialResponse>(
            Client, ApiRoutes.Materials.Group, new MaterialRequest("M-NO-ROUTE", "Material", "kg"));
        var bom = await PostAsync<BomResponse>(Client, BomsRoute(product.Id), new BomRequest(
            1m, [new BomItemRequest(material.Id, 1m, null)]));
        using (var activateBom = await Client.PostAsync(ActivateBomRoute(product.Id, bom.Id), null)) {
            activateBom.EnsureSuccessStatusCode();
        }
        var order = await CreateOrderAsync(Client, "PO-NO-ROUTE", product.Id, 1m);

        using var release = await Client.PostAsync(ReleaseOrderRoute(order.Id), null);
        Assert.Equal(HttpStatusCode.Conflict, release.StatusCode);
        var persisted = await GetOrderAsync(Client, order.Id);
        Assert.Equal(ProductionOrderStatuses.Planned, persisted.Status);
        Assert.Null(persisted.BillOfMaterialId);
        Assert.Null(persisted.RoutingId);
        Assert.Empty(persisted.Operations);
    }

    [Fact]
    public async Task Concurrent_release_has_one_success_and_one_conflict_with_one_complete_snapshot() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var scenario = await CreateScenarioAsync(Client, "RELEASE-RACE");
        var order = await CreateOrderAsync(Client, "PO-RELEASE-RACE", scenario.Product.Id, 1m);

        var responses = await Task.WhenAll(
            Client.PostAsync(ReleaseOrderRoute(order.Id), null),
            Client.PostAsync(ReleaseOrderRoute(order.Id), null));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in responses) response.Dispose();
        var persisted = await GetOrderAsync(Client, order.Id);
        Assert.Equal(ProductionOrderStatuses.Released, persisted.Status);
        Assert.Equal(3, persisted.Operations.Count);
        Assert.Equal(3, persisted.Operations.Select(operation => operation.Sequence).Distinct().Count());
    }

    [Fact]
    public async Task Release_locks_routing_and_snapshots_operations_across_later_revisions() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var scenario = await CreateScenarioAsync(Client, "SNAPSHOT");
        var orderOne = await CreateOrderAsync(Client, "PO-SNAPSHOT-1", scenario.Product.Id, 5m);
        var releasedOne = await ReleaseAsync(Client, orderOne.Id);

        Assert.Equal(scenario.Bom.Id, releasedOne.BillOfMaterialId);
        Assert.Equal(scenario.Routing.Id, releasedOne.RoutingId);
        Assert.Equal(1, releasedOne.RoutingRevision);
        Assert.Equal(new[] { 10, 20, 30 }, releasedOne.Operations.Select(item => item.Sequence));
        Assert.Equal(new[] { "Cutting", "Assembly", "Packaging" },
            releasedOne.Operations.Select(item => item.Name));

        var revisionTwo = await CreateRoutingAsync(Client, scenario.Product.Id, [
            new RoutingOperationRequest(10, "Laser cutting", scenario.Cutting.Id, 3, 6, null),
            new RoutingOperationRequest(20, "Assembly v2", scenario.Assembly.Id, 1, 8, null)
        ]);
        await ActivateRoutingAsync(Client, scenario.Product.Id, revisionTwo.Id);

        var persistedOne = await GetOrderAsync(Client, orderOne.Id);
        Assert.Equal(scenario.Routing.Id, persistedOne.RoutingId);
        Assert.Equal(new[] { "Cutting", "Assembly", "Packaging" },
            persistedOne.Operations.Select(item => item.Name));
        var orderTwo = await CreateOrderAsync(Client, "PO-SNAPSHOT-2", scenario.Product.Id, 5m);
        var releasedTwo = await ReleaseAsync(Client, orderTwo.Id);
        Assert.Equal(revisionTwo.Id, releasedTwo.RoutingId);
        Assert.Equal(2, releasedTwo.RoutingRevision);
        Assert.Equal(new[] { "Laser cutting", "Assembly v2" },
            releasedTwo.Operations.Select(item => item.Name));
    }

    [Fact]
    public async Task Operations_enforce_sequence_and_order_completion_requires_all_operations() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var scenario = await CreateScenarioAsync(Client, "HAPPY");
        var order = await CreateOrderAsync(Client, "PO-HAPPY", scenario.Product.Id, 5m);
        var released = await ReleaseAsync(Client, order.Id);
        var started = await StartOrderAsync(Client, released, scenario, 5m);
        Assert.Equal(ProductionOrderStatuses.InProgress, started.Status);

        using (var startSecond = await Client.PostAsync(
                   StartOperationRoute(order.Id, released.Operations[1].Id), null)) {
            Assert.Equal(HttpStatusCode.Conflict, startSecond.StatusCode);
        }
        using (var completeTooEarly = await Client.PostAsJsonAsync(
                   CompleteOrderRoute(order.Id), new CompleteProductionOrderRequest(scenario.Finished.Id))) {
            Assert.Equal(HttpStatusCode.Conflict, completeTooEarly.StatusCode);
        }

        foreach (var operation in released.Operations) {
            var running = await StartOperationAsync(Client, order.Id, operation.Id);
            Assert.Equal(ProductionOperationStatuses.InProgress, running.Status);
            Assert.NotNull(running.StartedAt);
            var completed = await CompleteOperationAsync(Client, order.Id, operation.Id);
            Assert.Equal(ProductionOperationStatuses.Completed, completed.Status);
            Assert.NotNull(completed.CompletedAt);
        }
        var completedOrder = await CompleteOrderAsync(Client, order.Id, scenario.Finished.Id);
        Assert.Equal(ProductionOrderStatuses.Completed, completedOrder.Status);

        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        Assert.Equal(95m, (await dbContext.InventoryBalances.SingleAsync()).Quantity);
        Assert.Single(await dbContext.InventoryTransactions.Where(transaction =>
            transaction.Type == InventoryTransactionType.ProductionConsume).ToListAsync());
        Assert.Equal(5m, (await dbContext.ProductInventoryBalances.SingleAsync()).Quantity);
        Assert.Single(await dbContext.ProductInventoryTransactions.ToListAsync());
    }

    [Fact]
    public async Task Concurrent_operation_transitions_have_exactly_one_success_and_terminal_states_are_frozen() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var scenario = await CreateScenarioAsync(Client, "CONCURRENT");
        var order = await CreateOrderAsync(Client, "PO-CONCURRENT", scenario.Product.Id, 5m);
        var released = await ReleaseAsync(Client, order.Id);
        await StartOrderAsync(Client, released, scenario, 5m);
        var operation = released.Operations[0];

        var starts = await Task.WhenAll(
            Client.PostAsync(StartOperationRoute(order.Id, operation.Id), null),
            Client.PostAsync(StartOperationRoute(order.Id, operation.Id), null));
        Assert.Single(starts, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(starts, response => response.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in starts) {
            response.Dispose();
        }

        var completes = await Task.WhenAll(
            Client.PostAsync(CompleteOperationRoute(order.Id, operation.Id), null),
            Client.PostAsync(CompleteOperationRoute(order.Id, operation.Id), null));
        Assert.Single(completes, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(completes, response => response.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in completes) {
            response.Dispose();
        }

        foreach (var remaining in released.Operations.Skip(1)) {
            await StartOperationAsync(Client, order.Id, remaining.Id);
            await CompleteOperationAsync(Client, order.Id, remaining.Id);
        }
        await CompleteOrderAsync(Client, order.Id, scenario.Finished.Id);

        using var startAfterOrderComplete = await Client.PostAsync(
            StartOperationRoute(order.Id, operation.Id), null);
        using var completeAfterOrderComplete = await Client.PostAsync(
            CompleteOperationRoute(order.Id, operation.Id), null);
        using var completeOrderAgain = await Client.PostAsJsonAsync(
            CompleteOrderRoute(order.Id), new CompleteProductionOrderRequest(scenario.Finished.Id));
        Assert.Equal(HttpStatusCode.Conflict, startAfterOrderComplete.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, completeAfterOrderComplete.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, completeOrderAgain.StatusCode);
    }

    [Fact]
    public async Task Last_operation_completion_race_never_outputs_before_the_operation_completes() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var scenario = await CreateScenarioAsync(Client, "RACE", operationCount: 1);
        var order = await CreateOrderAsync(Client, "PO-RACE", scenario.Product.Id, 5m);
        var released = await ReleaseAsync(Client, order.Id);
        await StartOrderAsync(Client, released, scenario, 5m);
        await StartOperationAsync(Client, order.Id, released.Operations[0].Id);

        var results = await Task.WhenAll(
            Client.PostAsync(CompleteOperationRoute(order.Id, released.Operations[0].Id), null),
            Client.PostAsJsonAsync(
                CompleteOrderRoute(order.Id),
                new CompleteProductionOrderRequest(scenario.Finished.Id)));
        Assert.Equal(HttpStatusCode.OK, results[0].StatusCode);
        Assert.Contains(results[1].StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
        foreach (var response in results) {
            response.Dispose();
        }
        if ((await GetOrderAsync(Client, order.Id)).Status == ProductionOrderStatuses.InProgress) {
            await CompleteOrderAsync(Client, order.Id, scenario.Finished.Id);
        }

        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        Assert.Equal(ProductionOperationStatuses.Completed,
            (await dbContext.ProductionOrderOperations.SingleAsync()).Status);
        Assert.Single(await dbContext.ProductInventoryTransactions.ToListAsync());
    }

    [Fact]
    public async Task Operation_routes_do_not_disclose_another_tenants_order() {
        var companyAClient = CreateClient();
        var companyBClient = CreateClient();
        await LoginAsync(companyAClient, TestData.CompanyAAdminEmail);
        await LoginAsync(companyBClient, TestData.CompanyBAdminEmail);
        var scenario = await CreateScenarioAsync(companyAClient, "TENANT", operationCount: 1);
        var order = await CreateOrderAsync(companyAClient, "PO-TENANT-OPS", scenario.Product.Id, 1m);
        var released = await ReleaseAsync(companyAClient, order.Id);

        using var list = await companyBClient.GetAsync(OperationsRoute(order.Id));
        using var start = await companyBClient.PostAsync(
            StartOperationRoute(order.Id, released.Operations[0].Id), null);
        using var complete = await companyBClient.PostAsync(
            CompleteOperationRoute(order.Id, released.Operations[0].Id), null);
        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, start.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, complete.StatusCode);
    }

    private static async Task<Scenario> CreateScenarioAsync(
        HttpClient client,
        string suffix,
        int operationCount = 3) {
        var product = await PostAsync<ProductResponse>(
            client, ApiRoutes.Products.Group, new ProductRequest($"P-{suffix}", $"Product {suffix}"));
        var material = await PostAsync<MaterialResponse>(
            client, ApiRoutes.Materials.Group, new MaterialRequest($"M-{suffix}", $"Material {suffix}", "kg"));
        var raw = await PostAsync<WarehouseResponse>(
            client, ApiRoutes.Warehouses.Group, new WarehouseCreateRequest($"RAW-{suffix}", "Raw", null));
        var finished = await PostAsync<WarehouseResponse>(
            client, ApiRoutes.Warehouses.Group, new WarehouseCreateRequest($"FG-{suffix}", "Finished", null));
        await PostAsync<object>(client, ApiRoutes.Inventories.Group + ApiRoutes.Inventories.Receive,
            new InventoryMovementRequest(raw.Id, material.Id, 100m, null, null, null));
        var bom = await PostAsync<BomResponse>(client, BomsRoute(product.Id), new BomRequest(
            1m, [new BomItemRequest(material.Id, 1m, null)]));
        using (var activateBom = await client.PostAsync(ActivateBomRoute(product.Id, bom.Id), null)) {
            activateBom.EnsureSuccessStatusCode();
        }
        var cutting = await PostAsync<WorkCenterResponse>(
            client, ApiRoutes.WorkCenters.Group, new WorkCenterCreateRequest($"CUT-{suffix}", "Cutting", null));
        var assembly = await PostAsync<WorkCenterResponse>(
            client, ApiRoutes.WorkCenters.Group, new WorkCenterCreateRequest($"ASM-{suffix}", "Assembly", null));
        var packaging = await PostAsync<WorkCenterResponse>(
            client, ApiRoutes.WorkCenters.Group, new WorkCenterCreateRequest($"PKG-{suffix}", "Packaging", null));
        var definitions = new[] {
            new RoutingOperationRequest(10, "Cutting", cutting.Id, 2, 5, null),
            new RoutingOperationRequest(20, "Assembly", assembly.Id, 1, 10, null),
            new RoutingOperationRequest(30, "Packaging", packaging.Id, 0, 3, null)
        }.Take(operationCount).ToList();
        var routing = await CreateRoutingAsync(client, product.Id, definitions);
        await ActivateRoutingAsync(client, product.Id, routing.Id);
        return new(product, material, raw, finished, bom, routing, cutting, assembly);
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string route, object body) {
        using var response = await client.PostAsJsonAsync(route, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<T>>())!.Data!;
    }

    private static async Task<RoutingResponse> CreateRoutingAsync(
        HttpClient client,
        Guid productId,
        IReadOnlyList<RoutingOperationRequest> operations) => await PostAsync<RoutingResponse>(
        client, RoutingsRoute(productId), new RoutingRequest(operations));

    private static async Task ActivateRoutingAsync(HttpClient client, Guid productId, Guid routingId) {
        using var response = await client.PostAsync(ActivateRoutingRoute(productId, routingId), null);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<ProductionOrderResponse> CreateOrderAsync(
        HttpClient client,
        string number,
        Guid productId,
        decimal quantity) => await PostAsync<ProductionOrderResponse>(
        client, ApiRoutes.ProductionOrders.Group, new ProductionOrderRequest(number, productId, quantity));

    private static async Task<ProductionOrderResponse> ReleaseAsync(HttpClient client, Guid orderId) {
        using var response = await client.PostAsync(ReleaseOrderRoute(orderId), null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<ProductionOrderResponse>>())!.Data!;
    }

    private static async Task<ProductionOrderResponse> StartOrderAsync(
        HttpClient client,
        ProductionOrderResponse order,
        Scenario scenario,
        decimal quantity) => await PostAsync<ProductionOrderResponse>(
        client,
        StartOrderRoute(order.Id),
        new StartProductionOrderRequest([
            new ProductionMaterialAllocationRequest(scenario.Material.Id, scenario.Raw.Id, quantity)
        ]));

    private static async Task<ProductionOrderOperationResponse> StartOperationAsync(
        HttpClient client,
        Guid orderId,
        Guid operationId) {
        using var response = await client.PostAsync(StartOperationRoute(orderId, operationId), null);
        response.EnsureSuccessStatusCode();
        return (await response.Content
            .ReadFromJsonAsync<ApiResponse<ProductionOrderOperationResponse>>())!.Data!;
    }

    private static async Task<ProductionOrderOperationResponse> CompleteOperationAsync(
        HttpClient client,
        Guid orderId,
        Guid operationId) {
        using var response = await client.PostAsync(CompleteOperationRoute(orderId, operationId), null);
        response.EnsureSuccessStatusCode();
        return (await response.Content
            .ReadFromJsonAsync<ApiResponse<ProductionOrderOperationResponse>>())!.Data!;
    }

    private static async Task<ProductionOrderResponse> CompleteOrderAsync(
        HttpClient client,
        Guid orderId,
        Guid warehouseId) => await PostAsync<ProductionOrderResponse>(
        client, CompleteOrderRoute(orderId), new CompleteProductionOrderRequest(warehouseId));

    private static async Task<ProductionOrderResponse> GetOrderAsync(HttpClient client, Guid orderId) {
        var response = await client.GetFromJsonAsync<ApiResponse<IReadOnlyList<ProductionOrderResponse>>>(
            ApiRoutes.ProductionOrders.Group);
        return response!.Data!.Single(order => order.Id == orderId);
    }

    private static string BomsRoute(Guid productId) => ApiRoutes.Products.Group + ApiRoutes.Products.Boms
        .Replace("{productId:guid}", productId.ToString(), StringComparison.Ordinal);
    private static string ActivateBomRoute(Guid productId, Guid bomId) => ApiRoutes.Products.Group +
        ApiRoutes.Products.ActivateBom.Replace("{productId:guid}", productId.ToString(), StringComparison.Ordinal)
            .Replace("{bomId:guid}", bomId.ToString(), StringComparison.Ordinal);
    private static string RoutingsRoute(Guid productId) => ApiRoutes.Products.Group + ApiRoutes.Products.Routings
        .Replace("{productId:guid}", productId.ToString(), StringComparison.Ordinal);
    private static string ActivateRoutingRoute(Guid productId, Guid routingId) => ApiRoutes.Products.Group +
        ApiRoutes.Products.ActivateRouting
            .Replace("{productId:guid}", productId.ToString(), StringComparison.Ordinal)
            .Replace("{routingId:guid}", routingId.ToString(), StringComparison.Ordinal);
    private static string ReleaseOrderRoute(Guid orderId) => OrderRoute(ApiRoutes.ProductionOrders.Release, orderId);
    private static string StartOrderRoute(Guid orderId) => OrderRoute(ApiRoutes.ProductionOrders.Start, orderId);
    private static string CompleteOrderRoute(Guid orderId) => OrderRoute(ApiRoutes.ProductionOrders.Complete, orderId);
    private static string OperationsRoute(Guid orderId) => OrderRoute(ApiRoutes.ProductionOrders.Operations, orderId);
    private static string StartOperationRoute(Guid orderId, Guid operationId) =>
        OperationRoute(ApiRoutes.ProductionOrders.StartOperation, orderId, operationId);
    private static string CompleteOperationRoute(Guid orderId, Guid operationId) =>
        OperationRoute(ApiRoutes.ProductionOrders.CompleteOperation, orderId, operationId);
    private static string OrderRoute(string route, Guid orderId) => ApiRoutes.ProductionOrders.Group + route
        .Replace("{productionOrderId:guid}", orderId.ToString(), StringComparison.Ordinal);
    private static string OperationRoute(string route, Guid orderId, Guid operationId) => OrderRoute(route, orderId)
        .Replace("{operationId:guid}", operationId.ToString(), StringComparison.Ordinal);

    private sealed record Scenario(
        ProductResponse Product,
        MaterialResponse Material,
        WarehouseResponse Raw,
        WarehouseResponse Finished,
        BomResponse Bom,
        RoutingResponse Routing,
        WorkCenterResponse Cutting,
        WorkCenterResponse Assembly);
}
