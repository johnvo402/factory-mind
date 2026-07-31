using FactoryMind.Api;
using FactoryMind.Api.Endpoints;
using FactoryMind.Application;
using FactoryMind.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddPresentation(builder.Configuration);

var app = builder.Build();
await app.Services.InitializeInfrastructureAsync();

app.UseCors();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.MapAuthEndpoints();
app.MapChatEndpoints();
app.MapDocumentEndpoints();
app.MapGet("/health", () => Results.Ok(new { success = true, message = "FactoryMind API is running." }));
app.Run();

public partial class Program;
