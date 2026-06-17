using System.Text;
using SemanticMemory.Application.Abstractions;
using SemanticMemory.Application.Models;

namespace SemanticMemory.Application.Services;

public sealed class PromptContextBuilder : IPromptContextBuilder
{
    public string BuildContext(MemoryContext memoryContext, int maxTokens)
    {
        var maxChars = Math.Max(500, maxTokens * 4);
        var nodesById = memoryContext.RelevantNodes.ToDictionary(node => node.Id);
        var evidenceByEdgeId = memoryContext.Evidence
            .GroupBy(evidence => evidence.EdgeId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var builder = new StringBuilder();
        builder.AppendLine("Relevant memory:");

        foreach (var scoredChunk in memoryContext.SimilarChunks.Take(5))
        {
            AppendBoundedLine(builder, $"- [{scoredChunk.Chunk.CreatedAt:yyyy-MM-dd}] {TextFor(scoredChunk.Chunk.RawText)}", maxChars);
        }

        if (memoryContext.RelevantEdges.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Known facts:");

            foreach (var scoredEdge in memoryContext.RelevantEdges.Take(10))
            {
                var edge = scoredEdge.Edge;
                var subject = nodesById.TryGetValue(edge.SourceNodeId, out var sourceNode)
                    ? sourceNode.CanonicalName
                    : edge.SourceNodeId.ToString();

                var target = nodesById.TryGetValue(edge.TargetNodeId, out var targetNode)
                    ? targetNode.CanonicalName
                    : edge.TargetNodeId.ToString();

                var evidenceQuote = evidenceByEdgeId.TryGetValue(edge.Id, out var evidence)
                    ? evidence.Select(item => item.Quote).FirstOrDefault(quote => !string.IsNullOrWhiteSpace(quote))
                    : null;

                var line = $"- {subject} {edge.RelationType} {target}. Confidence: {edge.Confidence:0.00}.";

                if (!string.IsNullOrWhiteSpace(evidenceQuote))
                {
                    line += $" Evidence: \"{TextFor(evidenceQuote)}\"";
                }

                AppendBoundedLine(builder, line, maxChars);
            }
        }

        var result = builder.ToString().Trim();
        return result.Length <= maxChars ? result : result[..maxChars].Trim();
    }

    private static void AppendBoundedLine(StringBuilder builder, string line, int maxChars)
    {
        if (builder.Length >= maxChars)
        {
            return;
        }

        var remaining = maxChars - builder.Length;
        builder.AppendLine(line.Length <= remaining ? line : line[..remaining].Trim());
    }

    private static string TextFor(string text)
    {
        const int maxLength = 280;
        var normalized = text.ReplaceLineEndings(" ").Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength].Trim() + "...";
    }
}
