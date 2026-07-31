using System.Text;
using FactoryMind.Api.Auth;
using FactoryMind.Api.Endpoints;
using FactoryMind.Api.Errors;
using FactoryMind.Application.Common.Authorization;
using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.Auth;
using FactoryMind.Application.Features.Knowledge;
using FactoryMind.Domain.Identity;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.IdentityModel.Tokens;

namespace FactoryMind.Api;

public static class DependencyInjection {
    public static IServiceCollection AddPresentation(
        this IServiceCollection services,
        IConfiguration configuration) {
        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? throw new InvalidOperationException("JWT configuration is missing.");

        services.AddHttpContextAccessor();
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
                context.ProblemDetails.Extensions.TryAdd("traceId", context.HttpContext.TraceIdentifier);
            };
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();
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
}
