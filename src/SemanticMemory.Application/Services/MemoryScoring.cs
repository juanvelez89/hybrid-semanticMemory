using SemanticMemory.Domain;

namespace SemanticMemory.Application.Services;

internal static class MemoryScoring
{
    public static double CalculateHybridScore(
        double semanticSimilarity,
        double graphRelevance,
        double confidence,
        DateTimeOffset createdAt,
        DateTimeOffset now)
    {
        return Clamp01(
            (Clamp01(semanticSimilarity) * 0.45)
            + (Clamp01(graphRelevance) * 0.25)
            + (Clamp01(confidence) * 0.20)
            + (CalculateRecency(createdAt, now) * 0.10));
    }

    public static double CalculateRecency(DateTimeOffset createdAt, DateTimeOffset now)
    {
        var age = now - createdAt;

        if (age <= TimeSpan.FromDays(7))
        {
            return 1.0;
        }

        if (age <= TimeSpan.FromDays(30))
        {
            return 0.75;
        }

        if (age <= TimeSpan.FromDays(90))
        {
            return 0.50;
        }

        return 0.25;
    }

    public static double ConfidenceFor(SemanticEdge edge)
    {
        return Clamp01(edge.Confidence <= 0 ? edge.Weight : edge.Confidence);
    }

    private static double Clamp01(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 0;
        }

        return Math.Max(0, Math.Min(1, value));
    }
}
