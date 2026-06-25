using SemanticMemory.Infrastructure;

namespace SemanticMemory.UnitTests;

public sealed class Neo4jOptionsTests
{
    [Theory]
    [InlineData("neo4j://9d83d404.databases.neo4j.io", "neo4j+s://9d83d404.databases.neo4j.io")]
    [InlineData("neo4j://9d83d404.databases.neo4j.io:7687", "neo4j+s://9d83d404.databases.neo4j.io:7687")]
    [InlineData("bolt://9d83d404.databases.neo4j.io:7687", "neo4j+s://9d83d404.databases.neo4j.io:7687")]
    [InlineData("neo4j+s://9d83d404.databases.neo4j.io", "neo4j+s://9d83d404.databases.neo4j.io")]
    [InlineData("bolt://localhost:7687", "bolt://localhost:7687")]
    public void NormalizeUri_forces_tls_routing_for_aura_hosts(string rawUri, string expectedUri)
    {
        Assert.Equal(expectedUri, Neo4jOptions.NormalizeUri(rawUri));
    }
}
