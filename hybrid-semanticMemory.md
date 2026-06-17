# Hybrid Semantic Memory Engine para LLMs

## 1. Decisiones cerradas

Este documento define el MVP de producto del motor de memoria semántica.

Decisiones principales:

- El MVP sera **hibrido desde el inicio**.
- El sistema usara **PostgreSQL + pgvector + Neo4j**.
- `SemanticEdge` sera el **hecho semantico primario**.
- No se implementara `MemoryFact` en el MVP.
- Toda memoria factual se modelara como una relacion:

```text
SemanticNode -> SemanticEdge -> SemanticNode
```

- Toda relacion importante debe tener evidencia.
- Todo acceso de lectura y escritura debe estar aislado por `TenantId` y `UserId`.
- El olvido sera logico, no borrado fisico.
- La ingesta sera sincronica para el MVP. El worker asincrono queda para una version posterior.
- GraphQL no entra en el MVP. Primero REST.

---

## 2. Objetivo del MVP hibrido

Construir un motor de memoria semantica especializado para LLMs que pueda:

- Guardar mensajes como `MemoryChunk`.
- Generar embeddings y recuperarlos con pgvector.
- Extraer entidades.
- Extraer relaciones semanticas.
- Crear nodos y edges en Neo4j.
- Asociar evidencia a cada relacion.
- Recuperar contexto combinando similitud vectorial y expansion de grafo.
- Explicar de donde salio una memoria.
- Olvidar memorias de forma logica.
- Manejar contradicciones simples mediante estado y temporalidad.

Flujo conceptual:

```text
Usuario conversa
   ↓
API recibe mensaje
   ↓
PostgreSQL guarda MemoryChunk
   ↓
pgvector guarda embedding
   ↓
LLM extrae entidades y relaciones
   ↓
Neo4j guarda SemanticNodes y SemanticEdges
   ↓
PostgreSQL guarda Evidence
   ↓
Futuras preguntas recuperan chunks + grafo + evidencia
```

---

## 3. Alcance

Incluido en el MVP:

- ASP.NET Core Web API.
- Clean Architecture.
- PostgreSQL para chunks, evidencia y eventos.
- pgvector para embeddings de `MemoryChunk`.
- Neo4j para nodos y relaciones semanticas.
- Extractores LLM configurables.
- Extractores fake/mock para desarrollo local sin API key.
- Endpoints REST principales.
- Swagger.
- Docker Compose.
- Tests unitarios e integracion basicos.

Fuera del MVP:

- GraphQL.
- Worker asincrono real.
- Autenticacion avanzada.
- Panel visual de grafo.
- Decay automatico programado.
- Permisos complejos.
- SDK externo.
- Event sourcing completo.

---

## 4. Arquitectura recomendada

```text
Client / App / Agent
        ↓
SemanticMemory.Api
        ↓
SemanticMemory.Application
        ↓
SemanticMemory.Domain
        ↓
SemanticMemory.Infrastructure
        ↓
PostgreSQL + pgvector + Neo4j + LLM Provider
```

Solucion:

```text
SemanticMemory.sln

src/
  SemanticMemory.Api/
  SemanticMemory.Application/
  SemanticMemory.Domain/
  SemanticMemory.Infrastructure/
  SemanticMemory.Worker/

tests/
  SemanticMemory.UnitTests/
  SemanticMemory.IntegrationTests/
```

Dependencias:

```text
Api            -> Application, Infrastructure
Worker         -> Application, Infrastructure
Infrastructure -> Application, Domain
Application    -> Domain
Domain         -> ninguna capa
```

---

## 5. Regla de aislamiento

Toda operacion debe incluir:

```text
TenantId
UserId
```

La memoria del usuario A nunca debe aparecer en una recuperacion del usuario B, incluso si ambos pertenecen al mismo tenant.

Regla obligatoria:

```text
Toda consulta a PostgreSQL, pgvector o Neo4j debe filtrar por TenantId + UserId.
```

Esto aplica a:

- `MemoryChunk`
- `SemanticNode`
- `SemanticEdge`
- `Evidence`
- `MemoryEvent`
- busqueda vectorial
- busqueda de nodos
- expansion de grafo
- explain
- forget

---

## 6. Modelo de dominio

### MemoryStatus

```csharp
public enum MemoryStatus
{
    Active = 1,
    Forgotten = 2,
    Superseded = 3,
    Archived = 4
}
```

### MemoryType

```csharp
public enum MemoryType
{
    ShortTermMemory = 1,
    LongTermMemory = 2,
    UserPreference = 3,
    UserProfile = 4,
    ProjectMemory = 5,
    EphemeralFact = 6,
    TechnicalFact = 7,
    Decision = 8
}
```

### SourceType

```csharp
public enum SourceType
{
    Conversation = 1,
    ManualFact = 2,
    Document = 3,
    System = 4
}
```

### SemanticNode

Representa una entidad, concepto, persona, tecnologia, proyecto, documento, preferencia o tema.

```csharp
public sealed class SemanticNode
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = default!;
    public string UserId { get; set; } = default!;

    public string Type { get; set; } = default!;
    public string CanonicalName { get; set; } = default!;
    public string NormalizedKey { get; set; } = default!;
    public List<string> Aliases { get; set; } = [];
    public string? Description { get; set; }

    public MemoryStatus Status { get; set; } = MemoryStatus.Active;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

Regla de unicidad logica:

```text
TenantId + UserId + Type + NormalizedKey
```

Ejemplos:

```text
Person: Juan
Technology: .NET
Database: Neo4j
DatabaseExtension: pgvector
Concept: Semantic Memory Engine
Project: Semantic Memory Engine
ApiStyle: GraphQL
```

### SemanticEdge

`SemanticEdge` es el hecho semantico primario del sistema.

Representa:

```text
subject -> predicate -> object
```

Ejemplos:

```text
Juan -> hasSkill -> .NET
Juan -> prefersApiStyle -> GraphQL
Semantic Memory Engine -> uses -> Neo4j
Semantic Memory Engine -> uses -> pgvector
```

Modelo:

```csharp
public sealed class SemanticEdge
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = default!;
    public string UserId { get; set; } = default!;

    public Guid SourceNodeId { get; set; }
    public Guid TargetNodeId { get; set; }

    public string RelationType { get; set; } = default!;

    public double Confidence { get; set; }
    public double Weight { get; set; }

    public MemoryStatus Status { get; set; } = MemoryStatus.Active;

    public DateTimeOffset? ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

Regla de unicidad logica para edges activos:

```text
TenantId + UserId + SourceNodeId + RelationType + TargetNodeId + Status=Active
```

No se crea `MemoryFact` en el MVP.

Si el documento anterior menciona `MemoryFact`, debe entenderse como `SemanticEdge`.

### MemoryChunk

Guarda el texto original o sintetico que dio origen a la memoria.

```csharp
public sealed class MemoryChunk
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = default!;
    public string UserId { get; set; } = default!;
    public string? ConversationId { get; set; }

    public string RawText { get; set; } = default!;
    public string? Summary { get; set; }

    public float[]? Embedding { get; set; }

    public MemoryType MemoryType { get; set; } = MemoryType.LongTermMemory;
    public MemoryStatus Status { get; set; } = MemoryStatus.Active;
    public SourceType SourceType { get; set; } = SourceType.Conversation;

    public double Importance { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ForgottenAt { get; set; }
}
```

Notas:

- `forget(memoryId)` cambia `Status` a `Forgotten`.
- Las busquedas deben excluir chunks no activos.
- Para `rememberFact`, si no existe un chunk conversacional, se crea un `MemoryChunk` sintetico con `SourceType = ManualFact`.

### Evidence

Asocia un `SemanticEdge` con el chunk que lo justifica.

```csharp
public sealed class Evidence
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = default!;
    public string UserId { get; set; } = default!;

    public Guid EdgeId { get; set; }
    public Guid MemoryChunkId { get; set; }

    public string? Quote { get; set; }
    public SourceType SourceType { get; set; }
    public double Confidence { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
```

Reglas:

- Si `SourceType = Conversation`, `Quote` debe ser una cita exacta o un fragmento exacto del `RawText`.
- Si `SourceType = ManualFact`, `Quote` puede ser el texto declarado por el usuario o una frase sintetica.
- `Explain` devuelve evidencia filtrada por `TenantId + UserId + EdgeId`.

### MemoryEvent

Registra eventos internos del motor.

```csharp
public sealed class MemoryEvent
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = default!;
    public string UserId { get; set; } = default!;

    public string EventType { get; set; } = default!;
    public string EntityType { get; set; } = default!;
    public Guid EntityId { get; set; }
    public string PayloadJson { get; set; } = default!;

    public DateTimeOffset CreatedAt { get; set; }
}
```

Eventos iniciales:

```text
MemoryChunkCreated
EmbeddingCreated
EntitiesExtracted
RelationsExtracted
NodeUpserted
EdgeUpserted
EvidenceCreated
ConflictDetected
MemoryForgotten
EdgeSuperseded
```

---

## 7. DTOs principales

### IngestMessageCommand

```csharp
public sealed record IngestMessageCommand(
    string TenantId,
    string UserId,
    string? ConversationId,
    string Text,
    SourceType SourceType = SourceType.Conversation
);
```

### RetrieveMemoryQuery

```csharp
public sealed record RetrieveMemoryQuery(
    string TenantId,
    string UserId,
    string Prompt,
    int VectorLimit = 10,
    int NodeLimit = 10,
    int GraphDepth = 2,
    int MaxContextTokens = 1200
);
```

### RememberFactCommand

```csharp
public sealed record RememberFactCommand(
    string TenantId,
    string UserId,
    string Subject,
    string Predicate,
    string Object,
    double Confidence,
    string? EvidenceQuote,
    DateTimeOffset? ValidFrom = null,
    DateTimeOffset? ValidTo = null
);
```

### ForgetMemoryCommand

```csharp
public sealed record ForgetMemoryCommand(
    string TenantId,
    string UserId,
    Guid MemoryChunkId
);
```

### ExplainEdgeQuery

```csharp
public sealed record ExplainEdgeQuery(
    string TenantId,
    string UserId,
    Guid EdgeId
);
```

### ExtractedEntity

```csharp
public sealed record ExtractedEntity(
    string Name,
    string Type,
    double Confidence
);
```

### NormalizedEntity

```csharp
public sealed record NormalizedEntity(
    string CanonicalName,
    string NormalizedKey,
    string Type,
    IReadOnlyList<string> Aliases,
    double Confidence
);
```

### ExtractedRelation

```csharp
public sealed record ExtractedRelation(
    string Subject,
    string Predicate,
    string Object,
    double Confidence,
    string? EvidenceQuote
);
```

### ScoredMemoryChunk

```csharp
public sealed record ScoredMemoryChunk(
    MemoryChunk Chunk,
    double Similarity
);
```

### ScoredSemanticEdge

```csharp
public sealed record ScoredSemanticEdge(
    SemanticEdge Edge,
    double Score,
    double GraphRelevance,
    double Confidence,
    double Recency
);
```

### MemoryContext

```csharp
public sealed record MemoryContext(
    IReadOnlyList<ScoredMemoryChunk> SimilarChunks,
    IReadOnlyList<SemanticNode> RelevantNodes,
    IReadOnlyList<ScoredSemanticEdge> RelevantEdges,
    IReadOnlyList<Evidence> Evidence,
    string ContextText
);
```

---

## 8. Interfaces de Application

### Ingesta

```csharp
public interface IMemoryIngestionService
{
    Task<IngestionResult> IngestMessageAsync(
        IngestMessageCommand command,
        CancellationToken cancellationToken);
}
```

### Recuperacion

```csharp
public interface IMemoryRetriever
{
    Task<MemoryContext> RetrieveContextAsync(
        RetrieveMemoryQuery query,
        CancellationToken cancellationToken);
}
```

### Hechos manuales

```csharp
public interface IManualFactService
{
    Task<SemanticEdge> RememberFactAsync(
        RememberFactCommand command,
        CancellationToken cancellationToken);
}
```

### Olvido

```csharp
public interface IMemoryForgettingService
{
    Task ForgetAsync(
        ForgetMemoryCommand command,
        CancellationToken cancellationToken);
}
```

### Explicacion

```csharp
public interface IMemoryExplanationService
{
    Task<IReadOnlyList<Evidence>> ExplainEdgeAsync(
        ExplainEdgeQuery query,
        CancellationToken cancellationToken);
}
```

### Extraccion

```csharp
public interface IEntityExtractor
{
    Task<IReadOnlyList<ExtractedEntity>> ExtractEntitiesAsync(
        string text,
        CancellationToken cancellationToken);
}

public interface IRelationExtractor
{
    Task<IReadOnlyList<ExtractedRelation>> ExtractRelationsAsync(
        string text,
        CancellationToken cancellationToken);
}
```

### Normalizacion

```csharp
public interface IEntityNormalizer
{
    Task<NormalizedEntity> NormalizeAsync(
        string tenantId,
        string userId,
        ExtractedEntity entity,
        CancellationToken cancellationToken);
}
```

### Embeddings

```csharp
public interface IEmbeddingProvider
{
    Task<float[]> CreateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken);
}
```

### Vector store

```csharp
public interface IVectorMemoryStore
{
    Task SaveEmbeddingAsync(
        MemoryChunk chunk,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ScoredMemoryChunk>> SearchSimilarAsync(
        string tenantId,
        string userId,
        string prompt,
        int limit,
        CancellationToken cancellationToken);
}
```

### Graph store

```csharp
public interface ISemanticGraphStore
{
    Task<SemanticNode> UpsertNodeAsync(
        SemanticNode node,
        CancellationToken cancellationToken);

    Task<SemanticEdge> UpsertEdgeAsync(
        SemanticEdge edge,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SemanticNode>> SearchNodesAsync(
        string tenantId,
        string userId,
        string text,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SemanticEdge>> GetRelatedEdgesAsync(
        string tenantId,
        string userId,
        IReadOnlyList<Guid> nodeIds,
        int depth,
        int limit,
        CancellationToken cancellationToken);

    Task MarkEdgeStatusAsync(
        string tenantId,
        string userId,
        Guid edgeId,
        MemoryStatus status,
        DateTimeOffset? validTo,
        CancellationToken cancellationToken);
}
```

### Evidence store

```csharp
public interface IEvidenceStore
{
    Task SaveEvidenceAsync(
        Evidence evidence,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Evidence>> GetEvidenceForEdgeAsync(
        string tenantId,
        string userId,
        Guid edgeId,
        CancellationToken cancellationToken);
}
```

### Context builder

```csharp
public interface IPromptContextBuilder
{
    string BuildContext(MemoryContext memoryContext, int maxTokens);
}
```

---

## 9. API REST

### POST /api/memory/ingest

Request:

```json
{
  "tenantId": "default",
  "userId": "juan",
  "conversationId": "conv-001",
  "text": "Estoy construyendo un motor de memoria semantica para LLMs usando Neo4j, pgvector y GraphQL."
}
```

Response:

```json
{
  "memoryChunkId": "guid",
  "entities": [
    { "name": "motor de memoria semantica", "type": "Concept", "confidence": 0.94 },
    { "name": "LLMs", "type": "Technology", "confidence": 0.92 },
    { "name": "Neo4j", "type": "Database", "confidence": 0.99 },
    { "name": "pgvector", "type": "DatabaseExtension", "confidence": 0.99 },
    { "name": "GraphQL", "type": "ApiStyle", "confidence": 0.90 }
  ],
  "relations": [
    { "subject": "juan", "predicate": "isBuilding", "object": "Semantic Memory Engine", "confidence": 0.95 },
    { "subject": "Semantic Memory Engine", "predicate": "uses", "object": "Neo4j", "confidence": 0.90 },
    { "subject": "Semantic Memory Engine", "predicate": "uses", "object": "pgvector", "confidence": 0.90 }
  ],
  "upsertedNodeCount": 5,
  "upsertedEdgeCount": 3,
  "evidenceCount": 3
}
```

### POST /api/memory/retrieve

Request:

```json
{
  "tenantId": "default",
  "userId": "juan",
  "prompt": "Que arquitectura estaba considerando para mi motor?"
}
```

Response:

```json
{
  "context": "El usuario esta construyendo un motor de memoria semantica para LLMs. La arquitectura considerada incluye Neo4j para relaciones semanticas y pgvector para embeddings.",
  "facts": [
    {
      "subject": "Semantic Memory Engine",
      "predicate": "uses",
      "object": "Neo4j",
      "confidence": 0.9,
      "score": 0.86
    }
  ],
  "evidence": [
    {
      "edgeId": "guid",
      "quote": "Estoy construyendo un motor de memoria semantica para LLMs usando Neo4j, pgvector y GraphQL.",
      "confidence": 0.95
    }
  ]
}
```

### POST /api/memory/facts

Request:

```json
{
  "tenantId": "default",
  "userId": "juan",
  "subject": "juan",
  "predicate": "hasSkill",
  "object": ".NET",
  "confidence": 0.98,
  "evidenceQuote": "Tengo experiencia con .NET."
}
```

Comportamiento:

```text
1. Crear MemoryChunk sintetico con SourceType = ManualFact.
2. Normalizar subject y object.
3. Crear/upsert SemanticNode para subject.
4. Crear/upsert SemanticNode para object.
5. Crear/upsert SemanticEdge.
6. Crear Evidence asociada al edge.
```

### DELETE /api/memory/{memoryChunkId}

Debe marcar el `MemoryChunk` como `Forgotten`.

Comportamiento MVP:

- El chunk olvidado no aparece en retrieval vectorial.
- Las evidencias basadas exclusivamente en ese chunk no deben aparecer en `retrieve`.
- Si un edge queda sin evidencia activa, puede marcarse como `Archived` o excluirse del contexto.

### GET /api/memory/explain/{edgeId}

Request params:

```text
tenantId
userId
```

Response:

```json
{
  "edgeId": "guid",
  "evidence": [
    {
      "memoryChunkId": "guid",
      "quote": "Tengo experiencia con .NET.",
      "sourceType": "ManualFact",
      "confidence": 0.98,
      "createdAt": "2026-06-17T00:00:00Z"
    }
  ]
}
```

---

## 10. Flujo de ingesta

Pasos:

```text
1. Validar TenantId, UserId y Text.
2. Crear MemoryChunk activo.
3. Crear embedding del texto.
4. Guardar embedding en pgvector.
5. Extraer entidades con LLM o extractor fake.
6. Extraer relaciones con LLM o extractor fake.
7. Normalizar entidades.
8. Crear/upsert SemanticNodes en Neo4j.
9. Resolver cada relacion a SourceNodeId y TargetNodeId.
10. Detectar contradicciones simples.
11. Crear/upsert SemanticEdges en Neo4j.
12. Guardar Evidence en PostgreSQL.
13. Guardar MemoryEvents.
14. Devolver resultado de ingesta.
```

Reglas:

- Si una relacion menciona una entidad no extraida, se debe crear una entidad candidata.
- Si una relacion no puede resolverse a dos nodos, se descarta y se registra evento.
- Si `EvidenceQuote` no es fragmento exacto del texto en ingesta conversacional, se acepta con menor confianza o se descarta segun configuracion.

---

## 11. Flujo de recuperacion hibrida

Pasos:

```text
1. Validar TenantId, UserId y Prompt.
2. Crear embedding del prompt.
3. Buscar MemoryChunks similares en pgvector filtrando TenantId + UserId + Status=Active.
4. Extraer entidades del prompt.
5. Normalizar entidades del prompt.
6. Buscar SemanticNodes relevantes en Neo4j filtrando TenantId + UserId + Status=Active.
7. Expandir relaciones a profundidad 1 o 2.
8. Filtrar SemanticEdges por TenantId + UserId + Status=Active.
9. Recuperar Evidence activa asociada.
10. Calcular score hibrido.
11. Ordenar hechos y chunks.
12. Construir contexto compacto.
13. Devolver contexto, hechos y evidencia.
```

El contexto para el LLM debe ser breve y explicable.

Formato recomendado:

```text
Relevant memory:
- ...

Known facts:
- subject predicate object. Evidence: "..."

Notes:
- Some older facts were superseded and are not included.
```

---

## 12. Scoring hibrido

Formula inicial:

```text
score =
  semanticSimilarity * 0.45
+ graphRelevance      * 0.25
+ confidence          * 0.20
+ recency             * 0.10
```

Normalizacion:

```text
semanticSimilarity: 0..1 desde pgvector.
graphRelevance: 1.0 nodo exacto, 0.8 depth 1, 0.55 depth 2, 0.0 sin grafo.
confidence: 0..1 desde edge/evidence.
recency: 1.0 <= 7 dias, 0.75 <= 30 dias, 0.5 <= 90 dias, 0.25 > 90 dias.
```

Reglas:

- Edges `Forgotten`, `Superseded` o `Archived` no entran al contexto normal.
- Chunks `Forgotten`, `Superseded` o `Archived` no entran al contexto normal.
- `Superseded` solo puede aparecer en `explain` o vistas de auditoria.

---

## 13. Contradicciones

El MVP manejara contradicciones de forma conservadora.

No todos los predicados son exclusivos.

Ejemplos no exclusivos:

```text
uses
hasSkill
interestedIn
mentions
relatedTo
```

Ejemplos exclusivos o casi exclusivos:

```text
prefersLanguage
prefersApiStyle
currentProject
primaryGoal
currentRole
```

Regla MVP:

```text
Si llega un nuevo edge activo con el mismo source + relationType exclusivo
y target distinto, marcar el edge anterior como Superseded y cerrar ValidTo.
```

Ejemplo:

```text
2026-05: Juan -> prefersLanguage -> C#
2026-06: Juan -> prefersLanguage -> Rust
```

Resultado:

```text
Juan -> prefersLanguage -> C#    Status=Superseded ValidTo=2026-06
Juan -> prefersLanguage -> Rust  Status=Active     ValidFrom=2026-06
```

La evidencia del edge anterior se conserva.

---

## 14. Olvido

El olvido es logico.

Estados:

```text
Active
Forgotten
Superseded
Archived
```

Reglas:

- `DELETE /api/memory/{memoryChunkId}` marca el chunk como `Forgotten`.
- Retrieval no devuelve chunks olvidados.
- Evidence asociada a chunks olvidados se excluye del contexto.
- Explain puede mostrar evidencia olvidada solo si se agrega una opcion explicita de auditoria en el futuro.
- El borrado fisico queda fuera del MVP.

---

## 15. Prompts internos

### Extraccion de entidades

```text
You are an information extraction system.

Extract relevant entities from the input text.
Return only valid JSON.
Do not include markdown.
Do not invent entities that are not grounded in the input.
If there are no relevant entities, return { "entities": [] }.

Allowed entity types:
- Person
- Organization
- Technology
- Tool
- Platform
- Framework
- Library
- Database
- DatabaseExtension
- ApiStyle
- Concept
- Project
- Document
- Skill
- Preference
- Goal
- Event
- Other

Input:
{{text}}

JSON schema:
{
  "entities": [
    {
      "name": "string",
      "type": "string",
      "confidence": 0.0
    }
  ]
}
```

### Extraccion de relaciones

```text
You are a semantic relation extraction system.

Extract relationships from the input text.
Return only valid JSON.
Do not include markdown.
Do not invent relationships that are not grounded in the input.
If there are no relevant relationships, return { "relations": [] }.

Each evidenceQuote must be an exact substring from the input text.
Use concise predicates in camelCase.

Input:
{{text}}

JSON schema:
{
  "relations": [
    {
      "subject": "string",
      "predicate": "string",
      "object": "string",
      "confidence": 0.0,
      "evidenceQuote": "string"
    }
  ]
}
```

---

## 16. Persistencia

### PostgreSQL

Tablas:

```text
memory_chunks
evidence
memory_events
```

Indices minimos:

```text
memory_chunks(tenant_id, user_id, status, created_at)
memory_chunks(tenant_id, user_id, source_type)
evidence(tenant_id, user_id, edge_id)
evidence(tenant_id, user_id, memory_chunk_id)
memory_events(tenant_id, user_id, created_at)
```

### pgvector

Usar extension:

```sql
CREATE EXTENSION IF NOT EXISTS vector;
```

Columna recomendada:

```text
memory_chunks.embedding vector(1536)
```

El valor `1536` corresponde a `text-embedding-3-small`. Si se cambia el modelo, la dimension debe cambiar en migraciones.

Indice recomendado:

```sql
CREATE INDEX memory_chunks_embedding_idx
ON memory_chunks
USING ivfflat (embedding vector_cosine_ops);
```

Toda busqueda vectorial debe filtrar:

```text
tenant_id = ?
user_id = ?
status = Active
```

### Neo4j

Labels:

```text
(:SemanticNode)
```

Relationship:

```text
(:SemanticNode)-[:SEMANTIC_EDGE]->(:SemanticNode)
```

Propiedades de nodos:

```text
id
tenantId
userId
type
canonicalName
normalizedKey
aliases
status
createdAt
updatedAt
```

Propiedades de relaciones:

```text
id
tenantId
userId
relationType
confidence
weight
status
validFrom
validTo
createdAt
updatedAt
```

Constraints recomendados:

```cypher
CREATE CONSTRAINT semantic_node_identity IF NOT EXISTS
FOR (n:SemanticNode)
REQUIRE (n.tenantId, n.userId, n.type, n.normalizedKey) IS UNIQUE;

CREATE INDEX semantic_node_search IF NOT EXISTS
FOR (n:SemanticNode)
ON (n.tenantId, n.userId, n.status, n.canonicalName);

CREATE INDEX semantic_edge_identity IF NOT EXISTS
FOR ()-[r:SEMANTIC_EDGE]-()
ON (r.tenantId, r.userId, r.status, r.relationType);
```

---

## 17. Docker Compose

```yaml
services:
  postgres:
    image: pgvector/pgvector:pg16
    container_name: semantic_memory_postgres
    environment:
      POSTGRES_DB: semantic_memory
      POSTGRES_USER: semantic
      POSTGRES_PASSWORD: semantic
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data

  neo4j:
    image: neo4j:5
    container_name: semantic_memory_neo4j
    environment:
      NEO4J_AUTH: neo4j/semanticmemory
    ports:
      - "7474:7474"
      - "7687:7687"
    volumes:
      - neo4j_data:/data

volumes:
  postgres_data:
  neo4j_data:
```

---

## 18. Variables de entorno

```env
ASPNETCORE_ENVIRONMENT=Development

POSTGRES_CONNECTION_STRING=Host=localhost;Port=5432;Database=semantic_memory;Username=semantic;Password=semantic

NEO4J_URI=bolt://localhost:7687
NEO4J_USER=neo4j
NEO4J_PASSWORD=semanticmemory

LLM_PROVIDER=OpenAI
OPENAI_API_KEY=your-api-key-here
EMBEDDING_MODEL=text-embedding-3-small
EXTRACTION_MODEL=gpt-4.1-mini

USE_FAKE_EXTRACTORS=true
```

---

## 19. Criterios de aceptacion

El MVP estara listo cuando se pueda ejecutar este flujo:

```text
1. Levantar PostgreSQL y Neo4j con Docker.
2. Ejecutar migraciones de PostgreSQL.
3. Crear constraints de Neo4j.
4. Ejecutar la API.
5. Enviar texto a POST /api/memory/ingest.
6. Ver MemoryChunk activo en PostgreSQL.
7. Ver embedding en pgvector.
8. Ver SemanticNodes en Neo4j.
9. Ver SemanticEdges en Neo4j.
10. Ver Evidence en PostgreSQL.
11. Enviar pregunta a POST /api/memory/retrieve.
12. Recibir contexto compacto con chunks, hechos y evidencia.
13. Consultar GET /api/memory/explain/{edgeId}.
14. Recibir evidencia del edge.
15. Ejecutar DELETE /api/memory/{memoryChunkId}.
16. Confirmar que ese chunk ya no aparece en retrieve.
```

---

## 20. Tests minimos

Unit tests:

- `IngestMessage` crea `MemoryChunk`.
- `IngestMessage` llama a embedding provider.
- `IngestMessage` extrae entidades.
- `IngestMessage` extrae relaciones.
- `IngestMessage` crea nodos.
- `IngestMessage` crea edges.
- `IngestMessage` crea evidencia.
- `RememberFact` crea chunk sintetico.
- `RememberFact` usa `SemanticEdge` como hecho primario.
- `ForgetMemory` marca chunk como `Forgotten`.
- `PromptContextBuilder` respeta `maxTokens`.

Integration tests:

- Retrieval no cruza tenants.
- Retrieval no cruza users.
- Retrieval excluye chunks olvidados.
- Explain devuelve evidencia del edge correcto.
- Contradiccion en predicado exclusivo marca edge anterior como `Superseded`.
- Busqueda hibrida combina chunks similares y edges relacionados.

---

## 21. Roadmap posterior

### Version 0.3

```text
- Worker asincrono.
- Reintentos de extraccion.
- Mejor normalizacion semantica.
- Ranking mas sofisticado.
- Explain por memoryChunkId ademas de edgeId.
```

### Version 0.4

```text
- Decay automatico.
- Conflict resolver avanzado.
- Vista de auditoria.
- Panel visual del grafo.
- GraphQL opcional.
```

### Version 1.0

```text
- Autenticacion y permisos.
- Multiusuario robusto.
- Observabilidad.
- SDK para agentes.
- Politicas de privacidad y borrado fisico.
```

---

## 22. Principio guia

El motor no solo guarda texto.

Guarda conocimiento conectado:

```text
chunk + embedding + node + edge + evidence + confidence + time
```

La unidad factual del MVP es:

```text
SemanticEdge
```

La fuente explicable de cada hecho es:

```text
Evidence -> MemoryChunk
```

La recuperacion valiosa es:

```text
Vector Search + Knowledge Graph + Evidence
```
