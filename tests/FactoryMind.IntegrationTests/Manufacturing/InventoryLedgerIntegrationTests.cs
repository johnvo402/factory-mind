using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FactoryMind.Api.Endpoints;
using FactoryMind.Api.Routing;
using FactoryMind.Application.Features.Inventories;
using FactoryMind.Application.Features.Materials;
using FactoryMind.Application.Features.Warehouses;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.IntegrationTests.Infrastructure;
using FactoryMind.Shared.Contracts;

namespace FactoryMind.IntegrationTests.Manufacturing;

[Collection(IntegrationTestCollection.Name)]
public sealed class InventoryLedgerIntegrationTests(PostgreSqlFixture fixture)
    : IntegrationTestBase(fixture) {
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    [Fact]
    public async Task Receive_issue_and_history_explain_current_balance() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var material = await CreateMaterialAsync(Client, "MAT-STEEL", "Steel");
        var warehouse = await CreateWarehouseAsync(Client, "WH-RAW", "Raw Materials");

        await PostSuccessfulAsync(Client, ApiRoutes.Inventories.Receive, new InventoryMovementRequest(
            warehouse.Id, material.Id, 100m, "Initial receipt", null, null));
        var afterReceipt = Assert.Single(await GetBalancesAsync(Client));
        Assert.Equal(100m, afterReceipt.Quantity);

        await PostSuccessfulAsync(Client, ApiRoutes.Inventories.Issue, new InventoryMovementRequest(
            warehouse.Id, material.Id, 25m, "Production issue", null, null));
        var afterIssue = Assert.Single(await GetBalancesAsync(Client));
        Assert.Equal(75m, afterIssue.Quantity);

        var history = await GetHistoryAsync(Client);
        Assert.Equal(2, history.TotalCount);
        Assert.Contains(history.Items, transaction =>
            transaction.Type == InventoryTransactionType.Receipt && transaction.SignedQuantity == 100m);
        Assert.Contains(history.Items, transaction =>
            transaction.Type == InventoryTransactionType.Issue && transaction.SignedQuantity == -25m);
    }

    [Fact]
    public async Task Transfer_updates_both_warehouses_and_creates_correlated_entries() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var material = await CreateMaterialAsync(Client, "MAT-PP", "Polypropylene");
        var source = await CreateWarehouseAsync(Client, "WH-A", "Warehouse A");
        var destination = await CreateWarehouseAsync(Client, "WH-B", "Warehouse B");
        await PostSuccessfulAsync(Client, ApiRoutes.Inventories.Receive, new InventoryMovementRequest(
            source.Id, material.Id, 100m, null, null, null));

        await PostSuccessfulAsync(Client, ApiRoutes.Inventories.Transfer, new InventoryTransferRequest(
            source.Id, destination.Id, material.Id, 30m, "Replenishment", null));

        var balances = await GetBalancesAsync(Client);
        Assert.Equal(70m, balances.Single(balance => balance.WarehouseId == source.Id).Quantity);
        Assert.Equal(30m, balances.Single(balance => balance.WarehouseId == destination.Id).Quantity);
        var transfers = (await GetHistoryAsync(Client)).Items
            .Where(transaction => transaction.Type is
                InventoryTransactionType.TransferOut or InventoryTransactionType.TransferIn)
            .ToList();
        Assert.Equal(2, transfers.Count);
        Assert.NotNull(transfers[0].ReferenceId);
        Assert.Equal(transfers[0].ReferenceId, transfers[1].ReferenceId);
    }

    [Fact]
    public async Task Insufficient_issue_preserves_balance_and_ledger() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var material = await CreateMaterialAsync(Client, "MAT-COPPER", "Copper");
        var warehouse = await CreateWarehouseAsync(Client, "WH-A", "Warehouse A");
        await PostSuccessfulAsync(Client, ApiRoutes.Inventories.Receive, new InventoryMovementRequest(
            warehouse.Id, material.Id, 10m, null, null, null));

        using var response = await Client.PostAsJsonAsync(
            InventoryRoute(ApiRoutes.Inventories.Issue),
            new InventoryMovementRequest(warehouse.Id, material.Id, 20m, null, null, null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(10m, Assert.Single(await GetBalancesAsync(Client)).Quantity);
        var history = await GetHistoryAsync(Client);
        Assert.Equal(1, history.TotalCount);
        Assert.Equal(InventoryTransactionType.Receipt, Assert.Single(history.Items).Type);
    }

    [Fact]
    public async Task Tenant_cannot_view_or_mutate_another_company_inventory() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var material = await CreateMaterialAsync(Client, "MAT-A", "Company A Material");
        var warehouse = await CreateWarehouseAsync(Client, "WH-A", "Company A Warehouse");
        await PostSuccessfulAsync(Client, ApiRoutes.Inventories.Receive, new InventoryMovementRequest(
            warehouse.Id, material.Id, 50m, null, null, null));

        using var companyBClient = CreateClient();
        await LoginAsync(companyBClient, TestData.CompanyBAdminEmail);
        Assert.Empty(await GetBalancesAsync(companyBClient));
        using var issue = await companyBClient.PostAsJsonAsync(
            InventoryRoute(ApiRoutes.Inventories.Issue),
            new InventoryMovementRequest(warehouse.Id, material.Id, 1m, null, null, null));
        Assert.Equal(HttpStatusCode.NotFound, issue.StatusCode);

        Assert.Equal(50m, Assert.Single(await GetBalancesAsync(Client)).Quantity);
        Assert.Equal(1, (await GetHistoryAsync(Client)).TotalCount);
    }

    [Fact]
    public async Task Concurrent_issues_cannot_overdraw_stock() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var material = await CreateMaterialAsync(Client, "MAT-AL", "Aluminium");
        var warehouse = await CreateWarehouseAsync(Client, "WH-A", "Warehouse A");
        await PostSuccessfulAsync(Client, ApiRoutes.Inventories.Receive, new InventoryMovementRequest(
            warehouse.Id, material.Id, 10m, null, null, null));
        using var secondClient = CreateClient();
        await LoginAsync(secondClient, TestData.CompanyAAdminEmail);
        var request = new InventoryMovementRequest(warehouse.Id, material.Id, 8m, null, null, null);

        var responses = await Task.WhenAll(
            Client.PostAsJsonAsync(InventoryRoute(ApiRoutes.Inventories.Issue), request),
            secondClient.PostAsJsonAsync(InventoryRoute(ApiRoutes.Inventories.Issue), request));

        Assert.Single(responses, response => response.IsSuccessStatusCode);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in responses) response.Dispose();
        Assert.Equal(2m, Assert.Single(await GetBalancesAsync(Client)).Quantity);
        Assert.Equal(2, (await GetHistoryAsync(Client)).TotalCount);
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

    private static async Task PostSuccessfulAsync(HttpClient client, string route, object body) {
        using var response = await client.PostAsJsonAsync(InventoryRoute(route), body);
        response.EnsureSuccessStatusCode();
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
        return envelope?.Data ?? throw new InvalidOperationException("History response did not contain data.");
    }

    private static JsonSerializerOptions CreateJsonOptions() {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
