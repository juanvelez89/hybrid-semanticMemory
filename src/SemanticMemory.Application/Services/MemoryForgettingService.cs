using SemanticMemory.Application.Abstractions;
using SemanticMemory.Application.Models;
using SemanticMemory.Domain;

namespace SemanticMemory.Application.Services;

public sealed class MemoryForgettingService(
    IApplicationClock clock,
    IVectorMemoryStore vectorMemoryStore,
    IMemoryEventStore memoryEventStore) : IMemoryForgettingService
{
    public async Task ForgetAsync(
        ForgetMemoryCommand command,
        CancellationToken cancellationToken)
    {
        MemoryValidation.RequireTenantAndUser(command.TenantId, command.UserId);

        var now = clock.UtcNow;
        await vectorMemoryStore.MarkForgottenAsync(
            command.TenantId,
            command.UserId,
            command.MemoryChunkId,
            now,
            cancellationToken);

        await memoryEventStore.SaveEventAsync(
            new MemoryEvent
            {
                Id = Guid.NewGuid(),
                TenantId = command.TenantId,
                UserId = command.UserId,
                EventType = "MemoryForgotten",
                EntityType = nameof(MemoryChunk),
                EntityId = command.MemoryChunkId,
                PayloadJson = "{}",
                CreatedAt = now
            },
            cancellationToken);
    }
}
