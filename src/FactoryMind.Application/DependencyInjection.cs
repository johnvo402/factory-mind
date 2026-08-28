using FactoryMind.Application.Common.Behaviors;
using FactoryMind.Application.Features.Auth;
using FactoryMind.Application.Features.Auth.Login;
using FactoryMind.Application.Features.Boms;
using FactoryMind.Application.Features.Chat;
using FactoryMind.Application.Features.Chat.Rag;
using FactoryMind.Application.Features.Knowledge;
using FluentValidation;
using Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryMind.Application;

public static class DependencyInjection {
    public static IServiceCollection AddApplication(this IServiceCollection services) {
        services.AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped);
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
        services.AddValidatorsFromAssemblyContaining<LoginCommandValidator>();
        services.AddScoped<AuthSessionIssuer>();
        services.AddSingleton<MaterialRequirementCalculator>();
        services.AddSingleton<DocumentChunker>();
        services.AddScoped<KnowledgeRetriever>();
        services.AddScoped<IKnowledgeContextBuilder, KnowledgeContextBuilder>();
        services.AddScoped<IIntentRouter, IntentRouter>();
        services.AddScoped<IBusinessContextBuilder, BusinessContextBuilder>();
        services.AddScoped<IChatContextBuilder, ChatContextBuilder>();

        return services;
    }
}
