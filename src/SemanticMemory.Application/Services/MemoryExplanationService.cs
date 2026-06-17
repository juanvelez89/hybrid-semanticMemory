using SemanticMemory.Application.Abstractions;
using SemanticMemory.Application.Models;
using SemanticMemory.Domain;

namespace SemanticMemory.Application.Services;

public sealed class MemoryExplanationService(IEvidenceStore evidenceStore) : IMemoryExplanationService
{
    public Task<IReadOnlyList<Evidence>> ExplainEdgeAsync(
        ExplainEdgeQuery query,
        CancellationToken cancellationToken)
    {
        MemoryValidation.RequireTenantAndUser(query.TenantId, query.UserId);

        return evidenceStore.GetEvidenceForEdgeAsync(
            query.TenantId,
            query.UserId,
            query.EdgeId,
            cancellationToken);
    }
}
