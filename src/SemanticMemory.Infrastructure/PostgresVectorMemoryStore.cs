using Npgsql;
using SemanticMemory.Application.Abstractions;
using SemanticMemory.Application.Models;
using SemanticMemory.Domain;

namespace SemanticMemory.Infrastructure;

public sealed class PostgresVectorMemoryStore(NpgsqlDataSource dataSource) : IVectorMemoryStore
{
    public async Task<MemoryChunk> SaveEmbeddingAsync(
        MemoryChunk chunk,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            INSERT INTO memory_chunks (
                id,
                tenant_id,
                user_id,
                conversation_id,
                raw_text,
                summary,
                embedding,
                memory_type,
                status,
                source_type,
                importance,
                created_at,
                updated_at,
                forgotten_at
            )
            VALUES (
                @id,
                @tenant_id,
                @user_id,
                @conversation_id,
                @raw_text,
                @summary,
                CAST(@embedding AS vector),
                @memory_type,
                @status,
                @source_type,
                @importance,
                @created_at,
                @updated_at,
                @forgotten_at
            )
            ON CONFLICT (id) DO UPDATE SET
                conversation_id = EXCLUDED.conversation_id,
                raw_text = EXCLUDED.raw_text,
                summary = EXCLUDED.summary,
                embedding = EXCLUDED.embedding,
                memory_type = EXCLUDED.memory_type,
                status = EXCLUDED.status,
                source_type = EXCLUDED.source_type,
                importance = EXCLUDED.importance,
                updated_at = EXCLUDED.updated_at,
                forgotten_at = EXCLUDED.forgotten_at;
            """;

        await using var command = dataSource.CreateCommand(sql);
        AddChunkParameters(command, chunk);
        await command.ExecuteNonQueryAsync(cancellationToken);

        return chunk;
    }

    public async Task<IReadOnlyList<ScoredMemoryChunk>> SearchSimilarAsync(
        string tenantId,
        string userId,
        float[] embedding,
        int limit,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            SELECT
                id,
                tenant_id,
                user_id,
                conversation_id,
                raw_text,
                summary,
                embedding::text AS embedding_text,
                memory_type,
                status,
                source_type,
                importance,
                created_at,
                updated_at,
                forgotten_at,
                1 - (embedding <=> CAST(@embedding AS vector)) AS similarity
            FROM memory_chunks
            WHERE tenant_id = @tenant_id
              AND user_id = @user_id
              AND status = 'Active'
              AND embedding IS NOT NULL
            ORDER BY embedding <=> CAST(@embedding AS vector)
            LIMIT @limit;
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("embedding", PostgresSerialization.ToVectorLiteral(embedding)!);
        command.Parameters.AddWithValue("limit", Math.Max(1, limit));

        var results = new List<ScoredMemoryChunk>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new ScoredMemoryChunk(
                MapChunk(reader),
                reader.GetDouble(reader.GetOrdinal("similarity"))));
        }

        return results;
    }

    public async Task<MemoryChunk?> GetByIdAsync(
        string tenantId,
        string userId,
        Guid memoryChunkId,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            SELECT
                id,
                tenant_id,
                user_id,
                conversation_id,
                raw_text,
                summary,
                embedding::text AS embedding_text,
                memory_type,
                status,
                source_type,
                importance,
                created_at,
                updated_at,
                forgotten_at
            FROM memory_chunks
            WHERE tenant_id = @tenant_id
              AND user_id = @user_id
              AND id = @id;
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("id", memoryChunkId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapChunk(reader) : null;
    }

    public async Task<IReadOnlyList<MemoryChunk>> GetByIdsAsync(
        string tenantId,
        string userId,
        IReadOnlyList<Guid> memoryChunkIds,
        CancellationToken cancellationToken)
    {
        if (memoryChunkIds.Count == 0)
        {
            return [];
        }

        const string sql =
            """
            SELECT
                id,
                tenant_id,
                user_id,
                conversation_id,
                raw_text,
                summary,
                embedding::text AS embedding_text,
                memory_type,
                status,
                source_type,
                importance,
                created_at,
                updated_at,
                forgotten_at
            FROM memory_chunks
            WHERE tenant_id = @tenant_id
              AND user_id = @user_id
              AND id = ANY(@ids);
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("ids", memoryChunkIds.ToArray());

        var results = new List<MemoryChunk>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(MapChunk(reader));
        }

        return results;
    }

    public async Task MarkForgottenAsync(
        string tenantId,
        string userId,
        Guid memoryChunkId,
        DateTimeOffset forgottenAt,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            UPDATE memory_chunks
            SET
                status = 'Forgotten',
                forgotten_at = @forgotten_at,
                updated_at = @forgotten_at
            WHERE tenant_id = @tenant_id
              AND user_id = @user_id
              AND id = @id;
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("id", memoryChunkId);
        command.Parameters.AddWithValue("forgotten_at", forgottenAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddChunkParameters(NpgsqlCommand command, MemoryChunk chunk)
    {
        command.Parameters.AddWithValue("id", chunk.Id);
        command.Parameters.AddWithValue("tenant_id", chunk.TenantId);
        command.Parameters.AddWithValue("user_id", chunk.UserId);
        command.Parameters.AddWithValue("conversation_id", (object?)chunk.ConversationId ?? DBNull.Value);
        command.Parameters.AddWithValue("raw_text", chunk.RawText);
        command.Parameters.AddWithValue("summary", (object?)chunk.Summary ?? DBNull.Value);
        command.Parameters.AddWithValue("embedding", (object?)PostgresSerialization.ToVectorLiteral(chunk.Embedding) ?? DBNull.Value);
        command.Parameters.AddWithValue("memory_type", chunk.MemoryType.ToDbString());
        command.Parameters.AddWithValue("status", chunk.Status.ToDbString());
        command.Parameters.AddWithValue("source_type", chunk.SourceType.ToDbString());
        command.Parameters.AddWithValue("importance", chunk.Importance);
        command.Parameters.AddWithValue("created_at", chunk.CreatedAt);
        command.Parameters.AddWithValue("updated_at", chunk.UpdatedAt);
        command.Parameters.AddWithValue("forgotten_at", (object?)chunk.ForgottenAt ?? DBNull.Value);
    }

    private static MemoryChunk MapChunk(NpgsqlDataReader reader)
    {
        return new MemoryChunk
        {
            Id = reader.GetGuid(reader.GetOrdinal("id")),
            TenantId = reader.GetString(reader.GetOrdinal("tenant_id")),
            UserId = reader.GetString(reader.GetOrdinal("user_id")),
            ConversationId = GetNullableString(reader, "conversation_id"),
            RawText = reader.GetString(reader.GetOrdinal("raw_text")),
            Summary = GetNullableString(reader, "summary"),
            Embedding = PostgresSerialization.ParseVector(GetNullableString(reader, "embedding_text")),
            MemoryType = PostgresSerialization.ParseMemoryType(reader.GetString(reader.GetOrdinal("memory_type"))),
            Status = PostgresSerialization.ParseMemoryStatus(reader.GetString(reader.GetOrdinal("status"))),
            SourceType = PostgresSerialization.ParseSourceType(reader.GetString(reader.GetOrdinal("source_type"))),
            Importance = reader.GetDouble(reader.GetOrdinal("importance")),
            CreatedAt = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at")),
            UpdatedAt = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("updated_at")),
            ForgottenAt = GetNullableDateTimeOffset(reader, "forgotten_at")
        };
    }

    private static string? GetNullableString(NpgsqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static DateTimeOffset? GetNullableDateTimeOffset(NpgsqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<DateTimeOffset>(ordinal);
    }
}
