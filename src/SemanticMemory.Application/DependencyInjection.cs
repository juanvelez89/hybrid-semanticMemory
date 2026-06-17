using Microsoft.Extensions.DependencyInjection;
using SemanticMemory.Application.Abstractions;
using SemanticMemory.Application.Services;

namespace SemanticMemory.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IMemoryIngestionService, MemoryIngestionService>();
        services.AddScoped<IMemoryRetriever, MemoryRetriever>();
        services.AddScoped<IManualFactService, ManualFactService>();
        services.AddScoped<IMemoryForgettingService, MemoryForgettingService>();
        services.AddScoped<IMemoryExplanationService, MemoryExplanationService>();
        services.AddSingleton<IPromptContextBuilder, PromptContextBuilder>();

        return services;
    }
}
