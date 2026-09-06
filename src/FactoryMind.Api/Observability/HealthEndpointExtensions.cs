using FactoryMind.Api.Routing;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace FactoryMind.Api.Observability;

public static class HealthEndpointExtensions {
    public static IEndpointRouteBuilder MapFactoryMindHealthChecks(this IEndpointRouteBuilder endpoints) {
        endpoints.MapHealthChecks(ApiRoutes.HealthLive, new HealthCheckOptions {
            Predicate = _ => false,
            ResponseWriter = HealthResponseWriter.WriteAsync
        });
        var readinessOptions = new HealthCheckOptions {
            Predicate = registration => registration.Tags.Contains("ready"),
            ResponseWriter = HealthResponseWriter.WriteAsync
        };
        endpoints.MapHealthChecks(ApiRoutes.HealthReady, readinessOptions);
        endpoints.MapHealthChecks(ApiRoutes.Health, readinessOptions);
        return endpoints;
    }
}
