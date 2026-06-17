using SemanticMemory.Domain;

namespace SemanticMemory.Application.Models;

public sealed record IngestMessageCommand(
    string TenantId,
    string UserId,
    string? ConversationId,
    string Text,
    SourceType SourceType = SourceType.Conversation);

public sealed record RetrieveMemoryQuery(
    string TenantId,
    string UserId,
    string Prompt,
    int VectorLimit = 10,
    int NodeLimit = 10,
    int GraphDepth = 2,
    int MaxContextTokens = 1200);

public sealed record RememberFactCommand(
    string TenantId,
    string UserId,
    string Subject,
    string Predicate,
    string Object,
    double Confidence,
    string? EvidenceQuote,
    DateTimeOffset? ValidFrom = null,
    DateTimeOffset? ValidTo = null);

public sealed record ForgetMemoryCommand(
    string TenantId,
    string UserId,
    Guid MemoryChunkId);

public sealed record ExplainEdgeQuery(
    string TenantId,
    string UserId,
    Guid EdgeId);
