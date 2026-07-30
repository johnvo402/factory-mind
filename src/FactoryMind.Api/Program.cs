using System.Text;
using FactoryMind.Api.Auth;
using FactoryMind.Api.Endpoints;
using FactoryMind.Application.Features.Auth;
using FactoryMind.Application.Features.Auth.Login;
using FactoryMind.Application.Features.Auth.Logout;
using FactoryMind.Application.Features.Auth.Refresh;
using FactoryMind.Infrastructure.Persistence;
using FactoryMind.Infrastructure.Persistence.Auth;
using FactoryMind.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("FactoryMind")
    ?? "Host=localhost;Port=5432;Database=factorymind;Username=postgres;Password=postgres";
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
    ?? throw new InvalidOperationException("JWT configuration is missing.");

builder.Services.AddDbContext<FactoryMindDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<FactoryMindDatabaseInitializer>();
builder.Services.AddScoped<IAuthRepository, EfAuthRepository>();
builder.Services.AddSingleton<ICredentialHasher, CredentialHasher>();
builder.Services.AddScoped<AuthSessionIssuer>();
builder.Services.AddScoped<LoginCommandHandler>();
builder.Services.AddScoped<RefreshTokenCommandHandler>();
builder.Services.AddScoped<LogoutCommandHandler>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer, ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key))
    };
});
builder.Services.AddAuthorization();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.AllowAnyHeader().AllowAnyMethod().WithOrigins("http://localhost:4200")));

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<FactoryMindDatabaseInitializer>().InitializeAsync();
}
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapAuthEndpoints();
app.MapGet("/health", () => Results.Ok(new { success = true, message = "FactoryMind API is running." }));
app.Run();

public partial class Program;
