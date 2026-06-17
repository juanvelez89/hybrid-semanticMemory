namespace SemanticMemory.Domain;

public sealed class Evidence
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public Guid EdgeId { get; set; }
    public Guid MemoryChunkId { get; set; }
    public string? Quote { get; set; }
    public SourceType SourceType { get; set; }
    public double Confidence { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
