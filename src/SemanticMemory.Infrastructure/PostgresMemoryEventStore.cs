using Npgsql;
using SemanticMemory.Application.Abstractions;
using SemanticMemory.Domain;

namespace SemanticMemory.Infrastructure;

public sealed class PostgresMemoryEventStore(NpgsqlDataSource dataSource) : IMemoryEventStore
{
    public async Task SaveEventAsync(
        MemoryEvent memoryEvent,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            INSERT INTO memory_events (
                id,
                tenant_id,
                user_id,
                event_type,
                entity_type,
                entity_id,
                payload_json,
                created_at
            )
            VALUES (
                @id,
                @tenant_id,
                @user_id,
                @event_type,
                @entity_type,
                @entity_id,
                CAST(@payload_json AS jsonb),
                @created_at
            )
            ON CONFLICT (id) DO NOTHING;
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("id", memoryEvent.Id);
        command.Parameters.AddWithValue("tenant_id", memoryEvent.TenantId);
        command.Parameters.AddWithValue("user_id", memoryEvent.UserId);
        command.Parameters.AddWithValue("event_type", memoryEvent.EventType);
        command.Parameters.AddWithValue("entity_type", memoryEvent.EntityType);
        command.Parameters.AddWithValue("entity_id", memoryEvent.EntityId);
        command.Parameters.AddWithValue("payload_json", string.IsNullOrWhiteSpace(memoryEvent.PayloadJson) ? "{}" : memoryEvent.PayloadJson);
        command.Parameters.AddWithValue("created_at", memoryEvent.CreatedAt);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
