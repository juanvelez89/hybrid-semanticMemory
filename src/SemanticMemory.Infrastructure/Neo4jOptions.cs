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

    public static string NormalizeUri(string rawUri)
    {
        if (string.IsNullOrWhiteSpace(rawUri))
        {
            return string.Empty;
        }

        var trimmedUri = rawUri.Trim();

        if (!System.Uri.TryCreate(trimmedUri, UriKind.Absolute, out var uri))
        {
            return trimmedUri;
        }

        if (!uri.Host.EndsWith(".databases.neo4j.io", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(uri.Scheme, "neo4j+s", StringComparison.OrdinalIgnoreCase))
        {
            return trimmedUri;
        }

        var port = uri.IsDefaultPort || uri.Port <= 0 ? string.Empty : $":{uri.Port}";
        var path = uri.PathAndQuery == "/" ? string.Empty : uri.PathAndQuery;

        return $"neo4j+s://{uri.Host}{port}{path}";
    }
}
