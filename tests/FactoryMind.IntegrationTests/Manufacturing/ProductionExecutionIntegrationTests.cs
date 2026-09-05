using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FactoryMind.Api.Endpoints;
using FactoryMind.Api.Routing;
using FactoryMind.Application.Features.Boms;
using FactoryMind.Application.Features.Inventories;
using FactoryMind.Application.Features.Materials;
using FactoryMind.Application.Features.Products;
using FactoryMind.Application.Features.ProductionOrders;
using FactoryMind.Application.Features.Warehouses;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Infrastructure.Persistence;
using FactoryMind.IntegrationTests.Infrastructure;
using FactoryMind.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryMind.IntegrationTests.Manufacturing;

[Collection(IntegrationTestCollection.Name)]
public sealed class ProductionExecutionIntegrationTests(PostgreSqlFixture fixture)
    : IntegrationTestBase(fixture) {
    private const string FinishedGoodsMigration =
        "20260830081645_AddFinishedGoodsInventoryAndProductionCompletion";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task Release_locks_exact_Bom_revision_and_writes_no_inventory_transaction() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var product = await CreateProductAsync(Client, "P001", "Locked Revision Product");
        var steel = await CreateMaterialAsync(Client, "STEEL-LOCK", "Steel");
        var revisionOne = await CreateBomAsync(Client, product.Id, 1m, [
            new BomItemRequest(steel.Id, 1m, null)
        ]);
        await ActivateBomAsync(Client, product.Id, revisionOne.Id);
        var orderOne = await CreateProductionOrderAsync(Client, "PO-001", product.Id, 10m);

        var released = await ReleaseAsync(Client, orderOne.Id);

        Assert.Equal(ProductionOrderStatuses.Released, released.Status);
        Assert.Equal(revisionOne.Id, released.BillOfMaterialId);
        Assert.Equal(1, released.BomRevision);
        Assert.NotNull(released.ReleasedAt);
        Assert.Equal(0, (await GetHistoryAsync(Client)).TotalCount);

        var revisionTwo = await CreateBomAsync(Client, product.Id, 1m, [
            new BomItemRequest(steel.Id, 2m, null)
        ]);
        await ActivateBomAsync(Client, product.Id, revisionTwo.Id);

        var lockedRequirements = await GetOrderRequirementsAsync(Client, orderOne.Id);
        Assert.Equal(1, lockedRequirements.BomRevision);
        Assert.Equal(10m, Assert.Single(lockedRequirements.Materials).RequiredQuantity);

        var orderTwo = await CreateProductionOrderAsync(Client, "PO-002", product.Id, 10m);
        var plannedRequirements = await GetOrderRequirementsAsync(Client, orderTwo.Id);
        Assert.Equal(2, plannedRequirements.BomRevision);
        Assert.Equal(20m, Assert.Single(plannedRequirements.Materials).RequiredQuantity);
    }

    [Fact]
    public async Task Start_consumes_explicit_allocations_from_multiple_warehouses() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var product = await CreateProductAsync(Client, "P-START", "Start Product");
        var steel = await CreateMaterialAsync(Client, "STEEL-START", "Steel");
        var warehouseA = await CreateWarehouseAsync(Client, "WH-A", "Warehouse A");
        var warehouseB = await CreateWarehouseAsync(Client, "WH-B", "Warehouse B");
        await ReceiveAsync(Client, warehouseA.Id, steel.Id, 70m);
        await ReceiveAsync(Client, warehouseB.Id, steel.Id, 50m);
        var bom = await CreateBomAsync(Client, product.Id, 1m, [
            new BomItemRequest(steel.Id, 2m, null)
        ]);
        await ActivateBomAsync(Client, product.Id, bom.Id);
        var order = await CreateProductionOrderAsync(Client, "PO-START", product.Id, 50m);
        await ReleaseAsync(Client, order.Id);

        var started = await StartAsync(Client, order.Id, [
            new ProductionMaterialAllocationRequest(steel.Id, warehouseA.Id, 60m),
            new ProductionMaterialAllocationRequest(steel.Id, warehouseB.Id, 40m)
        ]);

        Assert.Equal(ProductionOrderStatuses.InProgress, started.Status);
        Assert.NotNull(started.StartedAt);
        var balances = await GetBalancesAsync(Client);
        Assert.Equal(10m, balances.Single(balance => balance.WarehouseId == warehouseA.Id).Quantity);
        Assert.Equal(10m, balances.Single(balance => balance.WarehouseId == warehouseB.Id).Quantity);
        var consumptions = (await GetHistoryAsync(Client)).Items
            .Where(item => item.ReferenceId == order.Id &&
                item.Type == InventoryTransactionType.ProductionConsume)
            .OrderBy(item => item.WarehouseId)
            .ToList();
        Assert.Equal(2, consumptions.Count);
        Assert.All(consumptions, item => {
            Assert.Equal("ProductionOrder", item.ReferenceType);
            Assert.True(item.Quantity > 0);
            Assert.True(item.SignedQuantity < 0);
        });
        Assert.Equal(-60m, consumptions.Single(item => item.WarehouseId == warehouseA.Id).SignedQuantity);
        Assert.Equal(-40m, consumptions.Single(item => item.WarehouseId == warehouseB.Id).SignedQuantity);
    }

    [Fact]
    public async Task Start_rolls_back_earlier_decrement_when_a_later_material_is_insufficient() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var product = await CreateProductAsync(Client, "P-ATOMIC", "Atomic Product");
        var firstCreated = await CreateMaterialAsync(Client, "MAT-ATOMIC-A", "Atomic A");
        var secondCreated = await CreateMaterialAsync(Client, "MAT-ATOMIC-B", "Atomic B");
        var ordered = new[] { firstCreated, secondCreated }.OrderBy(material => material.Id).ToArray();
        var sufficientFirst = ordered[0];
        var insufficientSecond = ordered[1];
        var warehouse = await CreateWarehouseAsync(Client, "WH-ATOMIC", "Atomic Warehouse");
        await ReceiveAsync(Client, warehouse.Id, sufficientFirst.Id, 100m);
        await ReceiveAsync(Client, warehouse.Id, insufficientSecond.Id, 20m);
        var bom = await CreateBomAsync(Client, product.Id, 1m, [
            new BomItemRequest(sufficientFirst.Id, 100m, null),
            new BomItemRequest(insufficientSecond.Id, 50m, null)
        ]);
        await ActivateBomAsync(Client, product.Id, bom.Id);
        var order = await CreateProductionOrderAsync(Client, "PO-ATOMIC", product.Id, 1m);
        await ReleaseAsync(Client, order.Id);

        using var response = await Client.PostAsJsonAsync(StartRoute(order.Id), new StartProductionOrderRequest([
            new ProductionMaterialAllocationRequest(sufficientFirst.Id, warehouse.Id, 100m),
            new ProductionMaterialAllocationRequest(insufficientSecond.Id, warehouse.Id, 50m)
        ]));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var balances = await GetBalancesAsync(Client);
        Assert.Equal(100m, balances.Single(balance => balance.MaterialId == sufficientFirst.Id).Quantity);
        Assert.Equal(20m, balances.Single(balance => balance.MaterialId == insufficientSecond.Id).Quantity);
        Assert.DoesNotContain((await GetHistoryAsync(Client)).Items, item =>
            item.ReferenceId == order.Id && item.Type == InventoryTransactionType.ProductionConsume);
        Assert.Equal(
            ProductionOrderStatuses.Released,
            (await GetOrderAsync(Client, order.Id)).Status);
    }

    [Fact]
    public async Task Concurrent_Start_allows_exactly_one_execution_and_consumes_once() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        using var secondClient = CreateClient();
        await LoginAsync(secondClient, TestData.CompanyAAdminEmail);
        var product = await CreateProductAsync(Client, "P-CONCURRENT", "Concurrent Product");
        var steel = await CreateMaterialAsync(Client, "STEEL-CONCURRENT", "Steel");
        var warehouse = await CreateWarehouseAsync(Client, "WH-CONCURRENT", "Concurrent Warehouse");
        await ReceiveAsync(Client, warehouse.Id, steel.Id, 100m);
        var bom = await CreateBomAsync(Client, product.Id, 1m, [
            new BomItemRequest(steel.Id, 100m, null)
        ]);
        await ActivateBomAsync(Client, product.Id, bom.Id);
        var order = await CreateProductionOrderAsync(Client, "PO-CONCURRENT", product.Id, 1m);
        await ReleaseAsync(Client, order.Id);
        var body = new StartProductionOrderRequest([
            new ProductionMaterialAllocationRequest(steel.Id, warehouse.Id, 100m)
        ]);

        var responses = await Task.WhenAll(
            Client.PostAsJsonAsync(StartRoute(order.Id), body),
            secondClient.PostAsJsonAsync(StartRoute(order.Id), body));

        Assert.Single(responses, response => response.IsSuccessStatusCode);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in responses) response.Dispose();
        Assert.Equal(0m, Assert.Single(await GetBalancesAsync(Client)).Quantity);
        Assert.Equal(ProductionOrderStatuses.InProgress, (await GetOrderAsync(Client, order.Id)).Status);
        var consumptions = (await GetHistoryAsync(Client)).Items.Where(item =>
            item.ReferenceId == order.Id && item.Type == InventoryTransactionType.ProductionConsume);
        Assert.Single(consumptions);
    }

    [Fact]
    public async Task Start_rechecks_stock_because_Release_does_not_reserve_inventory() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var product = await CreateProductAsync(Client, "P-NO-RESERVE", "No Reservation Product");
        var steel = await CreateMaterialAsync(Client, "STEEL-NO-RESERVE", "Steel");
        var warehouse = await CreateWarehouseAsync(Client, "WH-NO-RESERVE", "No Reservation Warehouse");
        await ReceiveAsync(Client, warehouse.Id, steel.Id, 100m);
        var bom = await CreateBomAsync(Client, product.Id, 1m, [
            new BomItemRequest(steel.Id, 100m, null)
        ]);
        await ActivateBomAsync(Client, product.Id, bom.Id);
        var order = await CreateProductionOrderAsync(Client, "PO-NO-RESERVE", product.Id, 1m);
        await ReleaseAsync(Client, order.Id);
        await IssueAsync(Client, warehouse.Id, steel.Id, 20m);

        using var response = await Client.PostAsJsonAsync(StartRoute(order.Id), new StartProductionOrderRequest([
            new ProductionMaterialAllocationRequest(steel.Id, warehouse.Id, 100m)
        ]));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(80m, Assert.Single(await GetBalancesAsync(Client)).Quantity);
        Assert.Equal(ProductionOrderStatuses.Released, (await GetOrderAsync(Client, order.Id)).Status);
        Assert.DoesNotContain((await GetHistoryAsync(Client)).Items, item =>
            item.ReferenceId == order.Id && item.Type == InventoryTransactionType.ProductionConsume);
    }

    [Fact]
    public async Task Start_rejects_invalid_and_cross_tenant_allocations_without_consuming_stock() {
        using var companyAClient = CreateClient();
        using var companyBClient = CreateClient();
        await LoginAsync(companyAClient, TestData.CompanyAAdminEmail);
        await LoginAsync(companyBClient, TestData.CompanyBAdminEmail);
        var product = await CreateProductAsync(companyAClient, "P-INVALID", "Invalid Allocation Product");
        var steel = await CreateMaterialAsync(companyAClient, "STEEL-INVALID", "Steel");
        var plastic = await CreateMaterialAsync(companyAClient, "PLASTIC-INVALID", "Plastic");
        var extra = await CreateMaterialAsync(companyAClient, "EXTRA-INVALID", "Extra");
        var otherTenantMaterial = await CreateMaterialAsync(companyBClient, "MAT-B", "Company B Material");
        var warehouse = await CreateWarehouseAsync(companyAClient, "WH-VALID", "Valid Warehouse");
        var inactiveWarehouse = await CreateWarehouseAsync(companyAClient, "WH-INACTIVE", "Inactive Warehouse");
        var otherTenantWarehouse = await CreateWarehouseAsync(companyBClient, "WH-B", "Company B Warehouse");
        await DeactivateWarehouseAsync(companyAClient, inactiveWarehouse.Id);
        await ReceiveAsync(companyAClient, warehouse.Id, steel.Id, 100m);
        await ReceiveAsync(companyAClient, warehouse.Id, plastic.Id, 100m);
        var bom = await CreateBomAsync(companyAClient, product.Id, 1m, [
            new BomItemRequest(steel.Id, 10m, null),
            new BomItemRequest(plastic.Id, 5m, null)
        ]);
        await ActivateBomAsync(companyAClient, product.Id, bom.Id);
        var order = await CreateProductionOrderAsync(companyAClient, "PO-INVALID", product.Id, 1m);
        await ReleaseAsync(companyAClient, order.Id);

        await AssertStartStatusAsync(companyAClient, order.Id, [
            new(steel.Id, warehouse.Id, 10m)
        ], HttpStatusCode.Conflict);
        await AssertStartStatusAsync(companyAClient, order.Id, [
            new(steel.Id, warehouse.Id, 10m),
            new(plastic.Id, warehouse.Id, 5m),
            new(extra.Id, warehouse.Id, 1m)
        ], HttpStatusCode.Conflict);
        await AssertStartStatusAsync(companyAClient, order.Id, [
            new(steel.Id, warehouse.Id, 9m),
            new(plastic.Id, warehouse.Id, 5m)
        ], HttpStatusCode.Conflict);
        await AssertStartStatusAsync(companyAClient, order.Id, [
            new(steel.Id, warehouse.Id, 11m),
            new(plastic.Id, warehouse.Id, 5m)
        ], HttpStatusCode.Conflict);
        await AssertStartStatusAsync(companyAClient, order.Id, [
            new(steel.Id, warehouse.Id, 0m),
            new(plastic.Id, warehouse.Id, 5m)
        ], HttpStatusCode.BadRequest);
        await AssertStartStatusAsync(companyAClient, order.Id, [
            new(steel.Id, warehouse.Id, -1m),
            new(plastic.Id, warehouse.Id, 5m)
        ], HttpStatusCode.BadRequest);
        await AssertStartStatusAsync(companyAClient, order.Id, [
            new(steel.Id, inactiveWarehouse.Id, 10m),
            new(plastic.Id, warehouse.Id, 5m)
        ], HttpStatusCode.NotFound);
        await AssertStartStatusAsync(companyAClient, order.Id, [
            new(steel.Id, otherTenantWarehouse.Id, 10m),
            new(plastic.Id, warehouse.Id, 5m)
        ], HttpStatusCode.NotFound);
        await AssertStartStatusAsync(companyAClient, order.Id, [
            new(otherTenantMaterial.Id, warehouse.Id, 10m),
            new(plastic.Id, warehouse.Id, 5m)
        ], HttpStatusCode.NotFound);

        Assert.Equal(ProductionOrderStatuses.Released, (await GetOrderAsync(companyAClient, order.Id)).Status);
        Assert.DoesNotContain((await GetHistoryAsync(companyAClient)).Items, item =>
            item.ReferenceId == order.Id && item.Type == InventoryTransactionType.ProductionConsume);
    }

    [Fact]
    public async Task Tenant_cannot_read_or_execute_another_company_Production_Order() {
        using var companyAClient = CreateClient();
        using var companyBClient = CreateClient();
        await LoginAsync(companyAClient, TestData.CompanyAAdminEmail);
        await LoginAsync(companyBClient, TestData.CompanyBAdminEmail);
        var product = await CreateProductAsync(companyAClient, "P-TENANT", "Tenant Product");
        var material = await CreateMaterialAsync(companyAClient, "MAT-TENANT", "Tenant Material");
        var warehouse = await CreateWarehouseAsync(companyAClient, "WH-TENANT", "Tenant Warehouse");
        var bom = await CreateBomAsync(companyAClient, product.Id, 1m, [
            new BomItemRequest(material.Id, 1m, null)
        ]);
        await ActivateBomAsync(companyAClient, product.Id, bom.Id);
        var order = await CreateProductionOrderAsync(companyAClient, "PO-TENANT", product.Id, 1m);

        using var release = await companyBClient.PostAsync(ReleaseRoute(order.Id), null);
        using var start = await companyBClient.PostAsJsonAsync(StartRoute(order.Id), new StartProductionOrderRequest([
            new ProductionMaterialAllocationRequest(material.Id, warehouse.Id, 1m)
        ]));
        using var cancel = await companyBClient.PostAsync(CancelRoute(order.Id), null);
        using var requirements = await companyBClient.GetAsync(RequirementsRoute(order.Id));

        Assert.Equal(HttpStatusCode.NotFound, release.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, start.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, requirements.StatusCode);
        Assert.DoesNotContain(await GetOrdersAsync(companyBClient), candidate => candidate.Id == order.Id);
    }

    [Fact]
    public async Task Lifecycle_commands_allow_only_InProgress_to_complete_and_freeze_Completed_orders() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var product = await CreateProductAsync(Client, "P-LIFECYCLE", "Lifecycle Product");
        var noBomOrder = await CreateProductionOrderAsync(Client, "PO-NO-BOM", product.Id, 1m);
        using (var noBomRelease = await Client.PostAsync(ReleaseRoute(noBomOrder.Id), null)) {
            Assert.Equal(HttpStatusCode.Conflict, noBomRelease.StatusCode);
        }
        var material = await CreateMaterialAsync(Client, "MAT-LIFECYCLE", "Lifecycle Material");
        var warehouse = await CreateWarehouseAsync(Client, "WH-LIFECYCLE", "Lifecycle Warehouse");
        await ReceiveAsync(Client, warehouse.Id, material.Id, 10m);
        var bom = await CreateBomAsync(Client, product.Id, 1m, [
            new BomItemRequest(material.Id, 1m, null)
        ]);
        await ActivateBomAsync(Client, product.Id, bom.Id);

        var editable = await CreateProductionOrderAsync(Client, "PO-EDIT", product.Id, 1m);
        using (var completePlanned = await Client.PostAsJsonAsync(
                   CompleteRoute(editable.Id),
                   new CompleteProductionOrderRequest(warehouse.Id))) {
            Assert.Equal(HttpStatusCode.Conflict, completePlanned.StatusCode);
        }
        using (var update = await Client.PutAsJsonAsync(OrderRoute(editable.Id), new {
            number = "PO-EDITED",
            productId = product.Id,
            quantity = 2m,
            status = ProductionOrderStatuses.Completed
        })) {
            update.EnsureSuccessStatusCode();
            var updated = (await update.Content.ReadFromJsonAsync<ApiResponse<ProductionOrderResponse>>())!.Data!;
            Assert.Equal(ProductionOrderStatuses.Planned, updated.Status);
            Assert.Equal("PO-EDITED", updated.Number);
        }
        using (var delete = await Client.DeleteAsync(OrderRoute(editable.Id))) {
            delete.EnsureSuccessStatusCode();
        }

        var plannedCancelled = await CreateProductionOrderAsync(Client, "PO-CANCEL-PLANNED", product.Id, 1m);
        var cancelledFromPlanned = await CancelAsync(Client, plannedCancelled.Id);
        Assert.Equal(ProductionOrderStatuses.Cancelled, cancelledFromPlanned.Status);
        Assert.NotNull(cancelledFromPlanned.CancelledAt);
        using (var releaseCancelled = await Client.PostAsync(ReleaseRoute(plannedCancelled.Id), null)) {
            Assert.Equal(HttpStatusCode.Conflict, releaseCancelled.StatusCode);
        }
        using (var deleteCancelled = await Client.DeleteAsync(OrderRoute(plannedCancelled.Id))) {
            Assert.Equal(HttpStatusCode.Conflict, deleteCancelled.StatusCode);
        }

        var releasedOrder = await CreateProductionOrderAsync(Client, "PO-CANCEL-RELEASED", product.Id, 1m);
        await ReleaseAsync(Client, releasedOrder.Id);
        using (var completeReleased = await Client.PostAsJsonAsync(
                   CompleteRoute(releasedOrder.Id),
                   new CompleteProductionOrderRequest(warehouse.Id))) {
            Assert.Equal(HttpStatusCode.Conflict, completeReleased.StatusCode);
        }
        using (var secondRelease = await Client.PostAsync(ReleaseRoute(releasedOrder.Id), null)) {
            Assert.Equal(HttpStatusCode.Conflict, secondRelease.StatusCode);
        }
        using (var updateReleased = await Client.PutAsJsonAsync(OrderRoute(releasedOrder.Id),
                   new ProductionOrderRequest("PO-CANNOT-EDIT", product.Id, 2m))) {
            Assert.Equal(HttpStatusCode.Conflict, updateReleased.StatusCode);
        }
        using (var deleteReleased = await Client.DeleteAsync(OrderRoute(releasedOrder.Id))) {
            Assert.Equal(HttpStatusCode.Conflict, deleteReleased.StatusCode);
        }
        Assert.Equal(
            ProductionOrderStatuses.Cancelled,
            (await CancelAsync(Client, releasedOrder.Id)).Status);

        var startedOrder = await CreateProductionOrderAsync(Client, "PO-IN-PROGRESS", product.Id, 1m);
        await ReleaseAsync(Client, startedOrder.Id);
        await StartAsync(Client, startedOrder.Id, [
            new ProductionMaterialAllocationRequest(material.Id, warehouse.Id, 1m)
        ]);
        var completed = await CompleteAsync(Client, startedOrder.Id, warehouse.Id);
        Assert.Equal(ProductionOrderStatuses.Completed, completed.Status);
        Assert.NotNull(completed.CompletedAt);
        using (var secondComplete = await Client.PostAsJsonAsync(
                   CompleteRoute(startedOrder.Id),
                   new CompleteProductionOrderRequest(warehouse.Id))) {
            Assert.Equal(HttpStatusCode.Conflict, secondComplete.StatusCode);
        }
        using (var startCompleted = await Client.PostAsJsonAsync(
                   StartRoute(startedOrder.Id),
                   new StartProductionOrderRequest([
                       new ProductionMaterialAllocationRequest(material.Id, warehouse.Id, 1m)
                   ]))) {
            Assert.Equal(HttpStatusCode.Conflict, startCompleted.StatusCode);
        }
        using (var cancelCompleted = await Client.PostAsync(CancelRoute(startedOrder.Id), null)) {
            Assert.Equal(HttpStatusCode.Conflict, cancelCompleted.StatusCode);
        }
        using (var updateCompleted = await Client.PutAsJsonAsync(
                   OrderRoute(startedOrder.Id),
                   new ProductionOrderRequest("PO-CANNOT-EDIT", product.Id, 2m))) {
            Assert.Equal(HttpStatusCode.Conflict, updateCompleted.StatusCode);
        }
        using (var deleteCompleted = await Client.DeleteAsync(OrderRoute(startedOrder.Id))) {
            Assert.Equal(HttpStatusCode.Conflict, deleteCompleted.StatusCode);
        }
    }

    [Fact]
    public async Task Empty_database_boot_applies_Production_execution_migration_chain() {
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();

        var allMigrations = dbContext.Database.GetMigrations();
        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();

        Assert.Contains(FinishedGoodsMigration, appliedMigrations);
        Assert.Equal(allMigrations, appliedMigrations);
    }

    private static string ProductsRoute => ApiRoutes.Products.Group + ApiRoutes.Products.Root;
    private static string ProductionOrdersRoute =>
        ApiRoutes.ProductionOrders.Group + ApiRoutes.ProductionOrders.Root;

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

    private static string CancelRoute(Guid orderId) => ApiRoutes.ProductionOrders.Group +
        ApiRoutes.ProductionOrders.Cancel.Replace(
            "{productionOrderId:guid}", orderId.ToString(), StringComparison.Ordinal);

    private static string RequirementsRoute(Guid orderId) => ApiRoutes.ProductionOrders.Group +
        ApiRoutes.ProductionOrders.MaterialRequirements.Replace(
            "{productionOrderId:guid}", orderId.ToString(), StringComparison.Ordinal);

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

    private static async Task IssueAsync(
        HttpClient client,
        Guid warehouseId,
        Guid materialId,
        decimal quantity) {
        using var response = await client.PostAsJsonAsync(
            InventoryRoute(ApiRoutes.Inventories.Issue),
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

    private static async Task<ProductionOrderResponse> ReleaseAsync(HttpClient client, Guid orderId) {
        using var response = await client.PostAsync(ReleaseRoute(orderId), null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<ProductionOrderResponse>>())!.Data!;
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

    private static async Task<ProductionOrderResponse> CancelAsync(HttpClient client, Guid orderId) {
        using var response = await client.PostAsync(CancelRoute(orderId), null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<ProductionOrderResponse>>())!.Data!;
    }

    private static async Task AssertStartStatusAsync(
        HttpClient client,
        Guid orderId,
        IReadOnlyList<ProductionMaterialAllocationRequest> allocations,
        HttpStatusCode expectedStatus) {
        using var response = await client.PostAsJsonAsync(
            StartRoute(orderId),
            new StartProductionOrderRequest(allocations));
        Assert.Equal(expectedStatus, response.StatusCode);
    }

    private static async Task<ProductionOrderResponse> GetOrderAsync(HttpClient client, Guid orderId) =>
        (await GetOrdersAsync(client)).Single(order => order.Id == orderId);

    private static async Task<IReadOnlyList<ProductionOrderResponse>> GetOrdersAsync(HttpClient client) {
        var envelope = await client.GetFromJsonAsync<ApiResponse<IReadOnlyList<ProductionOrderResponse>>>(
            ProductionOrdersRoute);
        return envelope?.Data ?? [];
    }

    private static async Task<MaterialRequirementsResponse> GetOrderRequirementsAsync(
        HttpClient client,
        Guid orderId) {
        var envelope = await client.GetFromJsonAsync<ApiResponse<MaterialRequirementsResponse>>(
            RequirementsRoute(orderId));
        return envelope?.Data
            ?? throw new InvalidOperationException("Material requirements response did not contain data.");
    }

    private static async Task<IReadOnlyList<InventoryBalanceResponse>> GetBalancesAsync(HttpClient client) {
        var envelope = await client.GetFromJsonAsync<ApiResponse<IReadOnlyList<InventoryBalanceResponse>>>(
            InventoriesRoute);
        return envelope?.Data ?? [];
    }

    private static async Task<InventoryTransactionPageResponse> GetHistoryAsync(HttpClient client) {
        var envelope = await client.GetFromJsonAsync<ApiResponse<InventoryTransactionPageResponse>>(
            InventoryRoute(ApiRoutes.Inventories.Transactions),
            JsonOptions);
        return envelope?.Data
            ?? throw new InvalidOperationException("Inventory history response did not contain data.");
    }

    private static JsonSerializerOptions CreateJsonOptions() {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
