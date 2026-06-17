using Microsoft.Extensions.DependencyInjection;
using SemanticMemory.Application;
using SemanticMemory.Application.Abstractions;
using SemanticMemory.Application.Models;
using SemanticMemory.Domain;
using SemanticMemory.Infrastructure;

namespace SemanticMemory.UnitTests;

public sealed class MemoryFlowTests
{
    [Fact]
    public async Task IngestMessage_creates_chunk_nodes_edges_and_evidence()
    {
        var services = CreateServices();
        var ingestion = services.GetRequiredService<IMemoryIngestionService>();

        var result = await ingestion.IngestMessageAsync(
            new IngestMessageCommand(
                "default",
                "juan",
                "conv-001",
                "Estoy construyendo un motor de memoria semantica para LLMs usando Neo4j, pgvector y GraphQL."),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.MemoryChunkId);
        Assert.NotEmpty(result.Entities);
        Assert.NotEmpty(result.Relations);
        Assert.NotEmpty(result.UpsertedNodes);
        Assert.NotEmpty(result.UpsertedEdges);
        Assert.Equal(result.UpsertedEdges.Count, result.EvidenceCount);
    }

    [Fact]
    public async Task RetrieveMemory_returns_hybrid_context_with_facts_and_evidence()
    {
        var services = CreateServices();
        var ingestion = services.GetRequiredService<IMemoryIngestionService>();
        var retriever = services.GetRequiredService<IMemoryRetriever>();

        await ingestion.IngestMessageAsync(
            new IngestMessageCommand(
                "default",
                "juan",
                "conv-001",
                "Estoy construyendo un motor de memoria semantica para LLMs usando Neo4j y pgvector."),
            CancellationToken.None);

        var context = await retriever.RetrieveContextAsync(
            new RetrieveMemoryQuery("default", "juan", "Que arquitectura tenia mi motor?", VectorLimit: 5),
            CancellationToken.None);

        Assert.NotEmpty(context.SimilarChunks);
        Assert.NotEmpty(context.RelevantEdges);
        Assert.NotEmpty(context.Evidence);
        Assert.Contains("Known facts", context.ContextText);
    }

    [Fact]
    public async Task Retrieval_does_not_cross_tenants_or_users()
    {
        var services = CreateServices();
        var ingestion = services.GetRequiredService<IMemoryIngestionService>();
        var retriever = services.GetRequiredService<IMemoryRetriever>();

        await ingestion.IngestMessageAsync(
            new IngestMessageCommand(
                "tenant-a",
                "juan",
                "conv-001",
                "Estoy construyendo un motor de memoria semantica para LLMs usando Neo4j."),
            CancellationToken.None);

        var otherTenantContext = await retriever.RetrieveContextAsync(
            new RetrieveMemoryQuery("tenant-b", "juan", "motor memoria Neo4j"),
            CancellationToken.None);

        var otherUserContext = await retriever.RetrieveContextAsync(
            new RetrieveMemoryQuery("tenant-a", "ana", "motor memoria Neo4j"),
            CancellationToken.None);

        Assert.Empty(otherTenantContext.SimilarChunks);
        Assert.Empty(otherTenantContext.RelevantEdges);
        Assert.Empty(otherUserContext.SimilarChunks);
        Assert.Empty(otherUserContext.RelevantEdges);
    }

    [Fact]
    public async Task ForgetMemory_excludes_chunk_from_retrieval()
    {
        var services = CreateServices();
        var ingestion = services.GetRequiredService<IMemoryIngestionService>();
        var retriever = services.GetRequiredService<IMemoryRetriever>();
        var forgetting = services.GetRequiredService<IMemoryForgettingService>();

        var ingested = await ingestion.IngestMessageAsync(
            new IngestMessageCommand(
                "default",
                "juan",
                "conv-001",
                "Estoy construyendo un motor de memoria semantica para LLMs usando pgvector."),
            CancellationToken.None);

        await forgetting.ForgetAsync(
            new ForgetMemoryCommand("default", "juan", ingested.MemoryChunkId),
            CancellationToken.None);

        var context = await retriever.RetrieveContextAsync(
            new RetrieveMemoryQuery("default", "juan", "pgvector memoria semantica"),
            CancellationToken.None);

        Assert.DoesNotContain(context.SimilarChunks, chunk => chunk.Chunk.Id == ingested.MemoryChunkId);
    }

    [Fact]
    public async Task RememberFact_uses_semantic_edge_as_primary_fact()
    {
        var services = CreateServices();
        var facts = services.GetRequiredService<IManualFactService>();
        var explanation = services.GetRequiredService<IMemoryExplanationService>();

        var edge = await facts.RememberFactAsync(
            new RememberFactCommand(
                "default",
                "juan",
                "juan",
                "hasSkill",
                ".NET",
                0.98,
                "Tengo experiencia con .NET."),
            CancellationToken.None);

        var evidence = await explanation.ExplainEdgeAsync(
            new ExplainEdgeQuery("default", "juan", edge.Id),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, edge.Id);
        Assert.Equal("hasSkill", edge.RelationType);
        Assert.NotEmpty(evidence);
    }

    [Fact]
    public async Task Exclusive_relation_supersedes_previous_active_edge()
    {
        var services = CreateServices();
        var facts = services.GetRequiredService<IManualFactService>();
        var graph = services.GetRequiredService<ISemanticGraphStore>();

        await facts.RememberFactAsync(
            new RememberFactCommand("default", "juan", "juan", "prefersLanguage", "C#", 0.9, "Prefiero C#."),
            CancellationToken.None);

        await facts.RememberFactAsync(
            new RememberFactCommand("default", "juan", "juan", "prefersLanguage", "Rust", 0.95, "Prefiero Rust."),
            CancellationToken.None);

        var userNodes = await graph.SearchNodesAsync("default", "juan", "juan", 5, CancellationToken.None);
        var edges = await graph.GetRelatedEdgesAsync(
            "default",
            "juan",
            userNodes.Select(node => node.Id).ToArray(),
            1,
            10,
            CancellationToken.None);

        var activePreferenceEdges = edges
            .Where(edge => edge.RelationType == "prefersLanguage")
            .ToArray();

        Assert.Single(activePreferenceEdges);
    }

    private static IServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure();
        return services.BuildServiceProvider();
    }
}
