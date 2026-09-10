using FactoryMind.Api;
using FactoryMind.Api.Endpoints;
using FactoryMind.Api.Observability;
using FactoryMind.Application;
using FactoryMind.Infrastructure;
using FactoryMind.Infrastructure.Persistence;
using FactoryMind.Api.Routing;

var builder = WebApplication.CreateBuilder(args);
builder.AddFactoryMindLogging();
var initializationMode = InfrastructureInitializationModeResolver.Resolve(
    args,
    builder.Environment.EnvironmentName);

if (initializationMode == InfrastructureInitializationMode.Migration) {
    builder.Services.AddInfrastructure(builder.Configuration);
} else {
    builder.Services
        .AddApplication()
        .AddInfrastructure(builder.Configuration)
        .AddPresentation(builder.Configuration, builder.Environment);
}

var app = builder.Build();
await app.Services.InitializeInfrastructureAsync(initializationMode);
if (initializationMode == InfrastructureInitializationMode.Migration) return;

app.UseCors();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.MapAiActionEndpoints();
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
