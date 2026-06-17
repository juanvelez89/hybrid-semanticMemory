using SemanticMemory.Application.Models;
using SemanticMemory.Domain;

namespace SemanticMemory.Application.Abstractions;

public interface IVectorMemoryStore
{
    Task<MemoryChunk> SaveEmbeddingAsync(
        MemoryChunk chunk,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ScoredMemoryChunk>> SearchSimilarAsync(
        string tenantId,
        string userId,
        float[] embedding,
        int limit,
        CancellationToken cancellationToken);

    Task<MemoryChunk?> GetByIdAsync(
        string tenantId,
        string userId,
        Guid memoryChunkId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MemoryChunk>> GetByIdsAsync(
        string tenantId,
        string userId,
        IReadOnlyList<Guid> memoryChunkIds,
        CancellationToken cancellationToken);

    Task MarkForgottenAsync(
        string tenantId,
        string userId,
        Guid memoryChunkId,
        DateTimeOffset forgottenAt,
        CancellationToken cancellationToken);
}

public interface ISemanticGraphStore
{
    Task<SemanticNode> UpsertNodeAsync(
        SemanticNode node,
        CancellationToken cancellationToken);

    Task<SemanticEdge> UpsertEdgeAsync(
        SemanticEdge edge,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SemanticNode>> SearchNodesAsync(
        string tenantId,
        string userId,
        string text,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SemanticEdge>> GetRelatedEdgesAsync(
        string tenantId,
        string userId,
        IReadOnlyList<Guid> nodeIds,
        int depth,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SemanticEdge>> GetEdgesByIdsAsync(
        string tenantId,
        string userId,
        IReadOnlyList<Guid> edgeIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SemanticNode>> GetNodesByIdsAsync(
        string tenantId,
        string userId,
        IReadOnlyList<Guid> nodeIds,
        CancellationToken cancellationToken);

    Task MarkEdgeStatusAsync(
        string tenantId,
        string userId,
        Guid edgeId,
        MemoryStatus status,
        DateTimeOffset? validTo,
        CancellationToken cancellationToken);
}

public interface IEvidenceStore
{
    Task SaveEvidenceAsync(
        Evidence evidence,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Evidence>> GetEvidenceForEdgeAsync(
        string tenantId,
        string userId,
        Guid edgeId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Evidence>> GetEvidenceForEdgesAsync(
        string tenantId,
        string userId,
        IReadOnlyList<Guid> edgeIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Evidence>> GetEvidenceForMemoryChunksAsync(
        string tenantId,
        string userId,
        IReadOnlyList<Guid> memoryChunkIds,
        CancellationToken cancellationToken);
}

public interface IMemoryEventStore
{
    Task SaveEventAsync(
        MemoryEvent memoryEvent,
        CancellationToken cancellationToken);
}
