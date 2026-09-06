using FactoryMind.Api;
using FactoryMind.Api.Endpoints;
using FactoryMind.Api.Observability;
using FactoryMind.Application;
using FactoryMind.Infrastructure;
using FactoryMind.Api.Routing;

var builder = WebApplication.CreateBuilder(args);
builder.AddFactoryMindLogging();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddPresentation(builder.Configuration, builder.Environment);

var app = builder.Build();
await app.Services.InitializeInfrastructureAsync();

app.UseCors();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.MapAuthEndpoints();
app.MapBomEndpoints();
app.MapChatEndpoints();
app.MapDocumentEndpoints();
app.MapDashboardEndpoints();
app.MapExcelImportEndpoints();
app.MapKnowledgeEndpoints();
app.MapInventoryEndpoints();
app.MapMachineEndpoints();
app.MapMaterialEndpoints();
app.MapProductEndpoints();
app.MapProductInventoryEndpoints();
app.MapProductionOrderEndpoints();
app.MapRoutingEndpoints();
app.MapSettingsEndpoints();
app.MapWarehouseEndpoints();
app.MapWorkCenterEndpoints();
app.MapFactoryMindHealthChecks();
app.Run();

public partial class Program;
