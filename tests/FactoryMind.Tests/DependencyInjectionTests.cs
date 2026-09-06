using FactoryMind.Application;
using FactoryMind.Application.Features.Auth.Login;
using FactoryMind.Application.Features.Boms;
using FactoryMind.Application.Features.Chat;
using FactoryMind.Application.Features.Chat.Rag;
using FactoryMind.Application.Features.Chat.Tools;
using FactoryMind.Application.Features.Knowledge;
using FactoryMind.Application.Features.Inventories;
using FactoryMind.Application.Features.Machines;
using FactoryMind.Application.Features.Materials;
using FactoryMind.Application.Features.Products;
using FactoryMind.Application.Features.ProductionOrders;
using FactoryMind.Infrastructure;
using FactoryMind.Infrastructure.Persistence.Chat;
using FactoryMind.Infrastructure.Persistence.Boms;
using FactoryMind.Infrastructure.Persistence.Knowledge;
using FactoryMind.Infrastructure.Persistence.Inventories;
using FactoryMind.Infrastructure.Persistence.Machines;
using FactoryMind.Infrastructure.Persistence.Materials;
using FactoryMind.Infrastructure.Persistence.Products;
using FactoryMind.Infrastructure.Persistence.ProductionOrders;
using FactoryMind.Infrastructure.Jobs;
using FactoryMind.Infrastructure.Knowledge;
using FluentValidation;
using Mediator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using FactoryMind.Infrastructure.AI;

namespace FactoryMind.Tests;

public sealed class DependencyInjectionTests {
    [Fact]
    public void Application_registration_adds_Mediator_and_validators() {
        var services = new ServiceCollection();

        services.AddApplication();

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ISender));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IValidator<LoginCommand>));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IKnowledgeContextBuilder)
            && descriptor.ImplementationType == typeof(KnowledgeContextBuilder));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(KnowledgeRetriever));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(MaterialRequirementCalculator));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IIntentRouter)
            && descriptor.ImplementationType == typeof(IntentRouter));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IBusinessContextBuilder)
            && descriptor.ImplementationType == typeof(BusinessContextBuilder));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IChatContextBuilder)
            && descriptor.ImplementationType == typeof(ChatContextBuilder));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IAiToolOrchestrator)
            && descriptor.ImplementationType == typeof(AiToolOrchestrator));
    }

    [Fact]
    public void Infrastructure_registration_adds_feature_repositories() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["ConnectionStrings:FactoryMind"] = "Host=localhost;Database=test"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddInfrastructure(configuration);

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IConversationRepository)
            && descriptor.ImplementationType == typeof(EfConversationRepository));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IBomRepository)
            && descriptor.ImplementationType == typeof(EfBomRepository));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IBusinessContextRepository)
            && descriptor.ImplementationType == typeof(EfBusinessContextRepository));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IDocumentRepository)
            && descriptor.ImplementationType == typeof(EfDocumentRepository));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IKnowledgeSearchRepository)
            && descriptor.ImplementationType == typeof(EfKnowledgeSearchRepository));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IInventoryRepository)
            && descriptor.ImplementationType == typeof(EfInventoryRepository));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IMachineRepository)
            && descriptor.ImplementationType == typeof(EfMachineRepository));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IMaterialRepository)
            && descriptor.ImplementationType == typeof(EfMaterialRepository));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IProductRepository)
            && descriptor.ImplementationType == typeof(EfProductRepository));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IProductionOrderRepository)
            && descriptor.ImplementationType == typeof(EfProductionOrderRepository));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IManufacturingToolRegistry)
            && descriptor.ImplementationType == typeof(ManufacturingToolRegistry));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IAiToolPlanner));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IDocumentProcessingQueue)
            && descriptor.ImplementationType == typeof(HangfireDocumentProcessingQueue));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IDocumentTextExtractor)
            && descriptor.ImplementationType == typeof(PdfPigDocumentTextExtractor));
    }

    [Theory]
    [InlineData("Gemini:ChatTimeoutSeconds", "0", "Gemini ChatTimeoutSeconds must be between 1 and 600.")]
    [InlineData("Gemini:ToolPlanningTimeoutSeconds", "61", "Gemini ToolPlanningTimeoutSeconds must be between 1 and 60.")]
    [InlineData("Gemini:EmbeddingTimeoutSeconds", "301", "Gemini EmbeddingTimeoutSeconds must be between 1 and 300.")]
    public void Infrastructure_registration_validates_AI_timeouts(
        string key,
        string value,
        string expectedMessage) {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["ConnectionStrings:FactoryMind"] = "Host=localhost;Database=test",
                [key] = value
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<GeminiSettings>>().Value);

        Assert.Contains(expectedMessage, exception.Failures);
    }
}
