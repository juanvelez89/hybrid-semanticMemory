using SemanticMemory.Application.Abstractions;

namespace SemanticMemory.Infrastructure;

public sealed class FakeEmbeddingProvider : IEmbeddingProvider
{
    public string Model => "fake-hashing-embedding";

    public Task<float[]> CreateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken)
    {
        const int dimensions = 1536;
        var vector = new float[dimensions];
        var tokens = Tokenize(text);

        foreach (var token in tokens)
        {
            var hash = Math.Abs(StringComparer.OrdinalIgnoreCase.GetHashCode(token));
            var index = hash % dimensions;
            vector[index] += 1;
        }

        Normalize(vector);
        return Task.FromResult(vector);
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        return text
            .ToLowerInvariant()
            .Split([' ', '.', ',', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}', '/', '\\', '-', '_'], StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length > 1);
    }

    private static void Normalize(float[] vector)
    {
        var magnitude = Math.Sqrt(vector.Sum(value => value * value));

        if (magnitude == 0)
        {
            return;
        }

        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = (float)(vector[i] / magnitude);
        }
    }
}
