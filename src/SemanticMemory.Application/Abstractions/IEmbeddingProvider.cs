namespace SemanticMemory.Application.Abstractions;

public interface IEmbeddingProvider
{
    string Model { get; }

    Task<float[]> CreateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken);
}
