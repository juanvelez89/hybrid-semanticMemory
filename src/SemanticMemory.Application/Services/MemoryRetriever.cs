using SemanticMemory.Application.Abstractions;
using SemanticMemory.Application.Models;
using SemanticMemory.Domain;

namespace SemanticMemory.Application.Services;

public sealed class MemoryRetriever(
    IApplicationClock clock,
    IEmbeddingProvider embeddingProvider,
    IEntityExtractor entityExtractor,
    IEntityNormalizer entityNormalizer,
    IVectorMemoryStore vectorMemoryStore,
    ISemanticGraphStore semanticGraphStore,
    IEvidenceStore evidenceStore,
    IPromptContextBuilder promptContextBuilder) : IMemoryRetriever
{
    public async Task<MemoryContext> RetrieveContextAsync(
        RetrieveMemoryQuery query,
        CancellationToken cancellationToken)
    {
        MemoryValidation.RequireTenantAndUser(query.TenantId, query.UserId);
        MemoryValidation.RequireText(query.Prompt, nameof(query.Prompt));

        var now = clock.UtcNow;
        var promptEmbedding = await embeddingProvider.CreateEmbeddingAsync(query.Prompt, cancellationToken);
        var similarChunks = await vectorMemoryStore.SearchSimilarAsync(
            query.TenantId,
            query.UserId,
            promptEmbedding,
            query.VectorLimit,
            cancellationToken);

        var promptEntities = await entityExtractor.ExtractEntitiesAsync(query.Prompt, cancellationToken);
        var promptNodes = new List<SemanticNode>();

        foreach (var entity in promptEntities)
        {
            var normalized = await entityNormalizer.NormalizeAsync(query.TenantId, query.UserId, entity, cancellationToken);
            var found = await semanticGraphStore.SearchNodesAsync(
                query.TenantId,
                query.UserId,
                normalized.CanonicalName,
                query.NodeLimit,
                cancellationToken);

            promptNodes.AddRange(found);
        }

        var promptNodeIds = promptNodes
            .Select(node => node.Id)
            .Distinct()
            .ToArray();

        var graphEdges = promptNodeIds.Length == 0
            ? []
            : await semanticGraphStore.GetRelatedEdgesAsync(
                query.TenantId,
                query.UserId,
                promptNodeIds,
                query.GraphDepth,
                query.NodeLimit,
                cancellationToken);

        var chunkIds = similarChunks
            .Select(chunk => chunk.Chunk.Id)
            .Distinct()
            .ToArray();

        var evidenceFromChunks = chunkIds.Length == 0
            ? []
            : await evidenceStore.GetEvidenceForMemoryChunksAsync(
                query.TenantId,
                query.UserId,
                chunkIds,
                cancellationToken);

        var chunkEvidenceEdgeIds = evidenceFromChunks
            .Select(evidence => evidence.EdgeId)
            .Distinct()
            .ToArray();

        var edgesFromChunkEvidence = chunkEvidenceEdgeIds.Length == 0
            ? []
            : await semanticGraphStore.GetEdgesByIdsAsync(
                query.TenantId,
                query.UserId,
                chunkEvidenceEdgeIds,
                cancellationToken);

        var edges = graphEdges
            .Concat(edgesFromChunkEvidence)
            .Where(edge => edge.Status == MemoryStatus.Active)
            .GroupBy(edge => edge.Id)
            .Select(group => group.First())
            .ToArray();

        var edgeIds = edges.Select(edge => edge.Id).ToArray();
        var edgeEvidence = edgeIds.Length == 0
            ? []
            : await evidenceStore.GetEvidenceForEdgesAsync(
                query.TenantId,
                query.UserId,
                edgeIds,
                cancellationToken);

        var activeChunkIds = similarChunks
            .Select(chunk => chunk.Chunk.Id)
            .ToHashSet();

        var evidenceChunkIds = edgeEvidence
            .Select(evidence => evidence.MemoryChunkId)
            .Distinct()
            .ToArray();

        var activeEvidenceChunks = evidenceChunkIds.Length == 0
            ? []
            : await vectorMemoryStore.GetByIdsAsync(
                query.TenantId,
                query.UserId,
                evidenceChunkIds,
                cancellationToken);

        foreach (var activeChunk in activeEvidenceChunks.Where(chunk => chunk.Status == MemoryStatus.Active))
        {
            activeChunkIds.Add(activeChunk.Id);
        }

        var activeEvidence = edgeEvidence
            .Where(evidence => activeChunkIds.Contains(evidence.MemoryChunkId))
            .ToArray();

        var similaritiesByChunkId = similarChunks
            .ToDictionary(chunk => chunk.Chunk.Id, chunk => chunk.Similarity);

        var evidenceByEdgeId = activeEvidence
            .GroupBy(evidence => evidence.EdgeId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var scoredEdges = edges
            .Select(edge =>
            {
                var graphRelevance = graphEdges.Any(graphEdge => graphEdge.Id == edge.Id) ? 0.8 : 0.55;
                var semanticSimilarity = evidenceByEdgeId.TryGetValue(edge.Id, out var evidenceForEdge)
                    ? evidenceForEdge
                        .Select(evidence => similaritiesByChunkId.TryGetValue(evidence.MemoryChunkId, out var similarity) ? similarity : 0.0)
                        .DefaultIfEmpty(0.0)
                        .Max()
                    : 0.0;

                var confidence = MemoryScoring.ConfidenceFor(edge);
                var recency = MemoryScoring.CalculateRecency(edge.CreatedAt, now);
                var score = MemoryScoring.CalculateHybridScore(semanticSimilarity, graphRelevance, confidence, edge.CreatedAt, now);

                return new ScoredSemanticEdge(edge, score, graphRelevance, confidence, recency);
            })
            .OrderByDescending(edge => edge.Score)
            .ToArray();

        var nodeIds = scoredEdges
            .SelectMany(edge => new[] { edge.Edge.SourceNodeId, edge.Edge.TargetNodeId })
            .Concat(promptNodeIds)
            .Distinct()
            .ToArray();

        var nodes = nodeIds.Length == 0
            ? []
            : await semanticGraphStore.GetNodesByIdsAsync(
                query.TenantId,
                query.UserId,
                nodeIds,
                cancellationToken);

        var context = new MemoryContext(
            similarChunks,
            nodes,
            scoredEdges,
            activeEvidence,
            string.Empty);

        var contextText = promptContextBuilder.BuildContext(context, query.MaxContextTokens);
        return context with { ContextText = contextText };
    }
}
