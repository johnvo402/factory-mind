using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FactoryMind.Api.Auth;
using FactoryMind.Api.Routing;
using FactoryMind.Application.Features.Auth.Login;
using FactoryMind.IntegrationTests.Infrastructure;
using FactoryMind.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace FactoryMind.IntegrationTests.Auth;

[Collection(IntegrationTestCollection.Name)]
public sealed class RefreshTokenIntegrationTests(PostgreSqlFixture fixture) : IntegrationTestBase(fixture) {
    [Fact]
    public async Task Login_issues_scoped_http_only_refresh_cookie() {
        using var response = await LoginResponseAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith(RefreshTokenCookie.Name + "=", setCookie, StringComparison.Ordinal);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=" + ApiRoutes.Auth.Group, setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Refresh_rotates_cookie_and_returns_working_access_token() {
        using var loginResponse = await LoginResponseAsync();
        var originalCookie = GetRefreshCookie(loginResponse);
        using var refreshRequest = CreateCookieRequest(
            HttpMethod.Post,
            AuthRoute(ApiRoutes.Auth.Refresh),
            originalCookie);

        using var refreshResponse = await Client.SendAsync(refreshRequest);

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var rotatedCookie = GetRefreshCookie(refreshResponse);
        Assert.NotEqual(originalCookie, rotatedCookie);
        var envelope = await refreshResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        var accessToken = envelope?.Data?.AccessToken;
        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var protectedResponse = await Client.GetAsync(MachinesRoute);
        Assert.Equal(HttpStatusCode.OK, protectedResponse.StatusCode);
    }

    [Fact]
    public async Task Rotated_refresh_token_cannot_be_reused() {
        using var loginResponse = await LoginResponseAsync();
        var originalCookie = GetRefreshCookie(loginResponse);
        using var firstRefreshRequest = CreateCookieRequest(
            HttpMethod.Post,
            AuthRoute(ApiRoutes.Auth.Refresh),
            originalCookie);
        using var firstRefreshResponse = await Client.SendAsync(firstRefreshRequest);
        Assert.Equal(HttpStatusCode.OK, firstRefreshResponse.StatusCode);

        using var reusedTokenRequest = CreateCookieRequest(
            HttpMethod.Post,
            AuthRoute(ApiRoutes.Auth.Refresh),
            originalCookie);
        using var reusedTokenResponse = await Client.SendAsync(reusedTokenRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, reusedTokenResponse.StatusCode);
        var problem = await reusedTokenResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Refresh token is invalid or expired.", problem?.Detail);
    }

    [Fact]
    public async Task Logout_revokes_refresh_token_and_clears_cookie() {
        using var loginResponse = await LoginResponseAsync();
        var refreshCookie = GetRefreshCookie(loginResponse);
        using var logoutRequest = CreateCookieRequest(
            HttpMethod.Post,
            AuthRoute(ApiRoutes.Auth.Logout),
            refreshCookie);

        using var logoutResponse = await Client.SendAsync(logoutRequest);

        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);
        Assert.Contains(logoutResponse.Headers.GetValues("Set-Cookie"), value =>
            value.StartsWith(RefreshTokenCookie.Name + "=", StringComparison.Ordinal)
            && value.Contains("expires=", StringComparison.OrdinalIgnoreCase));

        using var refreshRequest = CreateCookieRequest(
            HttpMethod.Post,
            AuthRoute(ApiRoutes.Auth.Refresh),
            refreshCookie);
        using var refreshResponse = await Client.SendAsync(refreshRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    private Task<HttpResponseMessage> LoginResponseAsync() => Client.PostAsJsonAsync(
        AuthRoute(ApiRoutes.Auth.Login),
        new LoginCommand(TestData.CompanyAAdminEmail, TestData.Password));
}
