using System.Net;
using System.Net.Http.Json;
using FactoryMind.Api.Endpoints;
using FactoryMind.Application.Features.Machines;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.IntegrationTests.Infrastructure;
using FactoryMind.Shared.Contracts;

namespace FactoryMind.IntegrationTests.Manufacturing;

[Collection(IntegrationTestCollection.Name)]
public sealed class MachineIntegrationTests(PostgreSqlFixture fixture) : IntegrationTestBase(fixture) {
    [Fact]
    public async Task Machine_crud_flow_runs_through_real_http_api() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);

        using var createResponse = await Client.PostAsJsonAsync(
            MachinesRoute,
            new MachineRequest("E2E-001", "End-to-end Machine", MachineStatuses.Available));
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var created = await ReadMachineAsync(createResponse);

        var machinesAfterCreate = await GetMachinesAsync();
        Assert.Contains(machinesAfterCreate, machine => machine.Id == created.Id);

        using var updateResponse = await Client.PutAsJsonAsync(
            MachineByIdRoute(created.Id),
            new MachineRequest("E2E-001", "Updated End-to-end Machine", MachineStatuses.Running));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await ReadMachineAsync(updateResponse);
        Assert.Equal("Updated End-to-end Machine", updated.Name);
        Assert.Equal(MachineStatuses.Running, updated.Status);

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
