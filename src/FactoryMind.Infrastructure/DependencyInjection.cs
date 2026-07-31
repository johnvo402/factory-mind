using FactoryMind.Application.Features.Auth;
using FactoryMind.Application.Features.Chat;
using FactoryMind.Application.Features.Dashboard;
using FactoryMind.Application.Features.Knowledge;
using FactoryMind.Application.Features.Inventories;
using FactoryMind.Application.Features.Machines;
using FactoryMind.Application.Features.Materials;
using FactoryMind.Application.Features.Products;
using FactoryMind.Application.Features.ProductionOrders;
using FactoryMind.Infrastructure.AI;
using FactoryMind.Infrastructure.Jobs;
using FactoryMind.Infrastructure.Knowledge;
using FactoryMind.Infrastructure.Persistence;
using FactoryMind.Infrastructure.Persistence.Auth;
using FactoryMind.Infrastructure.Persistence.Chat;
using FactoryMind.Infrastructure.Persistence.Dashboard;
using FactoryMind.Infrastructure.Persistence.Knowledge;
using FactoryMind.Infrastructure.Persistence.Inventories;
using FactoryMind.Infrastructure.Persistence.Machines;
using FactoryMind.Infrastructure.Persistence.Materials;
using FactoryMind.Infrastructure.Persistence.Products;
using FactoryMind.Infrastructure.Persistence.ProductionOrders;
using FactoryMind.Infrastructure.Security;
using FactoryMind.Infrastructure.Storage;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        services.AddScoped<IAuthRepository, EfAuthRepository>();
        services.AddScoped<IConversationRepository, EfConversationRepository>();
        services.AddScoped<IBusinessContextRepository, EfBusinessContextRepository>();
        services.AddScoped<IDashboardRepository, EfDashboardRepository>();
        services.AddScoped<IDocumentRepository, EfDocumentRepository>();
        services.AddScoped<IKnowledgeSearchRepository, EfKnowledgeSearchRepository>();
        services.AddScoped<IInventoryRepository, EfInventoryRepository>();
        services.AddScoped<IMachineRepository, EfMachineRepository>();
        services.AddScoped<IMaterialRepository, EfMaterialRepository>();
        services.AddScoped<IProductRepository, EfProductRepository>();
        services.AddScoped<IProductionOrderRepository, EfProductionOrderRepository>();
        services.AddScoped<DocumentProcessingJob>();
        services.AddSingleton<IDocumentProcessingQueue, HangfireDocumentProcessingQueue>();
        services.AddSingleton<IDocumentTextExtractor, PdfPigDocumentTextExtractor>();
        services.AddSingleton<ICredentialHasher, CredentialHasher>();
        services.Configure<GeminiSettings>(configuration.GetSection(GeminiSettings.SectionName));
        services.PostConfigure<GeminiSettings>(settings => {
            if (string.IsNullOrWhiteSpace(settings.ApiKey)) {
                settings.ApiKey = configuration["GEMINI_API_KEY"] ?? string.Empty;
            }
        });
        services.AddHttpClient<IChatCompletionClient, GeminiChatCompletionClient>();
        services.AddHttpClient<IEmbeddingClient, GeminiEmbeddingClient>();
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
