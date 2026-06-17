namespace SemanticMemory.Domain;

public sealed class MemoryChunk
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string? ConversationId { get; set; }
    public string RawText { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public float[]? Embedding { get; set; }
    public MemoryType MemoryType { get; set; } = MemoryType.LongTermMemory;
    public MemoryStatus Status { get; set; } = MemoryStatus.Active;
    public SourceType SourceType { get; set; } = SourceType.Conversation;
    public double Importance { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ForgottenAt { get; set; }
}
