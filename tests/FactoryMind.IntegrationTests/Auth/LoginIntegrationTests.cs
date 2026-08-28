using System.Net;
using System.Net.Http.Json;
using FactoryMind.Api.Auth;
using FactoryMind.Api.Routing;
using FactoryMind.Application.Features.Auth.Login;
using FactoryMind.IntegrationTests.Infrastructure;
using FactoryMind.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace FactoryMind.IntegrationTests.Auth;

[Collection(IntegrationTestCollection.Name)]
public sealed class LoginIntegrationTests(PostgreSqlFixture fixture) : IntegrationTestBase(fixture) {
    [Fact]
    public async Task Valid_credentials_return_access_token() {
        using var response = await Client.PostAsJsonAsync(
            AuthRoute(ApiRoutes.Auth.Login),
            new LoginCommand(TestData.CompanyAAdminEmail, TestData.Password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        Assert.True(envelope?.Success);
        Assert.False(string.IsNullOrWhiteSpace(envelope?.Data?.AccessToken));
        Assert.Equal(TestData.CompanyAAdminEmail, envelope?.Data?.User.Email);
        Assert.Equal(TestData.CompanyAId, envelope?.Data?.User.CompanyId);
    }

    [Fact]
    public async Task Invalid_password_returns_expected_authentication_error() {
        using var response = await Client.PostAsJsonAsync(
            AuthRoute(ApiRoutes.Auth.Login),
            new LoginCommand(TestData.CompanyAAdminEmail, "Wrong@Test#2026"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Email or password is incorrect.", problem?.Detail);
    }

    [Fact]
    public async Task Unknown_account_does_not_authenticate() {
        using var response = await Client.PostAsJsonAsync(
            AuthRoute(ApiRoutes.Auth.Login),
            new LoginCommand("unknown@factorymind.test", TestData.Password));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Email or password is incorrect.", problem?.Detail);
    }

    [Fact]
    public async Task Access_token_can_access_protected_endpoint() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);

        using var response = await Client.GetAsync(MachinesRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
