using System.Net;
using System.Net.Http.Json;
using FactoryMind.Api.Endpoints;
using FactoryMind.Api.Routing;
using FactoryMind.Application.Features.Machines;
using FactoryMind.Application.Features.WorkCenters;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.IntegrationTests.Infrastructure;
using FactoryMind.Shared.Contracts;
using FactoryMind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryMind.IntegrationTests.Manufacturing;

[Collection(IntegrationTestCollection.Name)]
public sealed class MachineIntegrationTests(PostgreSqlFixture fixture) : IntegrationTestBase(fixture) {
    [Fact]
    public async Task Machine_crud_flow_runs_through_real_http_api() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var workCenter = await PostAsync<WorkCenterResponse>(
            ApiRoutes.WorkCenters.Group,
            new WorkCenterCreateRequest("WC-E2E", "E2E Work Center", null));

        using var createResponse = await Client.PostAsJsonAsync(
            MachinesRoute,
            new MachineRequest(
                "E2E-001", "End-to-end Machine", MachineStatuses.Available, workCenter.Id));
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var created = await ReadMachineAsync(createResponse);
        Assert.Equal(workCenter.Id, created.WorkCenterId);
        Assert.Equal(workCenter.Code, created.WorkCenterCode);
        Assert.Equal(workCenter.Name, created.WorkCenterName);

        var machinesAfterCreate = await GetMachinesAsync();
        Assert.Contains(machinesAfterCreate, machine => machine.Id == created.Id);

        using var updateResponse = await Client.PutAsJsonAsync(
            MachineByIdRoute(created.Id),
            new MachineRequest(
                "E2E-001", "Updated End-to-end Machine", MachineStatuses.Maintenance, workCenter.Id));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await ReadMachineAsync(updateResponse);
        Assert.Equal("Updated End-to-end Machine", updated.Name);
        Assert.Equal(MachineStatuses.Maintenance, updated.Status);

        var machinesAfterUpdate = await GetMachinesAsync();
        var listedUpdate = Assert.Single(machinesAfterUpdate, machine => machine.Id == created.Id);
        Assert.Equal(updated.Name, listedUpdate.Name);
        Assert.Equal(updated.Status, listedUpdate.Status);

        using var deleteResponse = await Client.DeleteAsync(MachineByIdRoute(created.Id));
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var machinesAfterDelete = await GetMachinesAsync();
        Assert.DoesNotContain(machinesAfterDelete, machine => machine.Id == created.Id);
        using var updateDeletedResponse = await Client.PutAsJsonAsync(
            MachineByIdRoute(created.Id),
            new MachineRequest("E2E-001", "Deleted Machine", MachineStatuses.Offline));
        Assert.Equal(HttpStatusCode.NotFound, updateDeletedResponse.StatusCode);
    }

    [Fact]
    public async Task Machine_rejects_manual_running_and_invalid_work_center_assignments() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var workCenter = await PostAsync<WorkCenterResponse>(
            ApiRoutes.WorkCenters.Group,
            new WorkCenterCreateRequest("WC-RULES", "Rules Work Center", null));

        using (var createRunning = await Client.PostAsJsonAsync(
                   MachinesRoute,
                   new MachineRequest("M-RUN-CREATE", "Manual Running", MachineStatuses.Running, workCenter.Id))) {
            Assert.Equal(HttpStatusCode.BadRequest, createRunning.StatusCode);
        }
        using (var missingWorkCenter = await Client.PostAsJsonAsync(
                   MachinesRoute,
                   new MachineRequest("M-MISSING-WC", "Missing WC", MachineStatuses.Available, Guid.NewGuid()))) {
            Assert.Equal(HttpStatusCode.NotFound, missingWorkCenter.StatusCode);
        }

        var machine = await PostAsync<MachineResponse>(
            MachinesRoute,
            new MachineRequest("M-RULES", "Rules Machine", MachineStatuses.Available, workCenter.Id));
        using (var updateRunning = await Client.PutAsJsonAsync(
                   MachineByIdRoute(machine.Id),
                   new MachineRequest(machine.Code, machine.Name, MachineStatuses.Running, workCenter.Id))) {
            Assert.Equal(HttpStatusCode.BadRequest, updateRunning.StatusCode);
        }
        using (var deactivate = await Client.PostAsync(
                   ApiRoutes.WorkCenters.Group + ApiRoutes.WorkCenters.Deactivate.Replace(
                       "{workCenterId:guid}", workCenter.Id.ToString(), StringComparison.Ordinal), null)) {
            deactivate.EnsureSuccessStatusCode();
        }
        using var inactiveWorkCenter = await Client.PutAsJsonAsync(
            MachineByIdRoute(machine.Id),
            new MachineRequest(machine.Code, machine.Name, MachineStatuses.Offline, workCenter.Id));
        Assert.Equal(HttpStatusCode.Conflict, inactiveWorkCenter.StatusCode);
    }

    [Fact]
    public async Task Machine_work_center_assignment_does_not_cross_tenants() {
        using var companyAClient = CreateClient();
        using var companyBClient = CreateClient();
        await LoginAsync(companyAClient, TestData.CompanyAAdminEmail);
        await LoginAsync(companyBClient, TestData.CompanyBAdminEmail);
        var companyBWorkCenter = await PostAsync<WorkCenterResponse>(
            companyBClient,
            ApiRoutes.WorkCenters.Group,
            new WorkCenterCreateRequest("WC-B-PRIVATE", "Private Work Center", null));

        using (var create = await companyAClient.PostAsJsonAsync(
                   MachinesRoute,
                   new MachineRequest(
                       "M-CROSS-CREATE",
                       "Cross Tenant Create",
                       MachineStatuses.Available,
                       companyBWorkCenter.Id))) {
            Assert.Equal(HttpStatusCode.NotFound, create.StatusCode);
        }
        var companyAMachine = await PostAsync<MachineResponse>(
            companyAClient,
            MachinesRoute,
            new MachineRequest("M-CROSS-UPDATE", "Cross Tenant Update", MachineStatuses.Available));
        using var update = await companyAClient.PutAsJsonAsync(
            MachineByIdRoute(companyAMachine.Id),
            new MachineRequest(
                companyAMachine.Code,
                companyAMachine.Name,
                MachineStatuses.Offline,
                companyBWorkCenter.Id));
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
    }

    [Fact]
    public async Task Legacy_running_machine_is_readable_and_can_be_reset_without_an_active_operation() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var machine = await PostAsync<MachineResponse>(
            MachinesRoute,
            new MachineRequest("M-LEGACY-RUN", "Legacy Running", MachineStatuses.Available));
        using (var scope = ApiFactory.Services.CreateScope()) {
            var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
            Assert.Equal(1, await dbContext.Machines
                .Where(candidate => candidate.Id == machine.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(candidate => candidate.Status, MachineStatuses.Running)));
        }

        Assert.Equal(MachineStatuses.Running,
            Assert.Single(await GetMachinesAsync(), candidate => candidate.Id == machine.Id).Status);
        using var update = await Client.PutAsJsonAsync(
            MachineByIdRoute(machine.Id),
            new MachineRequest(machine.Code, machine.Name, MachineStatuses.Offline));
        update.EnsureSuccessStatusCode();
        Assert.Equal(MachineStatuses.Offline, (await ReadMachineAsync(update)).Status);
    }

    private async Task<T> PostAsync<T>(string route, object body) {
        using var response = await Client.PostAsJsonAsync(route, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<T>>())!.Data!;
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string route, object body) {
        using var response = await client.PostAsJsonAsync(route, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<T>>())!.Data!;
    }

    private static async Task<MachineResponse> ReadMachineAsync(HttpResponseMessage response) {
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<MachineResponse>>();
        return envelope?.Data ?? throw new InvalidOperationException("Machine response did not contain data.");
    }

    private async Task<IReadOnlyList<MachineResponse>> GetMachinesAsync() {
        using var response = await Client.GetAsync(MachinesRoute);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<List<MachineResponse>>>();
        return envelope?.Data ?? throw new InvalidOperationException("Machine list response did not contain data.");
    }
}
