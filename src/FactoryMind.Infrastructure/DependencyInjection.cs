using FactoryMind.Application.Features.Auth;
using FactoryMind.Application.Features.Chat;
using FactoryMind.Application.Features.Knowledge;
using FactoryMind.Infrastructure.AI;
using FactoryMind.Infrastructure.Jobs;
using FactoryMind.Infrastructure.Knowledge;
using FactoryMind.Infrastructure.Persistence;
using FactoryMind.Infrastructure.Persistence.Auth;
using FactoryMind.Infrastructure.Persistence.Chat;
using FactoryMind.Infrastructure.Persistence.Knowledge;
using FactoryMind.Infrastructure.Security;
using FactoryMind.Infrastructure.Storage;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryMind.Infrastructure;

public static class DependencyInjection {
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration) {
        var connectionString = configuration.GetConnectionString("FactoryMind")
            ?? "Host=localhost;Port=5432;Database=factorymind;Username=postgres;Password=postgres";

        services.AddDbContext<FactoryMindDbContext>(options => options.UseNpgsql(connectionString));
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
        services.AddScoped<IDocumentRepository, EfDocumentRepository>();
        services.AddScoped<DocumentProcessingJob>();
        services.AddSingleton<IDocumentProcessingQueue, HangfireDocumentProcessingQueue>();
        services.AddSingleton<IDocumentTextExtractor, PdfPigDocumentTextExtractor>();
        services.AddSingleton<ICredentialHasher, CredentialHasher>();
        services.Configure<OpenAiSettings>(configuration.GetSection(OpenAiSettings.SectionName));
        services.AddHttpClient<IChatCompletionClient, OpenAiChatCompletionClient>();
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
