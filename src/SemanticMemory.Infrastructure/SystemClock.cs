using SemanticMemory.Application.Abstractions;

namespace SemanticMemory.Infrastructure;

public sealed class SystemClock : IApplicationClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
