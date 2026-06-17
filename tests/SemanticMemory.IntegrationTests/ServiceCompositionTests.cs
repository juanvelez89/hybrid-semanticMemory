using Microsoft.Extensions.DependencyInjection;
using SemanticMemory.Application;
using SemanticMemory.Application.Abstractions;
using SemanticMemory.Infrastructure;

namespace SemanticMemory.IntegrationTests;

public sealed class ServiceCompositionTests
{
    [Fact]
    public void DependencyInjection_resolves_core_services()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IMemoryIngestionService>());
        Assert.NotNull(provider.GetRequiredService<IMemoryRetriever>());
        Assert.NotNull(provider.GetRequiredService<IManualFactService>());
        Assert.NotNull(provider.GetRequiredService<IMemoryForgettingService>());
        Assert.NotNull(provider.GetRequiredService<IMemoryExplanationService>());
    }
}
