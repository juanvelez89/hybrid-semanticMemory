using Microsoft.Extensions.DependencyInjection;
using SemanticMemory.Application.Abstractions;

namespace SemanticMemory.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IApplicationClock, SystemClock>();
        services.AddSingleton<IEmbeddingProvider, FakeEmbeddingProvider>();
        services.AddSingleton<IEntityExtractor, FakeEntityExtractor>();
        services.AddSingleton<IRelationExtractor, FakeRelationExtractor>();
        services.AddSingleton<IEntityNormalizer, SimpleEntityNormalizer>();
        services.AddSingleton<IVectorMemoryStore, InMemoryVectorMemoryStore>();
        services.AddSingleton<ISemanticGraphStore, InMemorySemanticGraphStore>();
        services.AddSingleton<IEvidenceStore, InMemoryEvidenceStore>();
        services.AddSingleton<IMemoryEventStore, InMemoryMemoryEventStore>();

        return services;
    }
}
