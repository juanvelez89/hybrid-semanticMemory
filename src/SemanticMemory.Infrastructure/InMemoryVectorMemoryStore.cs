using System.Collections.Concurrent;
using SemanticMemory.Application.Abstractions;
using SemanticMemory.Application.Models;
using SemanticMemory.Domain;

namespace SemanticMemory.Infrastructure;

public sealed class InMemoryVectorMemoryStore : IVectorMemoryStore
{
    private readonly ConcurrentDictionary<Guid, MemoryChunk> chunks = new();

    public Task<MemoryChunk> SaveEmbeddingAsync(
        MemoryChunk chunk,
        CancellationToken cancellationToken)
    {
        chunks[chunk.Id] = chunk;
        return Task.FromResult(chunk);
    }

    public Task<IReadOnlyList<ScoredMemoryChunk>> SearchSimilarAsync(
        string tenantId,
        string userId,
        float[] embedding,
        int limit,
        CancellationToken cancellationToken)
    {
        var results = chunks.Values
            .Where(chunk =>
                chunk.TenantId == tenantId &&
                chunk.UserId == userId &&
                chunk.Status == MemoryStatus.Active &&
                chunk.Embedding is not null)
            .Select(chunk => new ScoredMemoryChunk(chunk, CosineSimilarity(embedding, chunk.Embedding!)))
            .OrderByDescending(chunk => chunk.Similarity)
            .Take(Math.Max(1, limit))
            .ToArray();

        return Task.FromResult<IReadOnlyList<ScoredMemoryChunk>>(results);
    }

    public Task<MemoryChunk?> GetByIdAsync(
        string tenantId,
        string userId,
        Guid memoryChunkId,
        CancellationToken cancellationToken)
    {
        chunks.TryGetValue(memoryChunkId, out var chunk);

        if (chunk is null || chunk.TenantId != tenantId || chunk.UserId != userId)
        {
            return Task.FromResult<MemoryChunk?>(null);
        }

        return Task.FromResult<MemoryChunk?>(chunk);
    }

    public Task<IReadOnlyList<MemoryChunk>> GetByIdsAsync(
        string tenantId,
        string userId,
        IReadOnlyList<Guid> memoryChunkIds,
        CancellationToken cancellationToken)
    {
        var idSet = memoryChunkIds.ToHashSet();
        var results = chunks.Values
            .Where(chunk => idSet.Contains(chunk.Id) && chunk.TenantId == tenantId && chunk.UserId == userId)
            .ToArray();

        return Task.FromResult<IReadOnlyList<MemoryChunk>>(results);
    }

    public Task MarkForgottenAsync(
        string tenantId,
        string userId,
        Guid memoryChunkId,
        DateTimeOffset forgottenAt,
        CancellationToken cancellationToken)
    {
        if (chunks.TryGetValue(memoryChunkId, out var chunk) &&
            chunk.TenantId == tenantId &&
            chunk.UserId == userId)
        {
            chunk.Status = MemoryStatus.Forgotten;
            chunk.ForgottenAt = forgottenAt;
            chunk.UpdatedAt = forgottenAt;
        }

        return Task.CompletedTask;
    }

    private static double CosineSimilarity(float[] left, float[] right)
    {
        var length = Math.Min(left.Length, right.Length);
        double dot = 0;
        double leftMagnitude = 0;
        double rightMagnitude = 0;

        for (var i = 0; i < length; i++)
        {
            dot += left[i] * right[i];
            leftMagnitude += left[i] * left[i];
            rightMagnitude += right[i] * right[i];
        }

        if (leftMagnitude == 0 || rightMagnitude == 0)
        {
            return 0;
        }

        return dot / (Math.Sqrt(leftMagnitude) * Math.Sqrt(rightMagnitude));
    }
}
