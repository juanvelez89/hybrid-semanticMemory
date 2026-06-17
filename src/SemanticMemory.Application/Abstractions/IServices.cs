using SemanticMemory.Application.Models;
using SemanticMemory.Domain;

namespace SemanticMemory.Application.Abstractions;

public interface IMemoryIngestionService
{
    Task<IngestionResult> IngestMessageAsync(
        IngestMessageCommand command,
        CancellationToken cancellationToken);
}

public interface IMemoryRetriever
{
    Task<MemoryContext> RetrieveContextAsync(
        RetrieveMemoryQuery query,
        CancellationToken cancellationToken);
}

public interface IManualFactService
{
    Task<SemanticEdge> RememberFactAsync(
        RememberFactCommand command,
        CancellationToken cancellationToken);
}

public interface IMemoryForgettingService
{
    Task ForgetAsync(
        ForgetMemoryCommand command,
        CancellationToken cancellationToken);
}

public interface IMemoryExplanationService
{
    Task<IReadOnlyList<Evidence>> ExplainEdgeAsync(
        ExplainEdgeQuery query,
        CancellationToken cancellationToken);
}

public interface IPromptContextBuilder
{
    string BuildContext(MemoryContext memoryContext, int maxTokens);
}
