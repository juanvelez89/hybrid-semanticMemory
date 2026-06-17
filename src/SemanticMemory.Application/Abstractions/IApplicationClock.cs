namespace SemanticMemory.Application.Abstractions;

public interface IApplicationClock
{
    DateTimeOffset UtcNow { get; }
}
