namespace SemanticMemory.Domain;

public sealed class SemanticEdge
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public Guid SourceNodeId { get; set; }
    public Guid TargetNodeId { get; set; }
    public string RelationType { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public double Weight { get; set; }
    public MemoryStatus Status { get; set; } = MemoryStatus.Active;
    public DateTimeOffset? ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
