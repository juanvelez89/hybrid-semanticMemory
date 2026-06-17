using SemanticMemory.Application.Abstractions;
using SemanticMemory.Application.Models;

namespace SemanticMemory.Infrastructure;

public sealed class FakeEntityExtractor : IEntityExtractor
{
    private static readonly (string Needle, string Name, string Type)[] KnownEntities =
    [
        ("semantic memory engine", "Semantic Memory Engine", "Project"),
        ("motor de memoria semantica", "Semantic Memory Engine", "Project"),
        ("memoria semantica", "Semantic Memory", "Concept"),
        ("llms", "LLMs", "Technology"),
        ("llm", "LLMs", "Technology"),
        ("neo4j", "Neo4j", "Database"),
        ("pgvector", "pgvector", "DatabaseExtension"),
        ("graphql", "GraphQL", "ApiStyle"),
        (".net", ".NET", "Technology"),
        ("dotnet", ".NET", "Technology"),
        ("postgresql", "PostgreSQL", "Database"),
        ("postgres", "PostgreSQL", "Database"),
        ("openai", "OpenAI", "Platform")
    ];

    public Task<IReadOnlyList<ExtractedEntity>> ExtractEntitiesAsync(
        string text,
        CancellationToken cancellationToken)
    {
        var entities = KnownEntities
            .Where(entity => text.Contains(entity.Needle, StringComparison.OrdinalIgnoreCase))
            .GroupBy(entity => entity.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var entity = group.First();
                return new ExtractedEntity(entity.Name, entity.Type, 0.9);
            })
            .ToArray();

        return Task.FromResult<IReadOnlyList<ExtractedEntity>>(entities);
    }
}

public sealed class FakeRelationExtractor : IRelationExtractor
{
    public Task<IReadOnlyList<ExtractedRelation>> ExtractRelationsAsync(
        string text,
        CancellationToken cancellationToken)
    {
        var relations = new List<ExtractedRelation>();
        var mentionsSemanticMemory = text.Contains("memoria semantica", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("semantic memory", StringComparison.OrdinalIgnoreCase);

        if (mentionsSemanticMemory &&
            (text.Contains("construyendo", StringComparison.OrdinalIgnoreCase) ||
             text.Contains("building", StringComparison.OrdinalIgnoreCase)))
        {
            relations.Add(new ExtractedRelation("user", "isBuilding", "Semantic Memory Engine", 0.92, text));
        }

        AddUsesRelationIfMentioned(text, relations, "Neo4j");
        AddUsesRelationIfMentioned(text, relations, "pgvector");
        AddUsesRelationIfMentioned(text, relations, "PostgreSQL");

        if (text.Contains("GraphQL", StringComparison.OrdinalIgnoreCase))
        {
            relations.Add(new ExtractedRelation("Semantic Memory Engine", "exposesApiWith", "GraphQL", 0.82, text));
        }

        if ((text.Contains(".NET", StringComparison.OrdinalIgnoreCase) ||
             text.Contains("dotnet", StringComparison.OrdinalIgnoreCase)) &&
            (text.Contains("experiencia", StringComparison.OrdinalIgnoreCase) ||
             text.Contains("skill", StringComparison.OrdinalIgnoreCase)))
        {
            relations.Add(new ExtractedRelation("user", "hasSkill", ".NET", 0.88, text));
        }

        return Task.FromResult<IReadOnlyList<ExtractedRelation>>(relations);
    }

    private static void AddUsesRelationIfMentioned(
        string text,
        List<ExtractedRelation> relations,
        string technology)
    {
        if (!text.Contains(technology, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        relations.Add(new ExtractedRelation("Semantic Memory Engine", "uses", technology, 0.9, text));
    }
}
