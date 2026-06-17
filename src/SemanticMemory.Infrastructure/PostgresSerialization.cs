using System.Globalization;
using SemanticMemory.Domain;

namespace SemanticMemory.Infrastructure;

internal static class PostgresSerialization
{
    public static string? ToVectorLiteral(float[]? vector)
    {
        if (vector is null)
        {
            return null;
        }

        return "[" + string.Join(",", vector.Select(value => value.ToString("G9", CultureInfo.InvariantCulture))) + "]";
    }

    public static float[]? ParseVector(string? vectorText)
    {
        if (string.IsNullOrWhiteSpace(vectorText))
        {
            return null;
        }

        var trimmed = vectorText.Trim('[', ']');

        if (trimmed.Length == 0)
        {
            return [];
        }

        return trimmed
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => float.Parse(value, CultureInfo.InvariantCulture))
            .ToArray();
    }

    public static string ToDbString(this MemoryStatus status) => status.ToString();

    public static string ToDbString(this MemoryType memoryType) => memoryType.ToString();

    public static string ToDbString(this SourceType sourceType) => sourceType.ToString();

    public static MemoryStatus ParseMemoryStatus(string value)
    {
        return Enum.TryParse<MemoryStatus>(value, ignoreCase: true, out var parsed)
            ? parsed
            : MemoryStatus.Active;
    }

    public static MemoryType ParseMemoryType(string value)
    {
        return Enum.TryParse<MemoryType>(value, ignoreCase: true, out var parsed)
            ? parsed
            : MemoryType.LongTermMemory;
    }

    public static SourceType ParseSourceType(string value)
    {
        return Enum.TryParse<SourceType>(value, ignoreCase: true, out var parsed)
            ? parsed
            : SourceType.Conversation;
    }
}
