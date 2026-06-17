namespace SemanticMemory.Domain;

public sealed class SemanticNode
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string CanonicalName { get; set; } = string.Empty;
    public string NormalizedKey { get; set; } = string.Empty;
    public List<string> Aliases { get; set; } = [];
    public string? Description { get; set; }
    public MemoryStatus Status { get; set; } = MemoryStatus.Active;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
