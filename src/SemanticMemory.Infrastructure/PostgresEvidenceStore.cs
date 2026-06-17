using Npgsql;
using SemanticMemory.Application.Abstractions;
using SemanticMemory.Domain;

namespace SemanticMemory.Infrastructure;

public sealed class PostgresEvidenceStore(NpgsqlDataSource dataSource) : IEvidenceStore
{
    public async Task SaveEvidenceAsync(
        Evidence evidence,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            INSERT INTO evidence (
                id,
                tenant_id,
                user_id,
                edge_id,
                memory_chunk_id,
                quote,
                source_type,
                confidence,
                created_at
            )
            VALUES (
                @id,
                @tenant_id,
                @user_id,
                @edge_id,
                @memory_chunk_id,
                @quote,
                @source_type,
                @confidence,
                @created_at
            )
            ON CONFLICT (id) DO NOTHING;
            """;

        await using var command = dataSource.CreateCommand(sql);
        AddEvidenceParameters(command, evidence);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task<IReadOnlyList<Evidence>> GetEvidenceForEdgeAsync(
        string tenantId,
        string userId,
        Guid edgeId,
        CancellationToken cancellationToken)
    {
        return QueryEvidenceAsync(
            """
            SELECT *
            FROM evidence
            WHERE tenant_id = @tenant_id
              AND user_id = @user_id
              AND edge_id = @edge_id
            ORDER BY confidence DESC;
            """,
            command =>
            {
                command.Parameters.AddWithValue("tenant_id", tenantId);
                command.Parameters.AddWithValue("user_id", userId);
                command.Parameters.AddWithValue("edge_id", edgeId);
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<Evidence>> GetEvidenceForEdgesAsync(
        string tenantId,
        string userId,
        IReadOnlyList<Guid> edgeIds,
        CancellationToken cancellationToken)
    {
        if (edgeIds.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<Evidence>>([]);
        }

        return QueryEvidenceAsync(
            """
            SELECT *
            FROM evidence
            WHERE tenant_id = @tenant_id
              AND user_id = @user_id
              AND edge_id = ANY(@edge_ids)
            ORDER BY confidence DESC;
            """,
            command =>
            {
                command.Parameters.AddWithValue("tenant_id", tenantId);
                command.Parameters.AddWithValue("user_id", userId);
                command.Parameters.AddWithValue("edge_ids", edgeIds.ToArray());
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<Evidence>> GetEvidenceForMemoryChunksAsync(
        string tenantId,
        string userId,
        IReadOnlyList<Guid> memoryChunkIds,
        CancellationToken cancellationToken)
    {
        if (memoryChunkIds.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<Evidence>>([]);
        }

        return QueryEvidenceAsync(
            """
            SELECT *
            FROM evidence
            WHERE tenant_id = @tenant_id
              AND user_id = @user_id
              AND memory_chunk_id = ANY(@memory_chunk_ids)
            ORDER BY confidence DESC;
            """,
            command =>
            {
                command.Parameters.AddWithValue("tenant_id", tenantId);
                command.Parameters.AddWithValue("user_id", userId);
                command.Parameters.AddWithValue("memory_chunk_ids", memoryChunkIds.ToArray());
            },
            cancellationToken);
    }

    private async Task<IReadOnlyList<Evidence>> QueryEvidenceAsync(
        string sql,
        Action<NpgsqlCommand> configure,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(sql);
        configure(command);

        var results = new List<Evidence>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(MapEvidence(reader));
        }

        return results;
    }

    private static void AddEvidenceParameters(NpgsqlCommand command, Evidence evidence)
    {
        command.Parameters.AddWithValue("id", evidence.Id);
        command.Parameters.AddWithValue("tenant_id", evidence.TenantId);
        command.Parameters.AddWithValue("user_id", evidence.UserId);
        command.Parameters.AddWithValue("edge_id", evidence.EdgeId);
        command.Parameters.AddWithValue("memory_chunk_id", evidence.MemoryChunkId);
        command.Parameters.AddWithValue("quote", (object?)evidence.Quote ?? DBNull.Value);
        command.Parameters.AddWithValue("source_type", evidence.SourceType.ToDbString());
        command.Parameters.AddWithValue("confidence", evidence.Confidence);
        command.Parameters.AddWithValue("created_at", evidence.CreatedAt);
    }

    private static Evidence MapEvidence(NpgsqlDataReader reader)
    {
        return new Evidence
        {
            Id = reader.GetGuid(reader.GetOrdinal("id")),
            TenantId = reader.GetString(reader.GetOrdinal("tenant_id")),
            UserId = reader.GetString(reader.GetOrdinal("user_id")),
            EdgeId = reader.GetGuid(reader.GetOrdinal("edge_id")),
            MemoryChunkId = reader.GetGuid(reader.GetOrdinal("memory_chunk_id")),
            Quote = reader.IsDBNull(reader.GetOrdinal("quote")) ? null : reader.GetString(reader.GetOrdinal("quote")),
            SourceType = PostgresSerialization.ParseSourceType(reader.GetString(reader.GetOrdinal("source_type"))),
            Confidence = reader.GetDouble(reader.GetOrdinal("confidence")),
            CreatedAt = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at"))
        };
    }
}
