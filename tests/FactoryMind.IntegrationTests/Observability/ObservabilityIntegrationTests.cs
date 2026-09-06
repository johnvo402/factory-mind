using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FactoryMind.Api.Observability;
using FactoryMind.Api.Routing;
using FactoryMind.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace FactoryMind.IntegrationTests.Observability;

[Collection(IntegrationTestCollection.Name)]
public sealed class ObservabilityIntegrationTests(PostgreSqlFixture fixture)
    : IntegrationTestBase(fixture) {
    [Fact]
    public async Task Valid_correlation_id_is_echoed_and_problem_uses_W3C_trace_id() {
        using var request = new HttpRequestMessage(HttpMethod.Get, MachinesRoute);
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "factory-test-123");

        using var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(
            "factory-test-123",
            Assert.Single(response.Headers.GetValues(CorrelationIdMiddleware.HeaderName)));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        var traceId = Assert.IsType<JsonElement>(problem!.Extensions["traceId"]).GetString();
        Assert.NotNull(traceId);
        Assert.Matches("^[0-9a-f]{32}$", traceId);
    }

    [Fact]
    public async Task Missing_or_unsafe_correlation_id_is_replaced_with_a_safe_value() {
        using var missingResponse = await Client.GetAsync(ApiRoutes.HealthLive);
        var generated = Assert.Single(missingResponse.Headers.GetValues(CorrelationIdMiddleware.HeaderName));
        Assert.True(CorrelationIdMiddleware.IsValid(generated));

        using var invalidRequest = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.HealthLive);
        invalidRequest.Headers.TryAddWithoutValidation(
            CorrelationIdMiddleware.HeaderName,
            new string('x', CorrelationIdMiddleware.MaximumLength + 1));
        using var invalidResponse = await Client.SendAsync(invalidRequest);
        var replacement = Assert.Single(invalidResponse.Headers.GetValues(CorrelationIdMiddleware.HeaderName));
        Assert.True(CorrelationIdMiddleware.IsValid(replacement));
        Assert.NotEqual(new string('x', CorrelationIdMiddleware.MaximumLength + 1), replacement);

        using var malformedRequest = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.HealthLive);
        malformedRequest.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, "bad$value");
        using var malformedResponse = await Client.SendAsync(malformedRequest);
        var malformedReplacement = Assert.Single(
            malformedResponse.Headers.GetValues(CorrelationIdMiddleware.HeaderName));
        Assert.True(CorrelationIdMiddleware.IsValid(malformedReplacement));
        Assert.NotEqual("bad$value", malformedReplacement);
    }

    [Fact]
    public async Task Liveness_is_dependency_free_and_readiness_reports_PostgreSQL_without_secrets() {
        using var liveResponse = await Client.GetAsync(ApiRoutes.HealthLive);
        using var readyResponse = await Client.GetAsync(ApiRoutes.HealthReady);
        using var compatibilityResponse = await Client.GetAsync(ApiRoutes.Health);

        liveResponse.EnsureSuccessStatusCode();
        readyResponse.EnsureSuccessStatusCode();
        compatibilityResponse.EnsureSuccessStatusCode();
        var live = await liveResponse.Content.ReadFromJsonAsync<HealthPayload>();
        var responseText = await readyResponse.Content.ReadAsStringAsync();
        var ready = JsonSerializer.Deserialize<HealthPayload>(
            responseText,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.Equal("Healthy", live!.Status);
        Assert.Empty(live.Checks);
        Assert.Equal("Healthy", ready!.Status);
        var postgresql = Assert.Single(ready.Checks);
        Assert.Equal("postgresql", postgresql.Name);
        Assert.Equal("Healthy", postgresql.Status);
        Assert.True(postgresql.DurationMs >= 0);

        Assert.DoesNotContain("factorymind-integration-password", responseText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ConnectionStrings", responseText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Host=", responseText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", responseText, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record HealthPayload(string Status, IReadOnlyList<HealthCheckPayload> Checks);
    private sealed record HealthCheckPayload(string Name, string Status, double DurationMs);
}
