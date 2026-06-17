using System.Collections.Concurrent;
using SemanticMemory.Application.Abstractions;
using SemanticMemory.Domain;

namespace SemanticMemory.Infrastructure;

public sealed class InMemoryEvidenceStore : IEvidenceStore
{
    private readonly ConcurrentDictionary<Guid, Evidence> evidenceItems = new();

    public Task SaveEvidenceAsync(
        Evidence evidence,
        CancellationToken cancellationToken)
    {
        evidenceItems[evidence.Id] = evidence;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Evidence>> GetEvidenceForEdgeAsync(
        string tenantId,
        string userId,
        Guid edgeId,
        CancellationToken cancellationToken)
    {
        var results = evidenceItems.Values
            .Where(evidence => evidence.TenantId == tenantId && evidence.UserId == userId && evidence.EdgeId == edgeId)
            .OrderByDescending(evidence => evidence.Confidence)
            .ToArray();

        return Task.FromResult<IReadOnlyList<Evidence>>(results);
    }

    public Task<IReadOnlyList<Evidence>> GetEvidenceForEdgesAsync(
        string tenantId,
        string userId,
        IReadOnlyList<Guid> edgeIds,
        CancellationToken cancellationToken)
    {
        var idSet = edgeIds.ToHashSet();
        var results = evidenceItems.Values
            .Where(evidence => evidence.TenantId == tenantId && evidence.UserId == userId && idSet.Contains(evidence.EdgeId))
            .OrderByDescending(evidence => evidence.Confidence)
            .ToArray();

        return Task.FromResult<IReadOnlyList<Evidence>>(results);
    }

    public Task<IReadOnlyList<Evidence>> GetEvidenceForMemoryChunksAsync(
        string tenantId,
        string userId,
        IReadOnlyList<Guid> memoryChunkIds,
        CancellationToken cancellationToken)
    {
        var idSet = memoryChunkIds.ToHashSet();
        var results = evidenceItems.Values
            .Where(evidence => evidence.TenantId == tenantId && evidence.UserId == userId && idSet.Contains(evidence.MemoryChunkId))
            .OrderByDescending(evidence => evidence.Confidence)
            .ToArray();

        return Task.FromResult<IReadOnlyList<Evidence>>(results);
    }
}
