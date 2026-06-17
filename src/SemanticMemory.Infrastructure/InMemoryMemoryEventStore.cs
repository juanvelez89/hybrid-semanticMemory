using System.Collections.Concurrent;
using SemanticMemory.Application.Abstractions;
using SemanticMemory.Domain;

namespace SemanticMemory.Infrastructure;

public sealed class InMemoryMemoryEventStore : IMemoryEventStore
{
    private readonly ConcurrentDictionary<Guid, MemoryEvent> events = new();

    public Task SaveEventAsync(
        MemoryEvent memoryEvent,
        CancellationToken cancellationToken)
    {
        events[memoryEvent.Id] = memoryEvent;
        return Task.CompletedTask;
    }
}
