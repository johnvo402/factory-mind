using System.Net.Http.Headers;
using System.Net.Http.Json;
using FactoryMind.Api.Auth;
using FactoryMind.Api.Routing;
using FactoryMind.Application.Features.Auth.Login;
using FactoryMind.Shared.Contracts;

namespace FactoryMind.IntegrationTests.Infrastructure;

public abstract class IntegrationTestBase(PostgreSqlFixture fixture) : IAsyncLifetime {
    protected HttpClient Client { get; private set; } = null!;

    protected FactoryMindApiFactory ApiFactory => fixture.ApiFactory;

    public async Task InitializeAsync() {
        await fixture.ResetDatabaseAsync();
        Client = CreateClient();
    }

    public Task DisposeAsync() {
        Client.Dispose();
        return Task.CompletedTask;
    }

    protected HttpClient CreateClient() => ApiFactory.CreateClient();

    protected static string AuthRoute(string route) => ApiRoutes.Auth.Group + route;

    protected static string MachinesRoute => ApiRoutes.Machines.Group + ApiRoutes.Machines.Root;

    protected static string MachineByIdRoute(Guid machineId) =>
        ApiRoutes.Machines.Group + ApiRoutes.Machines.ById.Replace(
            "{machineId:guid}",
            machineId.ToString(),
            StringComparison.Ordinal);

    protected static async Task<AuthResponse> LoginAsync(
        HttpClient client,
        string email,
        string password = TestData.Password) {
        using var response = await client.PostAsJsonAsync(
            AuthRoute(ApiRoutes.Auth.Login),
            new LoginCommand(email, password));
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        var auth = envelope?.Data ?? throw new InvalidOperationException("Login response did not contain auth data.");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return auth;
    }

    protected static string GetRefreshCookie(HttpResponseMessage response) {
        var setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), value =>
            value.StartsWith(RefreshTokenCookie.Name + "=", StringComparison.Ordinal));
        return setCookie[..setCookie.IndexOf(';')];
    }

    protected static HttpRequestMessage CreateCookieRequest(HttpMethod method, string route, string cookie) {
        var request = new HttpRequestMessage(method, route);
        request.Headers.Add("Cookie", cookie);
        return request;
    }
}
