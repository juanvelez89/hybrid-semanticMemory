using SemanticMemory.Application.Abstractions;
using SemanticMemory.Application.Models;
using SemanticMemory.Domain;

namespace SemanticMemory.Application.Services;

public sealed class MemoryIngestionService(
    IApplicationClock clock,
    IEmbeddingProvider embeddingProvider,
    IEntityExtractor entityExtractor,
    IRelationExtractor relationExtractor,
    IEntityNormalizer entityNormalizer,
    IVectorMemoryStore vectorMemoryStore,
    ISemanticGraphStore semanticGraphStore,
    IEvidenceStore evidenceStore,
    IMemoryEventStore memoryEventStore) : IMemoryIngestionService
{
    public async Task<IngestionResult> IngestMessageAsync(
        IngestMessageCommand command,
        CancellationToken cancellationToken)
    {
        MemoryValidation.RequireTenantAndUser(command.TenantId, command.UserId);
        MemoryValidation.RequireText(command.Text, nameof(command.Text));

        var now = clock.UtcNow;
        var embedding = await embeddingProvider.CreateEmbeddingAsync(command.Text, cancellationToken);

        var chunk = new MemoryChunk
        {
            Id = Guid.NewGuid(),
            TenantId = command.TenantId,
            UserId = command.UserId,
            ConversationId = command.ConversationId,
            RawText = command.Text.Trim(),
            Embedding = embedding,
            MemoryType = MemoryType.LongTermMemory,
            SourceType = command.SourceType,
            Status = MemoryStatus.Active,
            Importance = 0.5,
            CreatedAt = now,
            UpdatedAt = now
        };

        await vectorMemoryStore.SaveEmbeddingAsync(chunk, cancellationToken);
        await SaveEventAsync(command.TenantId, command.UserId, "MemoryChunkCreated", nameof(MemoryChunk), chunk.Id, cancellationToken);
        await SaveEventAsync(command.TenantId, command.UserId, "EmbeddingCreated", nameof(MemoryChunk), chunk.Id, cancellationToken);

        var entities = await entityExtractor.ExtractEntitiesAsync(command.Text, cancellationToken);
        var relations = await relationExtractor.ExtractRelationsAsync(command.Text, cancellationToken);
        await SaveEventAsync(command.TenantId, command.UserId, "EntitiesExtracted", nameof(MemoryChunk), chunk.Id, cancellationToken);
        await SaveEventAsync(command.TenantId, command.UserId, "RelationsExtracted", nameof(MemoryChunk), chunk.Id, cancellationToken);

        var nodesByKey = new Dictionary<string, SemanticNode>(StringComparer.OrdinalIgnoreCase);
        var upsertedNodes = new List<SemanticNode>();

        foreach (var entity in entities)
        {
            var node = await CreateOrUpdateNodeAsync(command.TenantId, command.UserId, entity, now, cancellationToken);
            nodesByKey[node.CanonicalName] = node;
            nodesByKey[node.NormalizedKey] = node;
            upsertedNodes.Add(node);
        }

        var upsertedEdges = new List<SemanticEdge>();
        var evidenceCount = 0;

        foreach (var relation in relations)
        {
            var source = await EnsureRelationNodeAsync(command.TenantId, command.UserId, relation.Subject, entities, nodesByKey, now, cancellationToken);
            var target = await EnsureRelationNodeAsync(command.TenantId, command.UserId, relation.Object, entities, nodesByKey, now, cancellationToken);

            if (!upsertedNodes.Any(node => node.Id == source.Id))
            {
                upsertedNodes.Add(source);
            }

            if (!upsertedNodes.Any(node => node.Id == target.Id))
            {
                upsertedNodes.Add(target);
            }

            var edge = new SemanticEdge
            {
                Id = Guid.NewGuid(),
                TenantId = command.TenantId,
                UserId = command.UserId,
                SourceNodeId = source.Id,
                TargetNodeId = target.Id,
                RelationType = NormalizeRelationType(relation.Predicate),
                Confidence = ClampConfidence(relation.Confidence),
                Weight = ClampConfidence(relation.Confidence),
                Status = MemoryStatus.Active,
                ValidFrom = now,
                CreatedAt = now,
                UpdatedAt = now
            };

            var savedEdge = await semanticGraphStore.UpsertEdgeAsync(edge, cancellationToken);
            upsertedEdges.Add(savedEdge);
            await SaveEventAsync(command.TenantId, command.UserId, "EdgeUpserted", nameof(SemanticEdge), savedEdge.Id, cancellationToken);

            var quoteConfidence = QuoteIsExact(command.Text, relation.EvidenceQuote)
                ? ClampConfidence(relation.Confidence)
                : ClampConfidence(relation.Confidence * 0.8);

            var evidence = new Evidence
            {
                Id = Guid.NewGuid(),
                TenantId = command.TenantId,
                UserId = command.UserId,
                EdgeId = savedEdge.Id,
                MemoryChunkId = chunk.Id,
                Quote = string.IsNullOrWhiteSpace(relation.EvidenceQuote) ? command.Text : relation.EvidenceQuote,
                SourceType = command.SourceType,
                Confidence = quoteConfidence,
                CreatedAt = now
            };

            await evidenceStore.SaveEvidenceAsync(evidence, cancellationToken);
            await SaveEventAsync(command.TenantId, command.UserId, "EvidenceCreated", nameof(Evidence), evidence.Id, cancellationToken);
            evidenceCount++;
        }

        return new IngestionResult(
            chunk.Id,
            entities,
            relations,
            upsertedNodes
                .GroupBy(node => node.Id)
                .Select(group => group.First())
                .ToArray(),
            upsertedEdges,
            evidenceCount);
    }

    private async Task<SemanticNode> CreateOrUpdateNodeAsync(
        string tenantId,
        string userId,
        ExtractedEntity entity,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var normalized = await entityNormalizer.NormalizeAsync(tenantId, userId, entity, cancellationToken);
        var node = new SemanticNode
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            Type = normalized.Type,
            CanonicalName = normalized.CanonicalName,
            NormalizedKey = normalized.NormalizedKey,
            Aliases = normalized.Aliases.ToList(),
            Status = MemoryStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        var savedNode = await semanticGraphStore.UpsertNodeAsync(node, cancellationToken);
        await SaveEventAsync(tenantId, userId, "NodeUpserted", nameof(SemanticNode), savedNode.Id, cancellationToken);
        return savedNode;
    }

    private async Task<SemanticNode> EnsureRelationNodeAsync(
        string tenantId,
        string userId,
        string name,
        IReadOnlyList<ExtractedEntity> extractedEntities,
        Dictionary<string, SemanticNode> nodesByKey,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (nodesByKey.TryGetValue(name, out var existing))
        {
            return existing;
        }

        var matchingEntity = extractedEntities.FirstOrDefault(entity =>
            string.Equals(entity.Name, name, StringComparison.OrdinalIgnoreCase));

        var entity = matchingEntity ?? new ExtractedEntity(name, InferEntityType(name, userId), 0.75);
        var node = await CreateOrUpdateNodeAsync(tenantId, userId, entity, now, cancellationToken);

        nodesByKey[node.CanonicalName] = node;
        nodesByKey[node.NormalizedKey] = node;
        nodesByKey[name] = node;
        return node;
    }

    private Task SaveEventAsync(
        string tenantId,
        string userId,
        string eventType,
        string entityType,
        Guid entityId,
        CancellationToken cancellationToken)
    {
        return memoryEventStore.SaveEventAsync(
            new MemoryEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = userId,
                EventType = eventType,
                EntityType = entityType,
                EntityId = entityId,
                PayloadJson = "{}",
                CreatedAt = clock.UtcNow
            },
            cancellationToken);
    }

    private static string InferEntityType(string name, string userId)
    {
        if (string.Equals(name, userId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "user", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "usuario", StringComparison.OrdinalIgnoreCase))
        {
            return "Person";
        }

        return "Other";
    }

    private static string NormalizeRelationType(string relationType)
    {
        return string.IsNullOrWhiteSpace(relationType)
            ? "relatedTo"
            : relationType.Trim();
    }

    private static double ClampConfidence(double confidence)
    {
        return Math.Max(0, Math.Min(1, confidence));
    }

    private static bool QuoteIsExact(string text, string? quote)
    {
        return string.IsNullOrWhiteSpace(quote) ||
            text.Contains(quote, StringComparison.OrdinalIgnoreCase);
    }
}
