using SemanticMemory.Domain;

namespace SemanticMemory.Application.Models;

public sealed record IngestionResult(
    Guid MemoryChunkId,
    IReadOnlyList<ExtractedEntity> Entities,
    IReadOnlyList<ExtractedRelation> Relations,
    IReadOnlyList<SemanticNode> UpsertedNodes,
    IReadOnlyList<SemanticEdge> UpsertedEdges,
    int EvidenceCount);

public sealed record ScoredMemoryChunk(
    MemoryChunk Chunk,
    double Similarity);

public sealed record ScoredSemanticEdge(
    SemanticEdge Edge,
    double Score,
    double GraphRelevance,
    double Confidence,
    double Recency);

public sealed record MemoryContext(
    IReadOnlyList<ScoredMemoryChunk> SimilarChunks,
    IReadOnlyList<SemanticNode> RelevantNodes,
    IReadOnlyList<ScoredSemanticEdge> RelevantEdges,
    IReadOnlyList<Evidence> Evidence,
    string ContextText);
