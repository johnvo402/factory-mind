using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using FactoryMind.Api.Auth;
using FactoryMind.Api.Endpoints;
using FactoryMind.Api.Errors;
using FactoryMind.Api.Observability;
using FactoryMind.Application.Common.Authorization;
using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.Auth;
using FactoryMind.Application.Features.Knowledge;
using FactoryMind.Domain.Identity;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using FactoryMind.Shared.Observability;

namespace FactoryMind.Api;

public static class DependencyInjection {
    public static WebApplicationBuilder AddFactoryMindLogging(this WebApplicationBuilder builder) {
        builder.Logging.Configure(options => {
            options.ActivityTrackingOptions = ActivityTrackingOptions.TraceId
                | ActivityTrackingOptions.SpanId
                | ActivityTrackingOptions.ParentId;
        });
        if (builder.Environment.IsProduction()) {
            builder.Logging.ClearProviders();
            builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
        }

        return builder;
    }

    public static IServiceCollection AddPresentation(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment) {
        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? throw new InvalidOperationException("JWT configuration is missing.");
        if (environment.IsProduction()
            && (jwtSettings.Key.Length < 32
                || jwtSettings.Key.Contains("development-only", StringComparison.OrdinalIgnoreCase))) {
            throw new InvalidOperationException("A strong production JWT key is required.");
        }

        services.AddHttpContextAccessor();
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddScoped<IPolicyChecker, HttpPolicyChecker>();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<JwtBearerProblemDetailsEvents>();
        services.AddScoped<ChatSseWriter>();
        services.AddSingleton<RefreshTokenCookie>();
        services.AddValidatorsFromAssemblyContaining<UploadDocumentFormValidator>();
        services.Configure<FormOptions>(options => {
            options.MultipartBodyLengthLimit = DocumentUploadConstraints.MaximumRequestSize;
        });
        services.AddProblemDetails(options => {
            options.CustomizeProblemDetails = context => {
                context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
                context.ProblemDetails.Extensions["traceId"] =
                    RequestTraceIdentifier.Get(context.HttpContext);
            };
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddHealthChecks()
            .AddCheck<PostgreSqlHealthCheck>(
                "postgresql",
                failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                tags: ["ready"],
                timeout: TimeSpan.FromSeconds(5));
        services.AddFactoryMindObservability(configuration, environment);
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options => {
            options.EventsType = typeof(JwtBearerProblemDetailsEvents);
            options.TokenValidationParameters = new TokenValidationParameters {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key))
            };
        });
        services.AddAuthorization(options => {
            options.AddPolicy(AuthorizationPolicies.Authenticated, policy => policy.RequireAuthenticatedUser());
            options.AddPolicy(AuthorizationPolicies.Admin, policy => policy.RequireRole(UserRoles.Admin));
            options.AddPolicy(
                AuthorizationPolicies.Manager,
                policy => policy.RequireRole(UserRoles.Admin, UserRoles.Manager));
        });
        services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithOrigins("http://localhost:4200")
            .AllowCredentials()));

        return services;
    }

    private static IServiceCollection AddFactoryMindObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment) {
        var section = configuration.GetSection(ObservabilitySettings.SectionName);
        var settings = section.Get<ObservabilitySettings>() ?? new ObservabilitySettings();
        ValidateObservabilitySettings(settings);
        services.AddOptions<ObservabilitySettings>()
            .Bind(section)
            .Validate(
                value => !string.IsNullOrWhiteSpace(value.ServiceName),
                "Observability ServiceName is required.")
            .Validate(
                value => !value.Otlp.Enabled || IsValidOtlpEndpoint(value.Otlp.Endpoint),
                "Observability OTLP endpoint must be an absolute HTTP or HTTPS URI when enabled.")
            .ValidateOnStart();

        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;
        var version = Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
            ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
            ?? "unknown";
        services.AddOpenTelemetry()
            .ConfigureResource(builder => builder.AddService(
                settings.ServiceName,
                serviceVersion: version).AddAttributes([new KeyValuePair<string, object>(
                    "deployment.environment",
                    environment.EnvironmentName)]))
            .WithTracing(builder => {
                builder.AddSource(FactoryMindTelemetry.ActivitySourceName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
                if (settings.Otlp.Enabled) {
                    builder.AddOtlpExporter(options => options.Endpoint = new Uri(settings.Otlp.Endpoint));
                }
            })
            .WithMetrics(builder => {
                builder.AddMeter(FactoryMindTelemetry.MeterName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
                if (settings.Otlp.Enabled) {
                    builder.AddOtlpExporter(options => options.Endpoint = new Uri(settings.Otlp.Endpoint));
                }
            });
        return services;
    }

    private static void ValidateObservabilitySettings(ObservabilitySettings settings) {
        if (string.IsNullOrWhiteSpace(settings.ServiceName)) {
            throw new InvalidOperationException("Observability ServiceName is required.");
        }

        if (settings.Otlp.Enabled && !IsValidOtlpEndpoint(settings.Otlp.Endpoint)) {
            throw new InvalidOperationException(
                "Observability OTLP endpoint must be an absolute HTTP or HTTPS URI when enabled.");
        }
    }

    private static bool IsValidOtlpEndpoint(string endpoint) =>
        Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
        && (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
}
