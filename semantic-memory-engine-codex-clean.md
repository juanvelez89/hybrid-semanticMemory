# Semantic Memory Engine para LLMs

## 1. Objetivo del proyecto

> Nota: esta especificación está enfocada únicamente en un motor de memoria semántica para LLMs. Los ejemplos del documento deben mantenerse genéricos y no depender de dominios externos como trading, finanzas, e-commerce u otros casos de negocio particulares.


Construir un **motor de memoria semántica especializado para LLMs**.

Este sistema no busca reemplazar PostgreSQL, Neo4j o una base vectorial. Su propósito es actuar como una **capa inteligente de memoria** que permita a un LLM:

- Recordar información importante.
- Relacionar conceptos, entidades y hechos.
- Recuperar contexto relevante en futuras conversaciones.
- Justificar de dónde salió cada memoria.
- Combinar búsqueda vectorial con relaciones semánticas explícitas.
- Detectar contradicciones y manejar obsolescencia.
- Construir contexto compacto para alimentar al LLM.

La idea principal:

```text
Usuario conversa
   ↓
El motor ingesta el mensaje
   ↓
Extrae entidades y relaciones
   ↓
Guarda hechos, evidencias y embeddings
   ↓
En futuras preguntas recupera memoria relevante
   ↓
El LLM responde con contexto enriquecido
```

---

## 2. Problema que resuelve

Un LLM normalmente puede razonar muy bien dentro del contexto actual, pero tiene limitaciones para:

- Mantener memoria persistente.
- Distinguir hechos importantes de información temporal.
- Explicar por qué recuerda algo.
- Relacionar información dispersa en varias conversaciones.
- Recuperar recuerdos por significado y no solo por texto literal.
- Controlar confianza, evidencia y temporalidad.

Un RAG tradicional recupera fragmentos de texto similares.

Este motor debe recuperar **conocimiento conectado**.

```text
RAG tradicional:
Pregunta → chunks similares → LLM

Semantic Memory Engine:
Pregunta
 → chunks similares
 → nodos relevantes
 → relaciones del grafo
 → evidencias
 → ranking
 → contexto compacto
 → LLM
```

---

## 3. Visión de arquitectura

Arquitectura recomendada para el MVP:

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

Componentes principales:

```text
Semantic Memory Engine
├── Ingestion Engine
├── Entity Extraction
├── Relation Extraction
├── Normalization Engine
├── Graph Store
├── Vector Store
├── Evidence Store
├── Memory Scoring Engine
├── Retrieval Engine
├── Conflict Resolver
├── Forgetting / Decay Engine
├── Prompt Context Builder
└── API Layer
```

---

## 4. Stack técnico recomendado

### Backend

- .NET 8 o superior.
- ASP.NET Core Web API.
- Clean Architecture.
- Minimal APIs o Controllers.
- MediatR opcional.
- FluentValidation opcional.
- GraphQL opcional con HotChocolate.

### Datos

- PostgreSQL.
- pgvector para embeddings.
- Neo4j para relaciones semánticas.
- EF Core para persistencia administrativa y documental.
- Neo4j.Driver para operaciones sobre grafo.

### Procesamiento

- Worker Service .NET para ingesta asíncrona.
- RabbitMQ, Redis Streams o BackgroundService para cola inicial.
- OpenAI API, Azure OpenAI o proveedor compatible para:
  - extracción de entidades,
  - extracción de relaciones,
  - generación de embeddings,
  - normalización semántica.

### Frontend opcional

- Angular.
- Cytoscape.js o D3.js para visualización del grafo.

---

## 5. Estructura de solución recomendada

Crear una solución .NET con esta estructura:

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

Responsabilidades:

```text
SemanticMemory.Api
- Endpoints REST/GraphQL.
- Auth JWT futura.
- Entrada y salida HTTP.
- Swagger/OpenAPI.

SemanticMemory.Application
- Casos de uso.
- Orquestación.
- Handlers.
- DTOs.
- Validaciones.
- Interfaces de puertos.

SemanticMemory.Domain
- Entidades del dominio.
- Value Objects.
- Reglas de negocio.
- Tipos de memoria.
- Tipos de relación.
- Scoring.

SemanticMemory.Infrastructure
- PostgreSQL.
- pgvector.
- Neo4j.
- LLM provider.
- Embedding provider.
- Repositorios.
- Implementaciones de interfaces.

SemanticMemory.Worker
- Procesamiento asíncrono.
- Extracción de entidades.
- Extracción de relaciones.
- Reindexación.
- Decay de memorias.
```

---

## 6. Modelo de dominio inicial

### SemanticNode

Representa una entidad, concepto, persona, tecnología, proyecto, documento o tema.

```csharp
public sealed class SemanticNode
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = default!;
    public string Type { get; set; } = default!;
    public string CanonicalName { get; set; } = default!;
    public List<string> Aliases { get; set; } = [];
    public string? Description { get; set; }
    public string? EmbeddingId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

Ejemplos:

```text
Technology: .NET
Technology: Angular
Platform: OpenAI API
Concept: Semantic Memory Engine
Person: Juan
Project: Semantic Memory Engine
```

---

### SemanticEdge

Representa una relación entre dos nodos.

```csharp
public sealed class SemanticEdge
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = default!;
    public Guid SourceNodeId { get; set; }
    public Guid TargetNodeId { get; set; }
    public string RelationType { get; set; } = default!;
    public double Confidence { get; set; }
    public double Weight { get; set; }
    public string Status { get; set; } = "Active";
    public DateTimeOffset? ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

Ejemplos:

```text
Juan → hasSkill → .NET
Juan → interestedIn → Semantic Networks
Semantic Memory Engine → uses → Vector Store
Semantic Memory Engine → uses → Graph Store
GraphQL → exposes → API
```

---

### MemoryChunk

Guarda el texto original o resumido que dio origen a una memoria.

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
    public double Importance { get; set; }
    public string SourceType { get; set; } = "Conversation";
    public DateTimeOffset CreatedAt { get; set; }
}
```

---

### Evidence

Asocia una relación o hecho con su fuente.

```csharp
public sealed class Evidence
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = default!;
    public Guid? EdgeId { get; set; }
    public Guid MemoryChunkId { get; set; }
    public string? Quote { get; set; }
    public string SourceType { get; set; } = default!;
    public double Confidence { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
```

Objetivo:

```text
No solo guardar:
Juan → hasSkill → .NET

También guardar:
Fuente: conversación X
Quote: "tengo 13 años de experiencia con C#/.NET"
Confianza: 0.97
```

---

### MemoryEvent

Registra eventos internos del motor.

```csharp
public sealed class MemoryEvent
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = default!;
    public string EventType { get; set; } = default!;
    public string EntityType { get; set; } = default!;
    public Guid EntityId { get; set; }
    public string PayloadJson { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }
}
```

Ejemplos de eventos:

```text
MemoryChunkCreated
EntitiesExtracted
RelationsExtracted
NodeUpserted
EdgeUpserted
ConflictDetected
MemoryForgotten
MemoryDecayed
```

---

## 7. Interfaces principales

Crear estas interfaces en `SemanticMemory.Application`.

### Ingesta

```csharp
public interface IMemoryIngestionService
{
    Task<IngestionResult> IngestMessageAsync(IngestMessageCommand command, CancellationToken cancellationToken);
}
```

### Extracción de entidades

```csharp
public interface IEntityExtractor
{
    Task<IReadOnlyList<ExtractedEntity>> ExtractEntitiesAsync(string text, CancellationToken cancellationToken);
}
```

### Extracción de relaciones

```csharp
public interface IRelationExtractor
{
    Task<IReadOnlyList<ExtractedRelation>> ExtractRelationsAsync(string text, CancellationToken cancellationToken);
}
```

### Normalización

```csharp
public interface IEntityNormalizer
{
    Task<NormalizedEntity> NormalizeAsync(ExtractedEntity entity, CancellationToken cancellationToken);
}
```

### Grafo semántico

```csharp
public interface ISemanticGraphStore
{
    Task<SemanticNode> UpsertNodeAsync(SemanticNode node, CancellationToken cancellationToken);
    Task<SemanticEdge> UpsertEdgeAsync(SemanticEdge edge, CancellationToken cancellationToken);
    Task<IReadOnlyList<SemanticEdge>> GetRelatedEdgesAsync(Guid nodeId, int depth, CancellationToken cancellationToken);
    Task<IReadOnlyList<SemanticNode>> SearchNodesAsync(string text, CancellationToken cancellationToken);
}
```

### Vector store

```csharp
public interface IVectorMemoryStore
{
    Task SaveEmbeddingAsync(MemoryChunk chunk, CancellationToken cancellationToken);
    Task<IReadOnlyList<MemoryChunk>> SearchSimilarAsync(string text, int limit, CancellationToken cancellationToken);
}
```

### Recuperación

```csharp
public interface IMemoryRetriever
{
    Task<MemoryContext> RetrieveContextAsync(string userId, string prompt, CancellationToken cancellationToken);
}
```

### Construcción de contexto

```csharp
public interface IPromptContextBuilder
{
    string BuildContext(MemoryContext memoryContext, int maxTokens);
}
```

### Evidencia

```csharp
public interface IEvidenceStore
{
    Task SaveEvidenceAsync(Evidence evidence, CancellationToken cancellationToken);
    Task<IReadOnlyList<Evidence>> GetEvidenceForEdgeAsync(Guid edgeId, CancellationToken cancellationToken);
}
```

---

## 8. DTOs principales

```csharp
public sealed record IngestMessageCommand(
    string TenantId,
    string UserId,
    string? ConversationId,
    string Text,
    string SourceType = "Conversation"
);
```

```csharp
public sealed record IngestionResult(
    Guid MemoryChunkId,
    IReadOnlyList<ExtractedEntity> Entities,
    IReadOnlyList<ExtractedRelation> Relations,
    IReadOnlyList<SemanticNode> UpsertedNodes,
    IReadOnlyList<SemanticEdge> UpsertedEdges
);
```

```csharp
public sealed record ExtractedEntity(
    string Name,
    string Type,
    double Confidence
);
```

```csharp
public sealed record ExtractedRelation(
    string Subject,
    string Predicate,
    string Object,
    double Confidence,
    string? EvidenceQuote
);
```

```csharp
public sealed record MemoryContext(
    IReadOnlyList<MemoryChunk> SimilarChunks,
    IReadOnlyList<SemanticNode> RelevantNodes,
    IReadOnlyList<SemanticEdge> RelevantEdges,
    IReadOnlyList<Evidence> Evidence
);
```

---

## 9. Operaciones mínimas del MVP

El MVP debe exponer estas operaciones:

```text
1. ingest(text, userId)
2. retrieve(prompt, userId)
3. rememberFact(subject, predicate, object)
4. forget(memoryId)
5. explain(memoryId or edgeId)
```

### Endpoint: ingest

```http
POST /api/memory/ingest
```

Body:

```json
{
  "tenantId": "default",
  "userId": "user_123",
  "conversationId": "conv_456",
  "text": "Estoy construyendo una memoria semántica para LLMs usando Neo4j y pgvector."
}
```

Respuesta esperada:

```json
{
  "memoryChunkId": "guid",
  "entities": [
    {
      "name": "memoria semántica",
      "type": "Concept",
      "confidence": 0.95
    },
    {
      "name": "LLMs",
      "type": "Technology",
      "confidence": 0.92
    },
    {
      "name": "Neo4j",
      "type": "Database",
      "confidence": 0.99
    },
    {
      "name": "pgvector",
      "type": "DatabaseExtension",
      "confidence": 0.99
    }
  ],
  "relations": [
    {
      "subject": "user_123",
      "predicate": "isBuilding",
      "object": "Semantic Memory Engine",
      "confidence": 0.95
    },
    {
      "subject": "Semantic Memory Engine",
      "predicate": "uses",
      "object": "Neo4j",
      "confidence": 0.88
    },
    {
      "subject": "Semantic Memory Engine",
      "predicate": "uses",
      "object": "pgvector",
      "confidence": 0.88
    }
  ]
}
```

---

### Endpoint: retrieve

```http
POST /api/memory/retrieve
```

Body:

```json
{
  "tenantId": "default",
  "userId": "user_123",
  "prompt": "¿Qué arquitectura debería usar para mi motor de memoria?"
}
```

Respuesta esperada:

```json
{
  "context": "El usuario está construyendo un motor de memoria semántica para LLMs. Ha considerado Neo4j para relaciones semánticas, PostgreSQL para datos administrativos, pgvector para embeddings y GraphQL como API.",
  "facts": [
    "user_123 isBuilding Semantic Memory Engine",
    "Semantic Memory Engine uses Neo4j",
    "Semantic Memory Engine uses pgvector"
  ],
  "evidence": [
    {
      "quote": "Estoy construyendo una memoria semántica para LLMs usando Neo4j y pgvector.",
      "confidence": 0.95
    }
  ]
}
```

---

### Endpoint: rememberFact

```http
POST /api/memory/facts
```

Body:

```json
{
  "tenantId": "default",
  "userId": "user_123",
  "subject": "user_123",
  "predicate": "hasSkill",
  "object": ".NET",
  "confidence": 0.98,
  "evidenceQuote": "Tengo experiencia con .NET."
}
```

---

### Endpoint: forget

```http
DELETE /api/memory/{memoryId}
```

Debe marcar la memoria como olvidada, no necesariamente borrarla físicamente al inicio.

---

### Endpoint: explain

```http
GET /api/memory/explain/{edgeId}
```

Debe devolver la evidencia asociada a una relación.

---

## 10. Flujo de ingesta

Cuando llega un mensaje:

```text
Usuario: "Estoy construyendo un motor de memoria semántica para LLMs."
```

El sistema debe:

```text
1. Guardar texto bruto como MemoryChunk.
2. Crear embedding del texto.
3. Guardar embedding en pgvector.
4. Extraer entidades:
   - motor de memoria semántica
   - LLMs
5. Extraer relaciones:
   - usuario builds motor de memoria semántica
   - motor de memoria semántica supports LLMs
6. Normalizar entidades.
7. Crear o actualizar nodos.
8. Crear o actualizar edges.
9. Asociar evidencia.
10. Calcular importancia.
11. Publicar evento MemoryUpdated.
```

---

## 11. Flujo de recuperación

Cuando llega una pregunta:

```text
"¿Qué componentes tendría ese motor?"
```

El sistema debe:

```text
1. Crear embedding de la pregunta.
2. Buscar MemoryChunks similares en pgvector.
3. Extraer entidades de la pregunta.
4. Buscar nodos relevantes en Neo4j.
5. Expandir relaciones a profundidad 1 o 2.
6. Filtrar por usuario, tenant, confianza y estado.
7. Recuperar evidencias.
8. Calcular score de memoria.
9. Construir contexto compacto.
10. Devolver contexto para el LLM.
```

---

## 12. Scoring inicial de memoria

Implementar un scoring simple:

```text
score =
  semanticSimilarity * 0.45
+ graphRelevance      * 0.25
+ confidence          * 0.20
+ recency             * 0.10
```

Donde:

```text
semanticSimilarity: similitud vectorial contra el prompt.
graphRelevance: cercanía en el grafo.
confidence: confianza del hecho o evidencia.
recency: factor de actualidad.
```

No optimizar demasiado en el MVP.

---

## 13. Tipos de memoria

Clasificar memorias para permitir olvido y priorización.

```text
ShortTermMemory
LongTermMemory
UserPreference
UserProfile
ProjectMemory
EphemeralFact
TechnicalFact
Decision
```

Ejemplos:

```text
"Hoy tengo entrevista a las 3 PM" → EphemeralFact
"Soy desarrollador .NET" → UserProfile
"Prefiero GraphQL como API" → UserPreference
"Estoy construyendo Semantic Memory Engine" → ProjectMemory
```

---

## 14. Manejo de contradicciones

El motor debe permitir memorias contradictorias con temporalidad.

Ejemplo:

```text
2026-05: user prefers C#
2026-06: user prefers Rust
```

Representación:

```text
MemoryFact A:
subject: user
predicate: prefersLanguage
object: C#
validFrom: 2026-05
validTo: 2026-06
status: Superseded

MemoryFact B:
subject: user
predicate: prefersLanguage
object: Rust
validFrom: 2026-06
status: Active
```

MVP:

- No borrar la memoria anterior.
- Marcarla como `Superseded`.
- Conservar evidencia.
- Recuperar preferentemente la memoria activa.

---

## 15. Olvido y decay

El sistema debe soportar:

```text
olvido explícito solicitado por usuario
olvido por baja confianza
olvido por obsolescencia
olvido por contradicción
reducción de prioridad por tiempo
```

MVP:

- Implementar `Status` en memorias y edges:
  - Active
  - Forgotten
  - Superseded
  - Archived
- Implementar `Importance`.
- Implementar job futuro para decay.

---

## 16. GraphQL opcional

GraphQL puede exponer el grafo semántico, pero no debe ser el motor semántico en sí.

GraphQL sirve como API:

```graphql
query {
  retrieveMemory(userId: "user_123", prompt: "arquitectura del motor") {
    facts {
      subject
      predicate
      object
      confidence
    }
    evidence {
      quote
      sourceType
    }
  }
}
```

Mutation:

```graphql
mutation {
  ingestMessage(input: {
    userId: "user_123",
    conversationId: "conv_456",
    text: "Quiero construir una segunda memoria para LLMs con redes semánticas"
  }) {
    extractedFacts {
      subject
      predicate
      object
      confidence
    }
  }
}
```

Recomendación:

- Empezar con REST para MVP.
- Agregar GraphQL en versión 0.3.

---

## 17. Persistencia recomendada

### PostgreSQL

Usar para:

```text
MemoryChunks
Evidence
MemoryEvents
Users futuro
Tenants futuro
Auditoría futura
Configuración
```

### pgvector

Usar para:

```text
Embeddings de MemoryChunks
Búsqueda por similitud semántica
```

### Neo4j

Usar para:

```text
SemanticNodes
SemanticEdges
Expansión de grafo
Relaciones entre conceptos
Path traversal
```

---

## 18. Docker Compose para desarrollo

Crear `docker-compose.yml` con:

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

## 19. Variables de entorno

Crear `.env.example`:

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
```

---

## 20. Prompts internos para extracción

### Prompt de extracción de entidades

```text
You are an information extraction system.

Extract the relevant entities from the input text.
Return only valid JSON.

Each entity must have:
- name
- type
- confidence

Allowed entity types:
- Person
- Organization
- Technology
- Tool
- Platform
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

### Prompt de extracción de relaciones

```text
You are a semantic relation extraction system.

Extract relationships from the input text.
Return only valid JSON.

Each relation must have:
- subject
- predicate
- object
- confidence
- evidenceQuote

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

## 21. Criterios de aceptación del MVP

El agente debe implementar un MVP que cumpla:

### Ingesta

- Dado un texto, el sistema crea un `MemoryChunk`.
- El sistema genera un embedding y lo guarda.
- El sistema extrae entidades.
- El sistema extrae relaciones.
- El sistema crea nodos semánticos.
- El sistema crea relaciones semánticas.
- El sistema guarda evidencia.

### Recuperación

- Dado un prompt, el sistema busca chunks similares.
- El sistema busca nodos relacionados.
- El sistema devuelve un contexto compacto.
- El sistema incluye evidencia cuando exista.

### Persistencia

- PostgreSQL corre en Docker.
- Neo4j corre en Docker.
- Migraciones iniciales creadas.
- Configuración por environment variables.

### API

- Endpoint `/api/memory/ingest`.
- Endpoint `/api/memory/retrieve`.
- Endpoint `/api/memory/facts`.
- Endpoint `/api/memory/explain/{edgeId}`.
- Endpoint `/api/memory/{memoryId}` para olvido lógico.

### Calidad

- Separación por Clean Architecture.
- Interfaces en Application.
- Implementaciones en Infrastructure.
- Dominio sin dependencias de infraestructura.
- Tests unitarios básicos.
- README con instrucciones de ejecución.

---

## 22. Roadmap

### Versión 0.1

```text
- API REST.
- PostgreSQL.
- pgvector.
- MemoryChunks.
- Embeddings.
- Ingesta básica.
- Retrieval vectorial básico.
```

### Versión 0.2

```text
- Neo4j.
- SemanticNodes.
- SemanticEdges.
- Evidence.
- Retrieval híbrido vector + grafo.
```

### Versión 0.3

```text
- GraphQL con HotChocolate.
- Ranking de memorias.
- Confidence scoring.
- Forget explícito.
- Explain memory.
```

### Versión 0.4

```text
- Conflict resolver.
- Temporal memory.
- Decay automático.
- Panel visual de grafo.
```

### Versión 1.0

```text
- Multiusuario.
- Permisos.
- Auditoría.
- Workers async.
- Observabilidad.
- SDK para integración con agentes.
```

---

## 23. Instrucciones para el agente de Codex

El agente debe ejecutar el proyecto en fases.

### Fase 1: Crear solución

1. Crear solución `SemanticMemory.sln`.
2. Crear proyectos:
   - `SemanticMemory.Api`
   - `SemanticMemory.Application`
   - `SemanticMemory.Domain`
   - `SemanticMemory.Infrastructure`
   - `SemanticMemory.Worker`
   - `SemanticMemory.UnitTests`
   - `SemanticMemory.IntegrationTests`
3. Referenciar proyectos siguiendo Clean Architecture:
   - Api → Application, Infrastructure
   - Worker → Application, Infrastructure
   - Infrastructure → Application, Domain
   - Application → Domain
   - Domain → ninguna capa

### Fase 2: Dominio

Crear entidades:

- SemanticNode
- SemanticEdge
- MemoryChunk
- Evidence
- MemoryEvent

Crear enums o constantes:

- MemoryStatus
- MemoryType
- RelationType inicial
- SourceType

### Fase 3: Application

Crear DTOs, commands e interfaces:

- IngestMessageCommand
- IngestionResult
- ExtractedEntity
- ExtractedRelation
- MemoryContext
- IMemoryIngestionService
- IEntityExtractor
- IRelationExtractor
- IEntityNormalizer
- ISemanticGraphStore
- IVectorMemoryStore
- IMemoryRetriever
- IPromptContextBuilder
- IEvidenceStore

### Fase 4: Infrastructure

Implementar:

- PostgreSQL DbContext.
- Migraciones EF Core.
- Repositorio para MemoryChunk.
- Repositorio para Evidence.
- Vector store usando pgvector.
- Neo4j graph store.
- LLM entity extractor.
- LLM relation extractor.
- Embedding provider.

Para el MVP, si no existe API key, implementar extractores fake/mock configurables para desarrollo local.

### Fase 5: API

Crear endpoints:

```text
POST   /api/memory/ingest
POST   /api/memory/retrieve
POST   /api/memory/facts
GET    /api/memory/explain/{edgeId}
DELETE /api/memory/{memoryId}
```

Agregar Swagger.

### Fase 6: Docker

Crear:

- docker-compose.yml
- .env.example
- README.md con instrucciones

### Fase 7: Tests

Crear tests básicos:

- IngestMessage creates MemoryChunk.
- IngestMessage extracts entities.
- IngestMessage extracts relations.
- RetrieveMemory returns context.
- ExplainMemory returns evidence.
- ForgetMemory marks memory as forgotten.

---

## 24. Decisiones de diseño importantes

### No construir una base de datos desde cero en el MVP

El objetivo no es competir con Neo4j, PostgreSQL o Qdrant.

El objetivo es construir la capa inteligente:

```text
decidir qué recordar
extraer conocimiento
guardar evidencia
relacionar conceptos
recuperar contexto
construir prompt
```

### GraphQL no es obligatorio al inicio

GraphQL puede agregarse después.

Primero construir REST + dominio + persistencia.

### El sistema debe ser explicable

Toda relación importante debe tener evidencia asociada.

### El sistema debe ser temporal

Las memorias deben poder quedar activas, olvidadas, archivadas o reemplazadas.

### El sistema debe ser híbrido

No usar solo embeddings.

No usar solo grafo.

Combinar:

```text
Vector Search + Knowledge Graph + Evidence
```

---

## 25. Definition of Done del MVP

El MVP estará terminado cuando se pueda ejecutar este flujo:

1. Levantar PostgreSQL y Neo4j con Docker.
2. Ejecutar la API.
3. Enviar un texto a `/api/memory/ingest`.
4. Ver que se guarda un MemoryChunk.
5. Ver que se generan entidades y relaciones.
6. Ver que existen nodos y edges en Neo4j.
7. Enviar una pregunta a `/api/memory/retrieve`.
8. Recibir un contexto relevante.
9. Consultar `/api/memory/explain/{edgeId}`.
10. Recibir evidencia asociada.

---

## 26. Ejemplo de flujo final esperado

### Ingesta

Request:

```json
{
  "tenantId": "default",
  "userId": "juan",
  "conversationId": "conv-001",
  "text": "Estoy construyendo un motor de memoria semántica para LLMs usando Neo4j, pgvector y GraphQL."
}
```

Resultado conceptual:

```text
MemoryChunk creado.

Nodos:
- Juan
- Semantic Memory Engine
- LLMs
- Neo4j
- pgvector
- GraphQL

Relaciones:
- Juan isBuilding Semantic Memory Engine
- Semantic Memory Engine supports LLMs
- Semantic Memory Engine uses Neo4j
- Semantic Memory Engine uses pgvector
- Semantic Memory Engine exposesApiWith GraphQL

Evidencia:
- Quote original asociado a cada relación.
```

### Recuperación

Request:

```json
{
  "tenantId": "default",
  "userId": "juan",
  "prompt": "¿Qué arquitectura estaba considerando para mi motor?"
}
```

Respuesta conceptual:

```text
El usuario está construyendo un motor de memoria semántica para LLMs.
La arquitectura considerada incluye Neo4j para relaciones semánticas,
pgvector para embeddings y GraphQL como API.
```

---

## 27. Principio guía

La frase guía del proyecto:

> No construir primero una base de datos general. Construir primero un motor de memoria semántica especializado para LLMs.

El valor diferencial del sistema:

```text
No solo recuerda texto.
Recuerda conocimiento conectado, con evidencia, confianza, temporalidad y recuperación contextual.
```
