using System.Collections.Concurrent;
using SemanticMemory.Application.Abstractions;
using SemanticMemory.Domain;

namespace SemanticMemory.Infrastructure;

public sealed class InMemorySemanticGraphStore : ISemanticGraphStore
{
    private static readonly HashSet<string> ExclusiveRelations = new(StringComparer.OrdinalIgnoreCase)
    {
        "prefersLanguage",
        "prefersApiStyle",
        "currentProject",
        "primaryGoal",
        "currentRole"
    };

    private readonly ConcurrentDictionary<Guid, SemanticNode> nodes = new();
    private readonly ConcurrentDictionary<Guid, SemanticEdge> edges = new();

    public Task<SemanticNode> UpsertNodeAsync(
        SemanticNode node,
        CancellationToken cancellationToken)
    {
        var existing = nodes.Values.FirstOrDefault(candidate =>
            candidate.TenantId == node.TenantId &&
            candidate.UserId == node.UserId &&
            candidate.Type == node.Type &&
            candidate.NormalizedKey == node.NormalizedKey);

        if (existing is not null)
        {
            existing.CanonicalName = node.CanonicalName;
            existing.Aliases = existing.Aliases
                .Concat(node.Aliases)
                .Where(alias => !string.IsNullOrWhiteSpace(alias))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            existing.Description = node.Description ?? existing.Description;
            existing.Status = MemoryStatus.Active;
            existing.UpdatedAt = node.UpdatedAt;
            return Task.FromResult(existing);
        }

        nodes[node.Id] = node;
        return Task.FromResult(node);
    }

    public Task<SemanticEdge> UpsertEdgeAsync(
        SemanticEdge edge,
        CancellationToken cancellationToken)
    {
        if (ExclusiveRelations.Contains(edge.RelationType))
        {
            var supersededEdges = edges.Values.Where(existing =>
                existing.TenantId == edge.TenantId &&
                existing.UserId == edge.UserId &&
                existing.SourceNodeId == edge.SourceNodeId &&
                existing.RelationType.Equals(edge.RelationType, StringComparison.OrdinalIgnoreCase) &&
                existing.TargetNodeId != edge.TargetNodeId &&
                existing.Status == MemoryStatus.Active);

            foreach (var superseded in supersededEdges)
            {
                superseded.Status = MemoryStatus.Superseded;
                superseded.ValidTo = edge.ValidFrom ?? edge.CreatedAt;
                superseded.UpdatedAt = edge.CreatedAt;
            }
        }

        var existingEdge = edges.Values.FirstOrDefault(existing =>
            existing.TenantId == edge.TenantId &&
            existing.UserId == edge.UserId &&
            existing.SourceNodeId == edge.SourceNodeId &&
            existing.TargetNodeId == edge.TargetNodeId &&
            existing.RelationType.Equals(edge.RelationType, StringComparison.OrdinalIgnoreCase) &&
            existing.Status == MemoryStatus.Active);

        if (existingEdge is not null)
        {
            existingEdge.Confidence = Math.Max(existingEdge.Confidence, edge.Confidence);
            existingEdge.Weight = Math.Max(existingEdge.Weight, edge.Weight);
            existingEdge.ValidFrom ??= edge.ValidFrom;
            existingEdge.ValidTo = edge.ValidTo;
            existingEdge.UpdatedAt = edge.UpdatedAt;
            return Task.FromResult(existingEdge);
        }

        edges[edge.Id] = edge;
        return Task.FromResult(edge);
    }

    public Task<IReadOnlyList<SemanticNode>> SearchNodesAsync(
        string tenantId,
        string userId,
        string text,
        int limit,
        CancellationToken cancellationToken)
    {
        var normalized = SimpleEntityNormalizer.NormalizeKey(text);
        var results = nodes.Values
            .Where(node =>
                node.TenantId == tenantId &&
                node.UserId == userId &&
                node.Status == MemoryStatus.Active &&
                (node.NormalizedKey.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                 normalized.Contains(node.NormalizedKey, StringComparison.OrdinalIgnoreCase) ||
                 node.CanonicalName.Contains(text, StringComparison.OrdinalIgnoreCase)))
            .Take(Math.Max(1, limit))
            .ToArray();

        return Task.FromResult<IReadOnlyList<SemanticNode>>(results);
    }

    public Task<IReadOnlyList<SemanticEdge>> GetRelatedEdgesAsync(
        string tenantId,
        string userId,
        IReadOnlyList<Guid> nodeIds,
        int depth,
        int limit,
        CancellationToken cancellationToken)
    {
        var frontier = nodeIds.ToHashSet();
        var visitedNodes = nodeIds.ToHashSet();
        var collectedEdges = new Dictionary<Guid, SemanticEdge>();
        var maxDepth = Math.Max(1, depth);

        for (var currentDepth = 0; currentDepth < maxDepth; currentDepth++)
        {
            var edgesAtDepth = edges.Values
                .Where(edge =>
                    edge.TenantId == tenantId &&
                    edge.UserId == userId &&
                    edge.Status == MemoryStatus.Active &&
                    (frontier.Contains(edge.SourceNodeId) || frontier.Contains(edge.TargetNodeId)))
                .ToArray();

            var nextFrontier = new HashSet<Guid>();

            foreach (var edge in edgesAtDepth)
            {
                collectedEdges.TryAdd(edge.Id, edge);

                if (visitedNodes.Add(edge.SourceNodeId))
                {
                    nextFrontier.Add(edge.SourceNodeId);
                }

                if (visitedNodes.Add(edge.TargetNodeId))
                {
                    nextFrontier.Add(edge.TargetNodeId);
                }
            }

            frontier = nextFrontier;

            if (frontier.Count == 0 || collectedEdges.Count >= limit)
            {
                break;
            }
        }

        return Task.FromResult<IReadOnlyList<SemanticEdge>>(
            collectedEdges.Values
                .Take(Math.Max(1, limit))
                .ToArray());
    }

    public Task<IReadOnlyList<SemanticEdge>> GetEdgesByIdsAsync(
        string tenantId,
        string userId,
        IReadOnlyList<Guid> edgeIds,
        CancellationToken cancellationToken)
    {
        var idSet = edgeIds.ToHashSet();
        var results = edges.Values
            .Where(edge => idSet.Contains(edge.Id) && edge.TenantId == tenantId && edge.UserId == userId)
            .ToArray();

        return Task.FromResult<IReadOnlyList<SemanticEdge>>(results);
    }

    public Task<IReadOnlyList<SemanticNode>> GetNodesByIdsAsync(
        string tenantId,
        string userId,
        IReadOnlyList<Guid> nodeIds,
        CancellationToken cancellationToken)
    {
        var idSet = nodeIds.ToHashSet();
        var results = nodes.Values
            .Where(node => idSet.Contains(node.Id) && node.TenantId == tenantId && node.UserId == userId)
            .ToArray();

        return Task.FromResult<IReadOnlyList<SemanticNode>>(results);
    }

    public Task MarkEdgeStatusAsync(
        string tenantId,
        string userId,
        Guid edgeId,
        MemoryStatus status,
        DateTimeOffset? validTo,
        CancellationToken cancellationToken)
    {
        if (edges.TryGetValue(edgeId, out var edge) &&
            edge.TenantId == tenantId &&
            edge.UserId == userId)
        {
            edge.Status = status;
            edge.ValidTo = validTo;
            edge.UpdatedAt = validTo ?? DateTimeOffset.UtcNow;
        }

        return Task.CompletedTask;
    }
}
