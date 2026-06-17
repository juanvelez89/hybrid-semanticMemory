using Neo4j.Driver;
using SemanticMemory.Application.Abstractions;
using SemanticMemory.Domain;

namespace SemanticMemory.Infrastructure;

public sealed class Neo4jSemanticGraphStore(
    IDriver driver,
    Neo4jOptions options) : ISemanticGraphStore
{
    private static readonly HashSet<string> ExclusiveRelations = new(StringComparer.OrdinalIgnoreCase)
    {
        "prefersLanguage",
        "prefersApiStyle",
        "currentProject",
        "primaryGoal",
        "currentRole"
    };

    public async Task<SemanticNode> UpsertNodeAsync(
        SemanticNode node,
        CancellationToken cancellationToken)
    {
        await using var session = CreateSession();

        return await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MERGE (n:SemanticNode {
                    tenantId: $tenantId,
                    userId: $userId,
                    type: $type,
                    normalizedKey: $normalizedKey
                })
                ON CREATE SET
                    n.id = $id,
                    n.createdAt = $createdAt
                SET
                    n.canonicalName = $canonicalName,
                    n.aliases = $aliases,
                    n.description = $description,
                    n.status = $status,
                    n.updatedAt = $updatedAt
                RETURN n
                """,
                new
                {
                    id = node.Id.ToString(),
                    node.TenantId,
                    node.UserId,
                    node.Type,
                    node.CanonicalName,
                    node.NormalizedKey,
                    node.Aliases,
                    node.Description,
                    status = node.Status.ToString(),
                    createdAt = ToIso(node.CreatedAt),
                    updatedAt = ToIso(node.UpdatedAt)
                });

            var record = await cursor.SingleAsync();
            return MapNode(record["n"].As<INode>());
        });
    }

    public async Task<SemanticEdge> UpsertEdgeAsync(
        SemanticEdge edge,
        CancellationToken cancellationToken)
    {
        await using var session = CreateSession();

        return await session.ExecuteWriteAsync(async tx =>
        {
            if (ExclusiveRelations.Contains(edge.RelationType))
            {
                await tx.RunAsync(
                    """
                    MATCH (:SemanticNode {tenantId: $tenantId, userId: $userId, id: $sourceNodeId})
                        -[r:SEMANTIC_EDGE {
                            tenantId: $tenantId,
                            userId: $userId,
                            relationType: $relationType,
                            status: 'Active'
                        }]->(target:SemanticNode)
                    WHERE target.id <> $targetNodeId
                    SET
                        r.status = 'Superseded',
                        r.validTo = $validFrom,
                        r.updatedAt = $updatedAt
                    """,
                    new
                    {
                        edge.TenantId,
                        edge.UserId,
                        sourceNodeId = edge.SourceNodeId.ToString(),
                        targetNodeId = edge.TargetNodeId.ToString(),
                        edge.RelationType,
                        validFrom = ToIso(edge.ValidFrom ?? edge.CreatedAt),
                        updatedAt = ToIso(edge.UpdatedAt)
                    });
            }

            var cursor = await tx.RunAsync(
                """
                MATCH (source:SemanticNode {
                    tenantId: $tenantId,
                    userId: $userId,
                    id: $sourceNodeId
                })
                MATCH (target:SemanticNode {
                    tenantId: $tenantId,
                    userId: $userId,
                    id: $targetNodeId
                })
                MERGE (source)-[r:SEMANTIC_EDGE {
                    tenantId: $tenantId,
                    userId: $userId,
                    sourceNodeId: $sourceNodeId,
                    targetNodeId: $targetNodeId,
                    relationType: $relationType,
                    status: 'Active'
                }]->(target)
                ON CREATE SET
                    r.id = $id,
                    r.createdAt = $createdAt
                SET
                    r.confidence = CASE
                        WHEN coalesce(r.confidence, 0.0) > $confidence THEN r.confidence
                        ELSE $confidence
                    END,
                    r.weight = CASE
                        WHEN coalesce(r.weight, 0.0) > $weight THEN r.weight
                        ELSE $weight
                    END,
                    r.validFrom = coalesce(r.validFrom, $validFrom),
                    r.validTo = $validTo,
                    r.updatedAt = $updatedAt
                RETURN r
                """,
                new
                {
                    id = edge.Id.ToString(),
                    edge.TenantId,
                    edge.UserId,
                    sourceNodeId = edge.SourceNodeId.ToString(),
                    targetNodeId = edge.TargetNodeId.ToString(),
                    edge.RelationType,
                    edge.Confidence,
                    edge.Weight,
                    validFrom = ToIso(edge.ValidFrom ?? edge.CreatedAt),
                    validTo = edge.ValidTo is null ? null : ToIso(edge.ValidTo.Value),
                    createdAt = ToIso(edge.CreatedAt),
                    updatedAt = ToIso(edge.UpdatedAt)
                });

            var record = await cursor.SingleAsync();
            return MapEdge(record["r"].As<IRelationship>());
        });
    }

    public async Task<IReadOnlyList<SemanticNode>> SearchNodesAsync(
        string tenantId,
        string userId,
        string text,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var session = CreateSession();
        var normalizedKey = SimpleEntityNormalizer.NormalizeKey(text);

        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH (n:SemanticNode)
                WHERE n.tenantId = $tenantId
                  AND n.userId = $userId
                  AND n.status = 'Active'
                  AND (
                    toLower(n.canonicalName) CONTAINS toLower($text)
                    OR n.normalizedKey CONTAINS $normalizedKey
                    OR $normalizedKey CONTAINS n.normalizedKey
                  )
                RETURN n
                LIMIT $limit
                """,
                new
                {
                    tenantId,
                    userId,
                    text,
                    normalizedKey,
                    limit = Math.Max(1, limit)
                });

            return await ReadListAsync(cursor, record => MapNode(record["n"].As<INode>()));
        });
    }

    public async Task<IReadOnlyList<SemanticEdge>> GetRelatedEdgesAsync(
        string tenantId,
        string userId,
        IReadOnlyList<Guid> nodeIds,
        int depth,
        int limit,
        CancellationToken cancellationToken)
    {
        if (nodeIds.Count == 0)
        {
            return [];
        }

        var safeDepth = Math.Max(1, Math.Min(2, depth));
        await using var session = CreateSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                $"""
                MATCH (n:SemanticNode)
                WHERE n.tenantId = $tenantId
                  AND n.userId = $userId
                  AND n.id IN $nodeIds
                MATCH path=(n)-[rels:SEMANTIC_EDGE*1..{safeDepth}]-(related:SemanticNode)
                UNWIND relationships(path) AS r
                WITH DISTINCT r
                WHERE r.tenantId = $tenantId
                  AND r.userId = $userId
                  AND r.status = 'Active'
                RETURN r
                LIMIT $limit
                """,
                new
                {
                    tenantId,
                    userId,
                    nodeIds = nodeIds.Select(id => id.ToString()).ToArray(),
                    limit = Math.Max(1, limit)
                });

            return await ReadListAsync(cursor, record => MapEdge(record["r"].As<IRelationship>()));
        });
    }

    public async Task<IReadOnlyList<SemanticEdge>> GetEdgesByIdsAsync(
        string tenantId,
        string userId,
        IReadOnlyList<Guid> edgeIds,
        CancellationToken cancellationToken)
    {
        if (edgeIds.Count == 0)
        {
            return [];
        }

        await using var session = CreateSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH ()-[r:SEMANTIC_EDGE]-()
                WHERE r.tenantId = $tenantId
                  AND r.userId = $userId
                  AND r.id IN $edgeIds
                RETURN r
                """,
                new
                {
                    tenantId,
                    userId,
                    edgeIds = edgeIds.Select(id => id.ToString()).ToArray()
                });

            return await ReadListAsync(cursor, record => MapEdge(record["r"].As<IRelationship>()));
        });
    }

    public async Task<IReadOnlyList<SemanticNode>> GetNodesByIdsAsync(
        string tenantId,
        string userId,
        IReadOnlyList<Guid> nodeIds,
        CancellationToken cancellationToken)
    {
        if (nodeIds.Count == 0)
        {
            return [];
        }

        await using var session = CreateSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH (n:SemanticNode)
                WHERE n.tenantId = $tenantId
                  AND n.userId = $userId
                  AND n.id IN $nodeIds
                RETURN n
                """,
                new
                {
                    tenantId,
                    userId,
                    nodeIds = nodeIds.Select(id => id.ToString()).ToArray()
                });

            return await ReadListAsync(cursor, record => MapNode(record["n"].As<INode>()));
        });
    }

    public async Task MarkEdgeStatusAsync(
        string tenantId,
        string userId,
        Guid edgeId,
        MemoryStatus status,
        DateTimeOffset? validTo,
        CancellationToken cancellationToken)
    {
        await using var session = CreateSession();

        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(
                """
                MATCH ()-[r:SEMANTIC_EDGE]-()
                WHERE r.tenantId = $tenantId
                  AND r.userId = $userId
                  AND r.id = $edgeId
                SET
                    r.status = $status,
                    r.validTo = $validTo,
                    r.updatedAt = $updatedAt
                """,
                new
                {
                    tenantId,
                    userId,
                    edgeId = edgeId.ToString(),
                    status = status.ToString(),
                    validTo = validTo is null ? null : ToIso(validTo.Value),
                    updatedAt = ToIso(DateTimeOffset.UtcNow)
                });
        });
    }

    private IAsyncSession CreateSession()
    {
        return driver.AsyncSession(builder => builder.WithDatabase(options.Database));
    }

    private static SemanticNode MapNode(INode node)
    {
        return new SemanticNode
        {
            Id = Guid.Parse(GetString(node.Properties, "id")),
            TenantId = GetString(node.Properties, "tenantId"),
            UserId = GetString(node.Properties, "userId"),
            Type = GetString(node.Properties, "type"),
            CanonicalName = GetString(node.Properties, "canonicalName"),
            NormalizedKey = GetString(node.Properties, "normalizedKey"),
            Aliases = GetStringList(node.Properties, "aliases"),
            Description = GetNullableString(node.Properties, "description"),
            Status = ParseStatus(GetString(node.Properties, "status")),
            CreatedAt = ParseDate(GetString(node.Properties, "createdAt")),
            UpdatedAt = ParseDate(GetString(node.Properties, "updatedAt"))
        };
    }

    private static SemanticEdge MapEdge(IRelationship relationship)
    {
        return new SemanticEdge
        {
            Id = Guid.Parse(GetString(relationship.Properties, "id")),
            TenantId = GetString(relationship.Properties, "tenantId"),
            UserId = GetString(relationship.Properties, "userId"),
            SourceNodeId = Guid.Parse(GetString(relationship.Properties, "sourceNodeId")),
            TargetNodeId = Guid.Parse(GetString(relationship.Properties, "targetNodeId")),
            RelationType = GetString(relationship.Properties, "relationType"),
            Confidence = GetDouble(relationship.Properties, "confidence"),
            Weight = GetDouble(relationship.Properties, "weight"),
            Status = ParseStatus(GetString(relationship.Properties, "status")),
            ValidFrom = GetNullableDate(relationship.Properties, "validFrom"),
            ValidTo = GetNullableDate(relationship.Properties, "validTo"),
            CreatedAt = ParseDate(GetString(relationship.Properties, "createdAt")),
            UpdatedAt = ParseDate(GetString(relationship.Properties, "updatedAt"))
        };
    }

    private static string ToIso(DateTimeOffset value)
    {
        return value.ToUniversalTime().ToString("O");
    }

    private static async Task<IReadOnlyList<T>> ReadListAsync<T>(
        IResultCursor cursor,
        Func<IRecord, T> map)
    {
        var results = new List<T>();

        while (await cursor.FetchAsync())
        {
            results.Add(map(cursor.Current));
        }

        return results;
    }

    private static DateTimeOffset ParseDate(string value)
    {
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : DateTimeOffset.UnixEpoch;
    }

    private static DateTimeOffset? GetNullableDate(IReadOnlyDictionary<string, object> properties, string key)
    {
        var value = GetNullableString(properties, key);
        return string.IsNullOrWhiteSpace(value) ? null : ParseDate(value);
    }

    private static MemoryStatus ParseStatus(string value)
    {
        return Enum.TryParse<MemoryStatus>(value, ignoreCase: true, out var status)
            ? status
            : MemoryStatus.Active;
    }

    private static string GetString(IReadOnlyDictionary<string, object> properties, string key)
    {
        return properties.TryGetValue(key, out var value) ? value?.ToString() ?? string.Empty : string.Empty;
    }

    private static string? GetNullableString(IReadOnlyDictionary<string, object> properties, string key)
    {
        return properties.TryGetValue(key, out var value) ? value?.ToString() : null;
    }

    private static double GetDouble(IReadOnlyDictionary<string, object> properties, string key)
    {
        if (!properties.TryGetValue(key, out var value))
        {
            return 0;
        }

        return value switch
        {
            double doubleValue => doubleValue,
            float floatValue => floatValue,
            int intValue => intValue,
            long longValue => longValue,
            _ => double.TryParse(value.ToString(), out var parsed) ? parsed : 0
        };
    }

    private static List<string> GetStringList(IReadOnlyDictionary<string, object> properties, string key)
    {
        if (!properties.TryGetValue(key, out var value))
        {
            return [];
        }

        return value switch
        {
            IEnumerable<string> strings => strings.Where(item => item.Length > 0).ToList(),
            IEnumerable<object> objects => objects.Select(item => item.ToString() ?? string.Empty).Where(item => item.Length > 0).ToList(),
            _ => []
        };
    }
}
