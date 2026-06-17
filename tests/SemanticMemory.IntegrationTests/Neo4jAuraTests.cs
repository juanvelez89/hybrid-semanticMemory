using Microsoft.Extensions.DependencyInjection;
using SemanticMemory.Infrastructure;

namespace SemanticMemory.IntegrationTests;

public sealed class Neo4jAuraTests
{
    [Fact]
    public async Task Neo4j_schema_initializer_runs_when_environment_is_configured()
    {
        if (!Neo4jEnvironmentIsConfigured())
        {
            return;
        }

        var services = new ServiceCollection();
        services.AddInfrastructure();

        await using var provider = services.BuildServiceProvider();
        var initializer = provider.GetRequiredService<Neo4jSchemaInitializer>();

        await initializer.InitializeAsync(CancellationToken.None);
    }

    private static bool Neo4jEnvironmentIsConfigured()
    {
        return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NEO4J_URI")) &&
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NEO4J_PASSWORD")) &&
            (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NEO4J_USERNAME")) ||
             !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NEO4J_USER")));
    }
}
