namespace SemanticMemory.Application.Services;

internal static class MemoryValidation
{
    public static void RequireTenantAndUser(string tenantId, string userId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("UserId is required.", nameof(userId));
        }
    }

    public static void RequireText(string text, string paramName)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException($"{paramName} is required.", paramName);
        }
    }
}
