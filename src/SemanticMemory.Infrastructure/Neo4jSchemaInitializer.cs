using Neo4j.Driver;

namespace SemanticMemory.Infrastructure;

public sealed class Neo4jSchemaInitializer(
    IDriver driver,
    Neo4jOptions options)
{
    private static readonly string[] SchemaStatements =
    [
        """
        CREATE CONSTRAINT semantic_node_identity IF NOT EXISTS
        FOR (n:SemanticNode)
        REQUIRE (n.tenantId, n.userId, n.type, n.normalizedKey) IS UNIQUE
        """,
        """
        CREATE INDEX semantic_node_search IF NOT EXISTS
        FOR (n:SemanticNode)
        ON (n.tenantId, n.userId, n.status, n.canonicalName)
        """,
        """
        CREATE INDEX semantic_edge_identity IF NOT EXISTS
        FOR ()-[r:SEMANTIC_EDGE]-()
        ON (r.tenantId, r.userId, r.status, r.relationType)
        """
    ];

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await driver.VerifyConnectivityAsync();

        await using var session = driver.AsyncSession(builder => builder.WithDatabase(options.Database));

        foreach (var statement in SchemaStatements)
        {
            await session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync(statement);
            });
        }
    }
}
