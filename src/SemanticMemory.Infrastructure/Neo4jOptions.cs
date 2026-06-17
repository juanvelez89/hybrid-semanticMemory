namespace SemanticMemory.Infrastructure;

public sealed class Neo4jOptions
{
    public string Uri { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string Database { get; init; } = "neo4j";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Uri) &&
        !string.IsNullOrWhiteSpace(Username) &&
        !string.IsNullOrWhiteSpace(Password);
}
