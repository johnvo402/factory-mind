using FactoryMind.Application.Features.AiActions;
using FactoryMind.Application.Features.Auth;
using FactoryMind.Application.Features.Boms;
using FactoryMind.Application.Features.Chat;
using FactoryMind.Application.Features.Dashboard;
using FactoryMind.Application.Features.ExcelImports;
using FactoryMind.Application.Features.Knowledge;
using FactoryMind.Application.Features.Inventories;
using FactoryMind.Application.Features.Machines;
using FactoryMind.Application.Features.Materials;
using FactoryMind.Application.Features.Products;
using FactoryMind.Application.Features.ProductInventories;
using FactoryMind.Application.Features.ProductionOrders;
using FactoryMind.Application.Features.Settings;
using FactoryMind.Application.Features.Warehouses;
using FactoryMind.Application.Features.WorkCenters;
using FactoryMind.Application.Features.Routings;
using FactoryMind.Infrastructure.AI;
using FactoryMind.Infrastructure.Jobs;
using FactoryMind.Infrastructure.Knowledge;
using FactoryMind.Infrastructure.Persistence;
using FactoryMind.Infrastructure.Persistence.Auth;
using FactoryMind.Infrastructure.Persistence.Boms;
using FactoryMind.Infrastructure.Persistence.Chat;
using FactoryMind.Infrastructure.Persistence.Dashboard;
using FactoryMind.Infrastructure.Persistence.ExcelImports;
using FactoryMind.Infrastructure.Excel;
using FactoryMind.Infrastructure.Persistence.Knowledge;
using FactoryMind.Infrastructure.Persistence.Inventories;
using FactoryMind.Infrastructure.Persistence.Machines;
using FactoryMind.Infrastructure.Persistence.Materials;
using FactoryMind.Infrastructure.Persistence.Products;
using FactoryMind.Infrastructure.Persistence.ProductInventories;
using FactoryMind.Infrastructure.Persistence.ProductionOrders;
using FactoryMind.Infrastructure.Persistence.Settings;
using FactoryMind.Infrastructure.Persistence.Warehouses;
using FactoryMind.Infrastructure.Persistence.WorkCenters;
using FactoryMind.Infrastructure.Persistence.Routings;
using FactoryMind.Infrastructure.Security;
using FactoryMind.Infrastructure.Storage;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pgvector.EntityFrameworkCore;

namespace FactoryMind.Infrastructure;

public static class DependencyInjection {
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration) {
        var connectionString = configuration.GetConnectionString("FactoryMind")
            ?? "Host=localhost;Port=5432;Database=factorymind;Username=postgres;Password=postgres";

        services.AddDbContext<FactoryMindDbContext>(options => options.UseNpgsql(
            connectionString,
            postgres => postgres.UseVector()));
        services.AddHangfire(configuration => configuration
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString)));
        services.AddHangfireServer(options => {
            options.WorkerCount = 1;
            options.Queues = ["documents"];
        });
        services.AddScoped<FactoryMindDatabaseInitializer>();
        services.Configure<BootstrapAdminSettings>(
            configuration.GetSection(BootstrapAdminSettings.SectionName));
        services.AddScoped<IAuthRepository, EfAuthRepository>();
        services.AddScoped<IBomRepository, EfBomRepository>();
        services.AddScoped<IConversationRepository, EfConversationRepository>();
        services.AddScoped<IAiActionProposalRepository, EfAiActionProposalRepository>();
        services.AddScoped<IBusinessContextRepository, EfBusinessContextRepository>();
        services.AddScoped<IDashboardRepository, EfDashboardRepository>();
        services.AddScoped<IExcelImportRepository, EfExcelImportRepository>();
        services.AddScoped<IDocumentRepository, EfDocumentRepository>();
        services.AddScoped<IKnowledgeSearchRepository, EfKnowledgeSearchRepository>();
        services.AddScoped<IInventoryRepository, EfInventoryRepository>();
        services.AddScoped<IWarehouseRepository, EfWarehouseRepository>();
        services.AddScoped<IWorkCenterRepository, EfWorkCenterRepository>();
        services.AddScoped<IRoutingRepository, EfRoutingRepository>();
        services.AddScoped<IMachineRepository, EfMachineRepository>();
        services.AddScoped<IMaterialRepository, EfMaterialRepository>();
        services.AddScoped<IProductRepository, EfProductRepository>();
        services.AddScoped<IProductInventoryRepository, EfProductInventoryRepository>();
        services.AddScoped<IProductionOrderRepository, EfProductionOrderRepository>();
        services.AddScoped<IProductionExecutionRepository, EfProductionExecutionRepository>();
        services.AddScoped<ISettingsRepository, EfSettingsRepository>();
        services.AddScoped<GetProductionOrderStatusTool>();
        services.AddScoped<GetMachineStatusTool>();
        services.AddScoped<ListMachinesTool>();
        services.AddScoped<GetWorkCenterStatusTool>();
        services.AddScoped<GetMaterialInventoryTool>();
        services.AddScoped<GetProductionOrderMaterialReadinessTool>();
        services.AddScoped<ListProductionOrdersTool>();
        services.AddScoped<IManufacturingToolRegistry, ManufacturingToolRegistry>();
        services.AddSingleton<IAiSettingsReader, GeminiSettingsReader>();
        services.AddScoped<DocumentProcessingJob>();
        services.AddSingleton<IDocumentProcessingQueue, HangfireDocumentProcessingQueue>();
        services.AddSingleton<IDocumentTextExtractor, PdfPigDocumentTextExtractor>();
        services.AddSingleton<IExcelWorkbookReader, ClosedXmlWorkbookReader>();
        services.AddSingleton<ICredentialHasher, CredentialHasher>();
        services.AddOptions<GeminiSettings>()
            .Bind(configuration.GetSection(GeminiSettings.SectionName))
            .Validate(
                settings => settings.ChatTimeoutSeconds is > 0 and <= 600,
                "Gemini ChatTimeoutSeconds must be between 1 and 600.")
            .Validate(
                settings => settings.ToolPlanningTimeoutSeconds is > 0 and <= 60,
                "Gemini ToolPlanningTimeoutSeconds must be between 1 and 60.")
            .Validate(
                settings => settings.EmbeddingTimeoutSeconds is > 0 and <= 300,
                "Gemini EmbeddingTimeoutSeconds must be between 1 and 300.")
            .ValidateOnStart();
        services.PostConfigure<GeminiSettings>(settings => {
            if (string.IsNullOrWhiteSpace(settings.ApiKey)) {
                settings.ApiKey = configuration["GEMINI_API_KEY"] ?? string.Empty;
            }
        });
        services.AddHttpClient<IChatCompletionClient, GeminiChatCompletionClient>();
        services.AddHttpClient<IAiToolPlanner, GeminiAiToolPlanner>();
        services.AddHttpClient<IAiActionPlanner, GeminiAiActionPlanner>();
        services.AddHttpClient<IEmbeddingClient, GeminiEmbeddingClient>();
        services.AddSingleton(TimeProvider.System);
        services.AddOptions<AiActionSettings>()
            .Bind(configuration.GetSection(AiActionSettings.SectionName))
            .Validate(settings => settings.ProposalExpirationMinutes is >= 1 and <= 60,
                "AiActions ProposalExpirationMinutes must be between 1 and 60.")
            .Validate(settings => settings.MaximumPendingProposalsPerUser is >= 1 and <= 20,
                "AiActions MaximumPendingProposalsPerUser must be between 1 and 20.")
            .ValidateOnStart();
        services.AddSingleton(serviceProvider =>
            serviceProvider.GetRequiredService<IOptions<AiActionSettings>>().Value);
        services.Configure<MinioSettings>(configuration.GetSection(MinioSettings.SectionName));
        services.AddSingleton<IFileStorage, MinioFileStorage>();

        return services;
    }

    public static async Task InitializeInfrastructureAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default) {
        using var scope = serviceProvider.CreateScope();
        await scope.ServiceProvider
            .GetRequiredService<FactoryMindDatabaseInitializer>()
            .InitializeAsync(cancellationToken);
    }
}
