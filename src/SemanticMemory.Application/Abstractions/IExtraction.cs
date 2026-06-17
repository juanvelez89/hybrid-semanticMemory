using SemanticMemory.Application.Models;

namespace SemanticMemory.Application.Abstractions;

public interface IEntityExtractor
{
    Task<IReadOnlyList<ExtractedEntity>> ExtractEntitiesAsync(
        string text,
        CancellationToken cancellationToken);
}

public interface IRelationExtractor
{
    Task<IReadOnlyList<ExtractedRelation>> ExtractRelationsAsync(
        string text,
        CancellationToken cancellationToken);
}

public interface IEntityNormalizer
{
    Task<NormalizedEntity> NormalizeAsync(
        string tenantId,
        string userId,
        ExtractedEntity entity,
        CancellationToken cancellationToken);
}
