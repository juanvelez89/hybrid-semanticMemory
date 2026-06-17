namespace SemanticMemory.Application.Models;

public sealed record ExtractedEntity(
    string Name,
    string Type,
    double Confidence);

public sealed record NormalizedEntity(
    string CanonicalName,
    string NormalizedKey,
    string Type,
    IReadOnlyList<string> Aliases,
    double Confidence);

public sealed record ExtractedRelation(
    string Subject,
    string Predicate,
    string Object,
    double Confidence,
    string? EvidenceQuote);
