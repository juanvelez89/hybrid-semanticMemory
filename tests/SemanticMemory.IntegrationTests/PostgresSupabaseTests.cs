using Microsoft.Extensions.DependencyInjection;
using SemanticMemory.Infrastructure;

namespace SemanticMemory.IntegrationTests;

public sealed class PostgresSupabaseTests
{
    [Fact]
    public async Task Postgres_schema_initializer_runs_when_environment_is_configured()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")))
        {
            return;
        }

        var services = new ServiceCollection();
        services.AddInfrastructure();

        await using var provider = services.BuildServiceProvider();
        var initializer = provider.GetRequiredService<PostgresSchemaInitializer>();

        await initializer.InitializeAsync(CancellationToken.None);
    }
}
