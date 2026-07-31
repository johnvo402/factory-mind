using FactoryMind.Application;
using FactoryMind.Application.Features.Auth.Login;
using FactoryMind.Application.Features.Chat;
using FactoryMind.Application.Features.Chat.Rag;
using FactoryMind.Application.Features.Knowledge;
using FactoryMind.Application.Features.Inventories;
using FactoryMind.Application.Features.Machines;
using FactoryMind.Application.Features.Materials;
using FactoryMind.Application.Features.Products;
using FactoryMind.Application.Features.ProductionOrders;
using FactoryMind.Infrastructure;
using FactoryMind.Infrastructure.Persistence.Chat;
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
            descriptor.ServiceType == typeof(IDocumentProcessingQueue)
            && descriptor.ImplementationType == typeof(HangfireDocumentProcessingQueue));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IDocumentTextExtractor)
            && descriptor.ImplementationType == typeof(PdfPigDocumentTextExtractor));
    }
}
