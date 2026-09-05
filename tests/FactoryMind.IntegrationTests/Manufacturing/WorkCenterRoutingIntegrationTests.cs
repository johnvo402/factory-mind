using System.Net;
using System.Net.Http.Json;
using FactoryMind.Api.Endpoints;
using FactoryMind.Api.Routing;
using FactoryMind.Application.Features.Products;
using FactoryMind.Application.Features.Routings;
using FactoryMind.Application.Features.WorkCenters;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.IntegrationTests.Infrastructure;
using FactoryMind.Shared.Contracts;

namespace FactoryMind.IntegrationTests.Manufacturing;

[Collection(IntegrationTestCollection.Name)]
public sealed class WorkCenterRoutingIntegrationTests(PostgreSqlFixture fixture)
    : IntegrationTestBase(fixture) {
    [Fact]
    public async Task Work_center_crud_deactivation_and_tenant_isolation_use_real_http() {
        var companyAClient = CreateClient();
        var companyBClient = CreateClient();
        await LoginAsync(companyAClient, TestData.CompanyAAdminEmail);
        await LoginAsync(companyBClient, TestData.CompanyBAdminEmail);
        var created = await CreateWorkCenterAsync(companyAClient, "CNC-01", "CNC", "Cutting");

        using (var crossTenantRead = await companyBClient.GetAsync(WorkCenterRoute(created.Id))) {
            Assert.Equal(HttpStatusCode.NotFound, crossTenantRead.StatusCode);
        }
        using (var update = await companyAClient.PutAsJsonAsync(
                   WorkCenterRoute(created.Id),
                   new WorkCenterUpdateRequest("CNC-02", "CNC Line", "Updated"))) {
            update.EnsureSuccessStatusCode();
            var updated = (await update.Content.ReadFromJsonAsync<ApiResponse<WorkCenterResponse>>())!.Data!;
            Assert.Equal("CNC-02", updated.Code);
            Assert.Equal("CNC Line", updated.Name);
        }
        using (var deactivate = await companyAClient.PostAsync(DeactivateWorkCenterRoute(created.Id), null)) {
            deactivate.EnsureSuccessStatusCode();
            var inactive = (await deactivate.Content.ReadFromJsonAsync<ApiResponse<WorkCenterResponse>>())!.Data!;
            Assert.False(inactive.IsActive);
        }

        var companyAList = await companyAClient.GetFromJsonAsync<ApiResponse<IReadOnlyList<WorkCenterResponse>>>(
            ApiRoutes.WorkCenters.Group);
        var companyBList = await companyBClient.GetFromJsonAsync<ApiResponse<IReadOnlyList<WorkCenterResponse>>>(
            ApiRoutes.WorkCenters.Group);
        Assert.Contains(companyAList!.Data!, item => item.Id == created.Id && !item.IsActive);
        Assert.DoesNotContain(companyBList!.Data!, item => item.Id == created.Id);
    }

    [Fact]
    public async Task Routing_revisions_activate_transactionally_and_archive_the_previous_revision() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var product = await CreateProductAsync(Client, "P-ROUTE", "Routed Product");
        var cutting = await CreateWorkCenterAsync(Client, "WC-CUT", "Cutting", null);
        var assembly = await CreateWorkCenterAsync(Client, "WC-ASM", "Assembly", null);
        var revisionOne = await CreateRoutingAsync(Client, product.Id, [
            new RoutingOperationRequest(10, "Cutting", cutting.Id, 5, 10, null),
            new RoutingOperationRequest(20, "Assembly", assembly.Id, 2, 15, "Sequential assembly")
        ]);
        Assert.Equal(1, revisionOne.Revision);
        Assert.Equal(RoutingStatuses.Draft, revisionOne.Status);
        await ActivateRoutingAsync(Client, product.Id, revisionOne.Id);

        var revisionTwo = await CreateRoutingAsync(Client, product.Id, [
            new RoutingOperationRequest(10, "Precision cutting", cutting.Id, 6, 8, null),
            new RoutingOperationRequest(20, "Assembly", assembly.Id, 2, 12, null)
        ]);
        Assert.Equal(2, revisionTwo.Revision);
        using (var updateDraft = await Client.PutAsJsonAsync(
                   RoutingRoute(product.Id, revisionTwo.Id),
                   new RoutingRequest([
                       new RoutingOperationRequest(10, "Precision cutting", cutting.Id, 4, 8, null),
                       new RoutingOperationRequest(20, "Assembly v2", assembly.Id, 2, 11, null)
                   ]))) {
            Assert.True(updateDraft.IsSuccessStatusCode, await updateDraft.Content.ReadAsStringAsync());
            revisionTwo = (await updateDraft.Content.ReadFromJsonAsync<ApiResponse<RoutingResponse>>())!.Data!;
            Assert.Equal("Assembly v2", revisionTwo.Operations[1].Name);
        }
        await ActivateRoutingAsync(Client, product.Id, revisionTwo.Id);

        var routings = await GetRoutingsAsync(Client, product.Id);
        Assert.Equal(RoutingStatuses.Active, routings.Single(item => item.Id == revisionTwo.Id).Status);
        Assert.Equal(RoutingStatuses.Archived, routings.Single(item => item.Id == revisionOne.Id).Status);
        Assert.Single(routings, item => item.Status == RoutingStatuses.Active);

        using var frozenUpdate = await Client.PutAsJsonAsync(
            RoutingRoute(product.Id, revisionOne.Id),
            new RoutingRequest([new RoutingOperationRequest(10, "Changed", cutting.Id, 0, 0, null)]));
        Assert.Equal(HttpStatusCode.Conflict, frozenUpdate.StatusCode);
    }

    [Fact]
    public async Task Routing_validation_rejects_empty_duplicate_inactive_and_cross_tenant_configuration() {
        var companyAClient = CreateClient();
        var companyBClient = CreateClient();
        await LoginAsync(companyAClient, TestData.CompanyAAdminEmail);
        await LoginAsync(companyBClient, TestData.CompanyBAdminEmail);
        var product = await CreateProductAsync(companyAClient, "P-INVALID-ROUTE", "Invalid Route Product");
        var local = await CreateWorkCenterAsync(companyAClient, "WC-LOCAL", "Local", null);
        var foreign = await CreateWorkCenterAsync(companyBClient, "WC-FOREIGN", "Foreign", null);

        using (var duplicate = await companyAClient.PostAsJsonAsync(RoutingsRoute(product.Id), new RoutingRequest([
                   new RoutingOperationRequest(10, "First", local.Id, 0, 1, null),
                   new RoutingOperationRequest(10, "Duplicate", local.Id, 0, 1, null)
               ]))) {
            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        }
        using (var crossTenant = await companyAClient.PostAsJsonAsync(RoutingsRoute(product.Id), new RoutingRequest([
                   new RoutingOperationRequest(10, "Foreign", foreign.Id, 0, 1, null)
               ]))) {
            Assert.Equal(HttpStatusCode.NotFound, crossTenant.StatusCode);
        }

        var empty = await CreateRoutingAsync(companyAClient, product.Id, []);
        using (var activateEmpty = await companyAClient.PostAsync(
                   ActivateRoutingRoute(product.Id, empty.Id), null)) {
            Assert.Equal(HttpStatusCode.Conflict, activateEmpty.StatusCode);
        }

        var inactiveRouting = await CreateRoutingAsync(companyAClient, product.Id, [
            new RoutingOperationRequest(10, "Inactive center", local.Id, 0, 1, null)
        ]);
        using (var deactivate = await companyAClient.PostAsync(DeactivateWorkCenterRoute(local.Id), null)) {
            deactivate.EnsureSuccessStatusCode();
        }
        using var activateInactive = await companyAClient.PostAsync(
            ActivateRoutingRoute(product.Id, inactiveRouting.Id), null);
        Assert.Equal(HttpStatusCode.Conflict, activateInactive.StatusCode);
    }

    private static string ProductsRoute => ApiRoutes.Products.Group;
    private static string WorkCenterRoute(Guid id) => ApiRoutes.WorkCenters.Group +
        ApiRoutes.WorkCenters.ById.Replace("{workCenterId:guid}", id.ToString(), StringComparison.Ordinal);
    private static string DeactivateWorkCenterRoute(Guid id) => ApiRoutes.WorkCenters.Group +
        ApiRoutes.WorkCenters.Deactivate.Replace("{workCenterId:guid}", id.ToString(), StringComparison.Ordinal);
    private static string RoutingsRoute(Guid productId) => ApiRoutes.Products.Group +
        ApiRoutes.Products.Routings.Replace("{productId:guid}", productId.ToString(), StringComparison.Ordinal);
    private static string RoutingRoute(Guid productId, Guid routingId) => ApiRoutes.Products.Group +
        ApiRoutes.Products.RoutingById
            .Replace("{productId:guid}", productId.ToString(), StringComparison.Ordinal)
            .Replace("{routingId:guid}", routingId.ToString(), StringComparison.Ordinal);
    private static string ActivateRoutingRoute(Guid productId, Guid routingId) => ApiRoutes.Products.Group +
        ApiRoutes.Products.ActivateRouting
            .Replace("{productId:guid}", productId.ToString(), StringComparison.Ordinal)
            .Replace("{routingId:guid}", routingId.ToString(), StringComparison.Ordinal);

    private static async Task<ProductResponse> CreateProductAsync(HttpClient client, string code, string name) {
        using var response = await client.PostAsJsonAsync(ProductsRoute, new ProductRequest(code, name));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<ProductResponse>>())!.Data!;
    }

    private static async Task<WorkCenterResponse> CreateWorkCenterAsync(
        HttpClient client,
        string code,
        string name,
        string? description) {
        using var response = await client.PostAsJsonAsync(
            ApiRoutes.WorkCenters.Group,
            new WorkCenterCreateRequest(code, name, description));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<WorkCenterResponse>>())!.Data!;
    }

    private static async Task<RoutingResponse> CreateRoutingAsync(
        HttpClient client,
        Guid productId,
        IReadOnlyList<RoutingOperationRequest> operations) {
        using var response = await client.PostAsJsonAsync(
            RoutingsRoute(productId), new RoutingRequest(operations));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<RoutingResponse>>())!.Data!;
    }

    private static async Task ActivateRoutingAsync(HttpClient client, Guid productId, Guid routingId) {
        using var response = await client.PostAsync(ActivateRoutingRoute(productId, routingId), null);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<IReadOnlyList<RoutingResponse>> GetRoutingsAsync(
        HttpClient client,
        Guid productId) => (await client.GetFromJsonAsync<ApiResponse<IReadOnlyList<RoutingResponse>>>(
            RoutingsRoute(productId)))!.Data!;
}
