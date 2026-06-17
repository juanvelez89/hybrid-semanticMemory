using SemanticMemory.Application.Abstractions;
using SemanticMemory.Application.Models;
using SemanticMemory.Domain;

namespace SemanticMemory.Application.Services;

public sealed class ManualFactService(
    IApplicationClock clock,
    IEmbeddingProvider embeddingProvider,
    IEntityNormalizer entityNormalizer,
    IVectorMemoryStore vectorMemoryStore,
    ISemanticGraphStore semanticGraphStore,
    IEvidenceStore evidenceStore,
    IMemoryEventStore memoryEventStore) : IManualFactService
{
    public async Task<SemanticEdge> RememberFactAsync(
        RememberFactCommand command,
        CancellationToken cancellationToken)
    {
        MemoryValidation.RequireTenantAndUser(command.TenantId, command.UserId);
        MemoryValidation.RequireText(command.Subject, nameof(command.Subject));
        MemoryValidation.RequireText(command.Predicate, nameof(command.Predicate));
        MemoryValidation.RequireText(command.Object, nameof(command.Object));

        var now = clock.UtcNow;
        var text = command.EvidenceQuote;

        if (string.IsNullOrWhiteSpace(text))
        {
            text = $"{command.Subject} {command.Predicate} {command.Object}.";
        }

        var chunk = new MemoryChunk
        {
            Id = Guid.NewGuid(),
            TenantId = command.TenantId,
            UserId = command.UserId,
            RawText = text,
            Embedding = await embeddingProvider.CreateEmbeddingAsync(text, cancellationToken),
            MemoryType = MemoryType.LongTermMemory,
            SourceType = SourceType.ManualFact,
            Status = MemoryStatus.Active,
            Importance = 0.75,
            CreatedAt = now,
            UpdatedAt = now
        };

        await vectorMemoryStore.SaveEmbeddingAsync(chunk, cancellationToken);
        await SaveEventAsync(command.TenantId, command.UserId, "MemoryChunkCreated", nameof(MemoryChunk), chunk.Id, cancellationToken);

        var source = await UpsertNodeAsync(command.TenantId, command.UserId, command.Subject, InferEntityType(command.Subject, command.UserId), now, cancellationToken);
        var target = await UpsertNodeAsync(command.TenantId, command.UserId, command.Object, "Other", now, cancellationToken);

        var edge = new SemanticEdge
        {
            Id = Guid.NewGuid(),
            TenantId = command.TenantId,
            UserId = command.UserId,
            SourceNodeId = source.Id,
            TargetNodeId = target.Id,
            RelationType = command.Predicate.Trim(),
            Confidence = Math.Max(0, Math.Min(1, command.Confidence)),
            Weight = Math.Max(0, Math.Min(1, command.Confidence)),
            Status = MemoryStatus.Active,
            ValidFrom = command.ValidFrom ?? now,
            ValidTo = command.ValidTo,
            CreatedAt = now,
            UpdatedAt = now
        };

        var savedEdge = await semanticGraphStore.UpsertEdgeAsync(edge, cancellationToken);

        await evidenceStore.SaveEvidenceAsync(
            new Evidence
            {
                Id = Guid.NewGuid(),
                TenantId = command.TenantId,
                UserId = command.UserId,
                EdgeId = savedEdge.Id,
                MemoryChunkId = chunk.Id,
                Quote = text,
                SourceType = SourceType.ManualFact,
                Confidence = edge.Confidence,
                CreatedAt = now
            },
            cancellationToken);

        await SaveEventAsync(command.TenantId, command.UserId, "EdgeUpserted", nameof(SemanticEdge), savedEdge.Id, cancellationToken);
        return savedEdge;
    }

    private async Task<SemanticNode> UpsertNodeAsync(
        string tenantId,
        string userId,
        string name,
        string type,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var normalized = await entityNormalizer.NormalizeAsync(
            tenantId,
            userId,
            new ExtractedEntity(name, type, 1),
            cancellationToken);

        return await semanticGraphStore.UpsertNodeAsync(
            new SemanticNode
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
            },
            cancellationToken);
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
        return string.Equals(name, userId, StringComparison.OrdinalIgnoreCase)
            ? "Person"
            : "Other";
    }
}
