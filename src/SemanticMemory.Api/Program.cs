using System.Text.Json.Serialization;
using SemanticMemory.Application;
using SemanticMemory.Application.Abstractions;
using SemanticMemory.Application.Models;
using SemanticMemory.Domain;
using SemanticMemory.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddApplication();
builder.Services.AddInfrastructure();

var app = builder.Build();

var postgresSchemaInitializer = app.Services.GetService<PostgresSchemaInitializer>();

if (postgresSchemaInitializer is not null)
{
    await postgresSchemaInitializer.InitializeAsync(app.Lifetime.ApplicationStopping);
}

var neo4jSchemaInitializer = app.Services.GetService<Neo4jSchemaInitializer>();

if (neo4jSchemaInitializer is not null)
{
    try
    {
        await neo4jSchemaInitializer.InitializeAsync(app.Lifetime.ApplicationStopping);
    }
    catch (Exception exception) when (!IsEnabled(builder.Configuration["NEO4J_STRICT_STARTUP"]))
    {
        app.Logger.LogWarning(
            exception,
            "Neo4j schema initialization failed. The API will continue running, but graph operations may fail until Neo4j connectivity is restored.");
    }
}

var swaggerEnabled = app.Environment.IsDevelopment() ||
    IsEnabled(builder.Configuration["ENABLE_SWAGGER"]) ||
    IsEnabled(builder.Configuration["SWAGGER_ENABLED"]);

if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => Results.Ok(new
    {
        name = "Hybrid Semantic Memory",
        status = "ok",
        health = "/health",
        swagger = swaggerEnabled ? "/swagger" : null,
        openApi = swaggerEnabled ? "/swagger/v1/swagger.json" : null
    }))
    .WithName("semantic_memory_root")
    .WithOpenApi();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("semantic_memory_health")
    .WithOpenApi();

var memory = app.MapGroup("/api/memory")
    .WithTags("Memory");

memory.MapPost("/ingest", async (
        IngestRequest request,
        IMemoryIngestionService ingestionService,
        CancellationToken cancellationToken) =>
    {
        var result = await ingestionService.IngestMessageAsync(
            new IngestMessageCommand(
                request.TenantId,
                request.UserId,
                request.ConversationId,
                request.Text,
                request.SourceType),
            cancellationToken);

        return Results.Ok(new IngestResponse(
            result.MemoryChunkId,
            result.Entities,
            result.Relations,
            result.UpsertedNodes.Count,
            result.UpsertedEdges.Count,
            result.EvidenceCount));
    })
    .WithName("semantic_memory_ingest")
    .WithOpenApi(operation =>
    {
        operation.OperationId = "semantic_memory_ingest";
        operation.Summary = "Store conversation text in semantic memory.";
        operation.Description = "Persists a memory chunk, extracts semantic entities and relations, and stores evidence for later retrieval.";
        return operation;
    });

memory.MapPost("/retrieve", async (
        RetrieveRequest request,
        IMemoryRetriever retriever,
        CancellationToken cancellationToken) =>
    {
        var context = await retriever.RetrieveContextAsync(
            new RetrieveMemoryQuery(
                request.TenantId,
                request.UserId,
                request.Prompt,
                request.VectorLimit,
                request.NodeLimit,
                request.GraphDepth,
                request.MaxContextTokens),
            cancellationToken);

        var nodesById = context.RelevantNodes.ToDictionary(node => node.Id);
        var facts = context.RelevantEdges.Select(edge =>
        {
            var source = nodesById.TryGetValue(edge.Edge.SourceNodeId, out var sourceNode)
                ? sourceNode.CanonicalName
                : edge.Edge.SourceNodeId.ToString();

            var target = nodesById.TryGetValue(edge.Edge.TargetNodeId, out var targetNode)
                ? targetNode.CanonicalName
                : edge.Edge.TargetNodeId.ToString();

            return new FactResponse(
                edge.Edge.Id,
                source,
                edge.Edge.RelationType,
                target,
                edge.Edge.Confidence,
                edge.Score);
        }).ToArray();

        var evidence = context.Evidence.Select(item =>
            new EvidenceResponse(
                item.EdgeId,
                item.MemoryChunkId,
                item.Quote,
                item.SourceType,
                item.Confidence,
                item.CreatedAt)).ToArray();

        return Results.Ok(new RetrieveResponse(context.ContextText, facts, evidence));
    })
    .WithName("semantic_memory_retrieve")
    .WithOpenApi(operation =>
    {
        operation.OperationId = "semantic_memory_retrieve";
        operation.Summary = "Retrieve semantic memory context.";
        operation.Description = "Returns compact memory context, relevant semantic facts, and supporting evidence for a user prompt.";
        return operation;
    });

memory.MapPost("/facts", async (
        RememberFactRequest request,
        IManualFactService manualFactService,
        CancellationToken cancellationToken) =>
    {
        var edge = await manualFactService.RememberFactAsync(
            new RememberFactCommand(
                request.TenantId,
                request.UserId,
                request.Subject,
                request.Predicate,
                request.Object,
                request.Confidence,
                request.EvidenceQuote,
                request.ValidFrom,
                request.ValidTo),
            cancellationToken);

        return Results.Ok(new RememberFactResponse(edge.Id, edge.Status, edge.Confidence));
    })
    .WithName("semantic_memory_facts")
    .WithOpenApi(operation =>
    {
        operation.OperationId = "semantic_memory_facts";
        operation.Summary = "Store a manual semantic fact.";
        operation.Description = "Creates a direct semantic fact with confidence, evidence, and optional validity dates.";
        return operation;
    });

memory.MapGet("/explain/{edgeId:guid}", async (
        Guid edgeId,
        string tenantId,
        string userId,
        IMemoryExplanationService explanationService,
        CancellationToken cancellationToken) =>
    {
        var evidence = await explanationService.ExplainEdgeAsync(
            new ExplainEdgeQuery(tenantId, userId, edgeId),
            cancellationToken);

        return Results.Ok(new ExplainResponse(
            edgeId,
            evidence.Select(item =>
                new EvidenceResponse(
                    item.EdgeId,
                    item.MemoryChunkId,
                    item.Quote,
                    item.SourceType,
                    item.Confidence,
                    item.CreatedAt)).ToArray()));
    })
    .WithName("semantic_memory_explain")
    .WithOpenApi(operation =>
    {
        operation.OperationId = "semantic_memory_explain";
        operation.Summary = "Explain why a semantic fact is remembered.";
        operation.Description = "Returns the evidence attached to a remembered semantic edge.";
        return operation;
    });

memory.MapDelete("/{memoryChunkId:guid}", async (
        Guid memoryChunkId,
        string tenantId,
        string userId,
        IMemoryForgettingService forgettingService,
        CancellationToken cancellationToken) =>
    {
        await forgettingService.ForgetAsync(
            new ForgetMemoryCommand(tenantId, userId, memoryChunkId),
            cancellationToken);

        return Results.NoContent();
    })
    .WithName("semantic_memory_forget")
    .WithOpenApi(operation =>
    {
        operation.OperationId = "semantic_memory_forget";
        operation.Summary = "Forget a memory chunk.";
        operation.Description = "Performs logical deletion of a memory chunk and its related facts for the tenant and user.";
        return operation;
    });

static bool IsEnabled(string? value)
{
    return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
}

app.Run();

public sealed record IngestRequest(
    string TenantId,
    string UserId,
    string? ConversationId,
    string Text,
    SourceType SourceType = SourceType.Conversation);

public sealed record IngestResponse(
    Guid MemoryChunkId,
    IReadOnlyList<ExtractedEntity> Entities,
    IReadOnlyList<ExtractedRelation> Relations,
    int UpsertedNodeCount,
    int UpsertedEdgeCount,
    int EvidenceCount);

public sealed record RetrieveRequest(
    string TenantId,
    string UserId,
    string Prompt,
    int VectorLimit = 10,
    int NodeLimit = 10,
    int GraphDepth = 2,
    int MaxContextTokens = 1200);

public sealed record RetrieveResponse(
    string Context,
    IReadOnlyList<FactResponse> Facts,
    IReadOnlyList<EvidenceResponse> Evidence);

public sealed record FactResponse(
    Guid EdgeId,
    string Subject,
    string Predicate,
    string Object,
    double Confidence,
    double Score);

public sealed record EvidenceResponse(
    Guid EdgeId,
    Guid MemoryChunkId,
    string? Quote,
    SourceType SourceType,
    double Confidence,
    DateTimeOffset CreatedAt);

public sealed record RememberFactRequest(
    string TenantId,
    string UserId,
    string Subject,
    string Predicate,
    string Object,
    double Confidence,
    string? EvidenceQuote,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo);

public sealed record RememberFactResponse(
    Guid EdgeId,
    MemoryStatus Status,
    double Confidence);

public sealed record ExplainResponse(
    Guid EdgeId,
    IReadOnlyList<EvidenceResponse> Evidence);

public partial class Program;
