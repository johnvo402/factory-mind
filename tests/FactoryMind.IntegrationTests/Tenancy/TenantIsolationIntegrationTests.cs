using System.Net;
using System.Net.Http.Json;
using FactoryMind.Api.Endpoints;
using FactoryMind.Application.Features.Machines;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.IntegrationTests.Infrastructure;
using FactoryMind.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace FactoryMind.IntegrationTests.Tenancy;

[Collection(IntegrationTestCollection.Name)]
public sealed class TenantIsolationIntegrationTests(PostgreSqlFixture fixture) : IntegrationTestBase(fixture) {
    [Fact]
    public async Task Lists_and_searches_only_return_current_company_machines() {
        using var companyAClient = CreateClient();
        using var companyBClient = CreateClient();
        await LoginAsync(companyAClient, TestData.CompanyAAdminEmail);
        await LoginAsync(companyBClient, TestData.CompanyBAdminEmail);
        var companyAMachine = await CreateMachineAsync(
            companyAClient,
            "ISO-001",
            "Company A Isolation Machine");
        var companyBMachine = await CreateMachineAsync(
            companyBClient,
            "ISO-001",
            "Company B Isolation Machine");

        var companyAMachines = await GetMachinesAsync(companyAClient, MachinesRoute);
        var companyBMachines = await GetMachinesAsync(companyBClient, MachinesRoute);

        Assert.Contains(companyAMachines, machine => machine.Id == companyAMachine.Id);
        Assert.Contains(companyAMachines, machine => machine.Id == TestData.CompanyAMachineId);
        Assert.DoesNotContain(companyAMachines, machine => machine.Id == companyBMachine.Id);
        Assert.DoesNotContain(companyAMachines, machine => machine.Id == TestData.CompanyBMachineId);
        Assert.Contains(companyBMachines, machine => machine.Id == companyBMachine.Id);
        Assert.Contains(companyBMachines, machine => machine.Id == TestData.CompanyBMachineId);
        Assert.DoesNotContain(companyBMachines, machine => machine.Id == companyAMachine.Id);
        Assert.DoesNotContain(companyBMachines, machine => machine.Id == TestData.CompanyAMachineId);

        var companyASearch = await GetMachinesAsync(companyAClient, MachinesRoute + "?search=ISO-001");
        var companyBSearch = await GetMachinesAsync(companyBClient, MachinesRoute + "?search=ISO-001");
        Assert.Collection(companyASearch, machine => Assert.Equal(companyAMachine.Id, machine.Id));
        Assert.Collection(companyBSearch, machine => Assert.Equal(companyBMachine.Id, machine.Id));
    }

    [Fact]
    public async Task Company_A_cannot_update_or_delete_Company_B_machine_by_id() {
        using var companyAClient = CreateClient();
        using var companyBClient = CreateClient();
        await LoginAsync(companyAClient, TestData.CompanyAAdminEmail);
        await LoginAsync(companyBClient, TestData.CompanyBAdminEmail);
        var companyBMachine = await CreateMachineAsync(
            companyBClient,
            "B-PRIVATE-001",
            "Company B Private Machine");
        var route = MachineByIdRoute(companyBMachine.Id);

        using var updateResponse = await companyAClient.PutAsJsonAsync(
            route,
            new MachineRequest("B-PRIVATE-001", "Leaked Machine", MachineStatuses.Offline));
        using var deleteResponse = await companyAClient.DeleteAsync(route);

        Assert.Equal(HttpStatusCode.NotFound, updateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
        var problem = await updateResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Machine was not found.", problem?.Detail);
        var companyBMachines = await GetMachinesAsync(companyBClient, MachinesRoute);
        var unchangedMachine = Assert.Single(companyBMachines, machine => machine.Id == companyBMachine.Id);
        Assert.Equal("Company B Private Machine", unchangedMachine.Name);
    }

    [Fact]
    public async Task Company_B_cannot_delete_Company_A_machine_by_id() {
        using var companyBClient = CreateClient();
        await LoginAsync(companyBClient, TestData.CompanyBAdminEmail);

        using var response = await companyBClient.DeleteAsync(MachineByIdRoute(TestData.CompanyAMachineId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<MachineResponse> CreateMachineAsync(
        HttpClient client,
        string code,
        string name) {
        using var response = await client.PostAsJsonAsync(
            MachinesRoute,
            new MachineRequest(code, name, MachineStatuses.Available));
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<MachineResponse>>();
        return envelope?.Data ?? throw new InvalidOperationException("Create machine response did not contain data.");
    }

    private static async Task<IReadOnlyList<MachineResponse>> GetMachinesAsync(
        HttpClient client,
        string route) {
        using var response = await client.GetAsync(route);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<List<MachineResponse>>>();
        return envelope?.Data ?? throw new InvalidOperationException("Machine list response did not contain data.");
    }
}
