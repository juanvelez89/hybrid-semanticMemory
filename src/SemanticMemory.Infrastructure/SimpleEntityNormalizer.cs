using System.Globalization;
using System.Text;
using SemanticMemory.Application.Abstractions;
using SemanticMemory.Application.Models;

namespace SemanticMemory.Infrastructure;

public sealed class SimpleEntityNormalizer : IEntityNormalizer
{
    public Task<NormalizedEntity> NormalizeAsync(
        string tenantId,
        string userId,
        ExtractedEntity entity,
        CancellationToken cancellationToken)
    {
        var canonicalName = string.IsNullOrWhiteSpace(entity.Name)
            ? "unknown"
            : entity.Name.Trim();

        var normalizedKey = NormalizeKey(canonicalName);

        return Task.FromResult(
            new NormalizedEntity(
                canonicalName,
                normalizedKey,
                string.IsNullOrWhiteSpace(entity.Type) ? "Other" : entity.Type.Trim(),
                [canonicalName],
                entity.Confidence));
    }

    public static string NormalizeKey(string value)
    {
        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);

            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
        }

        return builder.Length == 0 ? "unknown" : builder.ToString();
    }
}
