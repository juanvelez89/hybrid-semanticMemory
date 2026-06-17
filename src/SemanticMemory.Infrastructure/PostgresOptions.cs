using Npgsql;

namespace SemanticMemory.Infrastructure;

public sealed class PostgresOptions
{
    public string ConnectionString { get; init; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionString);

    public static PostgresOptions FromEnvironment()
    {
        var rawConnectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING") ?? string.Empty;

        return new PostgresOptions
        {
            ConnectionString = NormalizeConnectionString(rawConnectionString)
        };
    }

    private static string NormalizeConnectionString(string rawConnectionString)
    {
        if (string.IsNullOrWhiteSpace(rawConnectionString))
        {
            return string.Empty;
        }

        if (!rawConnectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !rawConnectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return rawConnectionString;
        }

        var uri = new Uri(rawConnectionString);
        var userInfo = uri.UserInfo.Split(':', 2);
        var username = Uri.UnescapeDataString(userInfo.ElementAtOrDefault(0) ?? string.Empty);
        var password = Uri.UnescapeDataString(userInfo.ElementAtOrDefault(1) ?? string.Empty);
        var database = uri.AbsolutePath.Trim('/');

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = string.IsNullOrWhiteSpace(database) ? "postgres" : database,
            Username = username,
            Password = password,
            SslMode = SslMode.Require,
            Pooling = true
        };

        return builder.ConnectionString;
    }
}
