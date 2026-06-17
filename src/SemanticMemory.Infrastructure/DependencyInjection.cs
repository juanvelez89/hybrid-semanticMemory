using Microsoft.Extensions.DependencyInjection;
using Neo4j.Driver;
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

        var neo4jOptions = LoadNeo4jOptions();

        if (neo4jOptions.IsConfigured)
        {
            services.AddSingleton(neo4jOptions);
            services.AddSingleton<IDriver>(_ => GraphDatabase.Driver(
                neo4jOptions.Uri,
                AuthTokens.Basic(neo4jOptions.Username, neo4jOptions.Password)));
            services.AddSingleton<ISemanticGraphStore, Neo4jSemanticGraphStore>();
            services.AddSingleton<Neo4jSchemaInitializer>();
        }
        else
        {
            services.AddSingleton<ISemanticGraphStore, InMemorySemanticGraphStore>();
        }

        services.AddSingleton<IEvidenceStore, InMemoryEvidenceStore>();
        services.AddSingleton<IMemoryEventStore, InMemoryMemoryEventStore>();

        return services;
    }

    private static Neo4jOptions LoadNeo4jOptions()
    {
        return new Neo4jOptions
        {
            Uri = Environment.GetEnvironmentVariable("NEO4J_URI") ?? string.Empty,
            Username = Environment.GetEnvironmentVariable("NEO4J_USERNAME")
                ?? Environment.GetEnvironmentVariable("NEO4J_USER")
                ?? string.Empty,
            Password = Environment.GetEnvironmentVariable("NEO4J_PASSWORD") ?? string.Empty,
            Database = Environment.GetEnvironmentVariable("NEO4J_DATABASE") ?? "neo4j"
        };
    }
}
