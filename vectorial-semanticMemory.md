# Vectorial Semantic Memory Engine para LLMs

## 1. Decisiones cerradas

Este documento define el MVP 0.1 tecnico del motor de memoria semantica.

Decisiones principales:

- El MVP 0.1 sera **vectorial**.
- El sistema usara **PostgreSQL + pgvector**.
- No se usara Neo4j en esta version.
- No se implementaran `SemanticNode`, `SemanticEdge` ni `MemoryFact`.
- La unidad principal de memoria sera `MemoryChunk`.
- La recuperacion se hara por similitud semantica entre embeddings.
- La explicacion de una memoria sera el chunk fuente y sus metadatos.
- Todo acceso de lectura y escritura debe estar aislado por `TenantId` y `UserId`.
- El olvido sera logico, no borrado fisico.
- GraphQL no entra en el MVP. Primero REST.

Flujo resumido:

```text
Mensaje del usuario
   ↓
MemoryChunk
   ↓
Embedding
   ↓
pgvector
   ↓
Pregunta futura
   ↓
Chunks similares
   ↓
Contexto compacto para el LLM
```

---

## 2. Objetivo del MVP vectorial

Construir una primera version funcional del motor que pueda:

- Guardar mensajes o notas como `MemoryChunk`.
- Generar embeddings.
- Guardar embeddings en pgvector.
- Recuperar chunks relevantes por significado.
- Construir contexto compacto para un LLM.
- Explicar de que chunk salio una memoria recuperada.
- Olvidar memorias de forma logica.

Este MVP no intenta representar conocimiento conectado. Su objetivo es crear una base estable de memoria persistente y recuperacion semantica.

---

## 3. Alcance

Incluido en el MVP:

- ASP.NET Core Web API.
- Clean Architecture.
- PostgreSQL.
- pgvector.
- EF Core.
- Embedding provider configurable.
- Embedding provider fake/mock para desarrollo local.
- Endpoints REST principales.
- Swagger.
- Docker Compose.
- Tests unitarios e integracion basicos.

Fuera del MVP:

- Neo4j.
- Grafo semantico.
- Extraccion de entidades.
- Extraccion de relaciones.
- `SemanticNode`.
- `SemanticEdge`.
- `Evidence` como entidad separada.
- Contradicciones semanticas.
- GraphQL.
- Worker asincrono real.
- Decay automatico.
- Panel visual.

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
PostgreSQL + pgvector + Embedding Provider
```

Solucion:

```text
SemanticMemory.sln

src/
  SemanticMemory.Api/
  SemanticMemory.Application/
  SemanticMemory.Domain/
  SemanticMemory.Infrastructure/

tests/
  SemanticMemory.UnitTests/
  SemanticMemory.IntegrationTests/
```

Dependencias:

```text
Api            -> Application, Infrastructure
Infrastructure -> Application, Domain
Application    -> Domain
Domain         -> ninguna capa
```

`SemanticMemory.Worker` puede agregarse despues, pero no es necesario en el MVP 0.1.

---

## 5. Regla de aislamiento

Toda operacion debe incluir:

```text
TenantId
UserId
```

Regla obligatoria:

```text
Toda consulta a PostgreSQL y pgvector debe filtrar por TenantId + UserId.
```

Esto aplica a:

- ingesta
- recuperacion
- explain
- forget
- busqueda vectorial
- listados futuros

La memoria de un usuario nunca debe aparecer en la recuperacion de otro usuario.

---

## 6. Modelo de dominio

### MemoryStatus

```csharp
public enum MemoryStatus
{
    Active = 1,
    Forgotten = 2,
    Archived = 3
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
    Decision = 8,
    Note = 9
}
```

### SourceType

```csharp
public enum SourceType
{
    Conversation = 1,
    ManualNote = 2,
    Document = 3,
    System = 4
}
```

### MemoryChunk

`MemoryChunk` es la unidad principal de memoria en el MVP vectorial.

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
    public string EmbeddingModel { get; set; } = default!;

    public MemoryType MemoryType { get; set; } = MemoryType.LongTermMemory;
    public MemoryStatus Status { get; set; } = MemoryStatus.Active;
    public SourceType SourceType { get; set; } = SourceType.Conversation;

    public double Importance { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ForgottenAt { get; set; }
}
```

Reglas:

- `RawText` es obligatorio.
- `Embedding` se genera al ingerir la memoria.
- `Status = Forgotten` excluye el chunk de retrieval.
- `Importance` permite priorizar chunks aunque la similitud sea parecida.
- `EmbeddingModel` permite saber con que modelo se genero el vector.

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
MemoryRetrieved
MemoryForgotten
```

---

## 7. DTOs principales

### IngestMemoryCommand

```csharp
public sealed record IngestMemoryCommand(
    string TenantId,
    string UserId,
    string? ConversationId,
    string Text,
    MemoryType MemoryType = MemoryType.LongTermMemory,
    SourceType SourceType = SourceType.Conversation,
    double Importance = 0.5
);
```

### IngestMemoryResult

```csharp
public sealed record IngestMemoryResult(
    Guid MemoryChunkId,
    string TenantId,
    string UserId,
    MemoryType MemoryType,
    SourceType SourceType,
    double Importance,
    DateTimeOffset CreatedAt
);
```

### RetrieveMemoryQuery

```csharp
public sealed record RetrieveMemoryQuery(
    string TenantId,
    string UserId,
    string Prompt,
    int Limit = 10,
    int MaxContextTokens = 1200
);
```

### ScoredMemoryChunk

```csharp
public sealed record ScoredMemoryChunk(
    Guid MemoryChunkId,
    string RawText,
    string? Summary,
    double Similarity,
    double Importance,
    double Recency,
    double Score,
    MemoryType MemoryType,
    SourceType SourceType,
    DateTimeOffset CreatedAt
);
```

### MemoryContext

```csharp
public sealed record MemoryContext(
    IReadOnlyList<ScoredMemoryChunk> Chunks,
    string ContextText
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

### ExplainMemoryQuery

```csharp
public sealed record ExplainMemoryQuery(
    string TenantId,
    string UserId,
    Guid MemoryChunkId
);
```

---

## 8. Interfaces de Application

### Ingesta

```csharp
public interface IMemoryIngestionService
{
    Task<IngestMemoryResult> IngestAsync(
        IngestMemoryCommand command,
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
    Task<MemoryChunk> ExplainAsync(
        ExplainMemoryQuery query,
        CancellationToken cancellationToken);
}
```

### Embeddings

```csharp
public interface IEmbeddingProvider
{
    string Model { get; }

    Task<float[]> CreateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken);
}
```

### Store vectorial

```csharp
public interface IVectorMemoryStore
{
    Task<MemoryChunk> SaveAsync(
        MemoryChunk chunk,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ScoredMemoryChunk>> SearchSimilarAsync(
        string tenantId,
        string userId,
        string prompt,
        int limit,
        CancellationToken cancellationToken);

    Task<MemoryChunk?> GetByIdAsync(
        string tenantId,
        string userId,
        Guid memoryChunkId,
        CancellationToken cancellationToken);

    Task MarkForgottenAsync(
        string tenantId,
        string userId,
        Guid memoryChunkId,
        CancellationToken cancellationToken);
}
```

### Context builder

```csharp
public interface IPromptContextBuilder
{
    string BuildContext(
        IReadOnlyList<ScoredMemoryChunk> chunks,
        int maxTokens);
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
  "text": "Estoy construyendo un motor de memoria semantica para LLMs usando pgvector.",
  "memoryType": "ProjectMemory",
  "sourceType": "Conversation",
  "importance": 0.8
}
```

Response:

```json
{
  "memoryChunkId": "guid",
  "tenantId": "default",
  "userId": "juan",
  "memoryType": "ProjectMemory",
  "sourceType": "Conversation",
  "importance": 0.8,
  "createdAt": "2026-06-17T00:00:00Z"
}
```

### POST /api/memory/retrieve

Request:

```json
{
  "tenantId": "default",
  "userId": "juan",
  "prompt": "Que estaba construyendo con pgvector?",
  "limit": 5,
  "maxContextTokens": 1200
}
```

Response:

```json
{
  "context": "El usuario esta construyendo un motor de memoria semantica para LLMs usando pgvector.",
  "chunks": [
    {
      "memoryChunkId": "guid",
      "text": "Estoy construyendo un motor de memoria semantica para LLMs usando pgvector.",
      "similarity": 0.91,
      "importance": 0.8,
      "recency": 1.0,
      "score": 0.89,
      "memoryType": "ProjectMemory",
      "sourceType": "Conversation",
      "createdAt": "2026-06-17T00:00:00Z"
    }
  ]
}
```

### GET /api/memory/explain/{memoryChunkId}

Request params:

```text
tenantId
userId
```

Response:

```json
{
  "memoryChunkId": "guid",
  "rawText": "Estoy construyendo un motor de memoria semantica para LLMs usando pgvector.",
  "summary": null,
  "sourceType": "Conversation",
  "memoryType": "ProjectMemory",
  "importance": 0.8,
  "status": "Active",
  "embeddingModel": "text-embedding-3-small",
  "createdAt": "2026-06-17T00:00:00Z"
}
```

### DELETE /api/memory/{memoryChunkId}

Debe marcar el `MemoryChunk` como `Forgotten`.

Comportamiento:

- No borra fisicamente el registro.
- No elimina el embedding.
- Excluye el chunk de futuras recuperaciones.
- Registra evento `MemoryForgotten`.

---

## 10. Flujo de ingesta

Pasos:

```text
1. Validar TenantId, UserId y Text.
2. Crear MemoryChunk con Status=Active.
3. Generar embedding para Text.
4. Guardar EmbeddingModel.
5. Guardar MemoryChunk en PostgreSQL/pgvector.
6. Registrar MemoryChunkCreated.
7. Registrar EmbeddingCreated.
8. Devolver IngestMemoryResult.
```

Reglas:

- Si el embedding provider falla, la ingesta debe fallar en el MVP.
- Si no hay API key, usar provider fake configurable.
- El provider fake debe generar vectores deterministas para permitir tests.

---

## 11. Flujo de recuperacion vectorial

Pasos:

```text
1. Validar TenantId, UserId y Prompt.
2. Crear embedding del Prompt.
3. Buscar chunks similares en pgvector.
4. Filtrar TenantId + UserId + Status=Active.
5. Calcular score final.
6. Ordenar por score descendente.
7. Construir contexto compacto.
8. Devolver MemoryContext.
```

El contexto debe ser compacto y no debe inventar relaciones no presentes.

Formato recomendado:

```text
Relevant memory:
- [2026-06-17] Estoy construyendo un motor de memoria semantica para LLMs usando pgvector.
```

---

## 12. Scoring vectorial

Formula inicial:

```text
score =
  semanticSimilarity * 0.70
+ importance         * 0.20
+ recency            * 0.10
```

Normalizacion:

```text
semanticSimilarity: 0..1 desde pgvector.
importance: 0..1 indicado en ingesta o calculado por heuristica.
recency: 1.0 <= 7 dias, 0.75 <= 30 dias, 0.5 <= 90 dias, 0.25 > 90 dias.
```

Reglas:

- Chunks `Forgotten` o `Archived` no entran en retrieval.
- Si dos chunks tienen score similar, priorizar mayor `Importance`.
- Si el contexto excede `MaxContextTokens`, mantener los chunks con mayor score.

---

## 13. Olvido

El olvido es logico.

Estados:

```text
Active
Forgotten
Archived
```

Reglas:

- `DELETE /api/memory/{memoryChunkId}` marca el chunk como `Forgotten`.
- Retrieval no devuelve chunks olvidados.
- Explain puede devolver un chunk olvidado solo si la API lo permite explicitamente en el futuro.
- El borrado fisico queda fuera del MVP.

---

## 14. Persistencia

### PostgreSQL

Tablas:

```text
memory_chunks
memory_events
```

Indices minimos:

```text
memory_chunks(tenant_id, user_id, status, created_at)
memory_chunks(tenant_id, user_id, memory_type)
memory_chunks(tenant_id, user_id, source_type)
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

El valor `1536` corresponde a `text-embedding-3-small`.

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

---

## 15. Docker Compose

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

volumes:
  postgres_data:
```

---

## 16. Variables de entorno

```env
ASPNETCORE_ENVIRONMENT=Development

POSTGRES_CONNECTION_STRING=Host=localhost;Port=5432;Database=semantic_memory;Username=semantic;Password=semantic

EMBEDDING_PROVIDER=OpenAI
OPENAI_API_KEY=your-api-key-here
EMBEDDING_MODEL=text-embedding-3-small
EMBEDDING_DIMENSIONS=1536

USE_FAKE_EMBEDDINGS=true
```

---

## 17. Criterios de aceptacion

El MVP 0.1 estara listo cuando se pueda ejecutar este flujo:

```text
1. Levantar PostgreSQL con Docker.
2. Ejecutar migraciones.
3. Ejecutar la API.
4. Enviar texto a POST /api/memory/ingest.
5. Ver MemoryChunk activo en PostgreSQL.
6. Ver embedding guardado en pgvector.
7. Enviar pregunta a POST /api/memory/retrieve.
8. Recibir chunks relevantes y contexto compacto.
9. Consultar GET /api/memory/explain/{memoryChunkId}.
10. Recibir el texto fuente y metadatos del chunk.
11. Ejecutar DELETE /api/memory/{memoryChunkId}.
12. Confirmar que ese chunk ya no aparece en retrieve.
```

---

## 18. Tests minimos

Unit tests:

- `IngestMemory` crea `MemoryChunk`.
- `IngestMemory` llama a `IEmbeddingProvider`.
- `IngestMemory` guarda `EmbeddingModel`.
- `RetrieveMemory` llama al store vectorial.
- `RetrieveMemory` devuelve contexto compacto.
- `ForgetMemory` marca chunk como `Forgotten`.
- `PromptContextBuilder` respeta `MaxContextTokens`.
- `FakeEmbeddingProvider` genera vectores deterministas.

Integration tests:

- Retrieval no cruza tenants.
- Retrieval no cruza users.
- Retrieval excluye chunks olvidados.
- Explain devuelve el chunk correcto.
- Busqueda vectorial devuelve resultados ordenados por score.

---

## 19. Camino hacia el MVP hibrido

El MVP vectorial debe dejar espacio para evolucionar al MVP hibrido.

En version 0.2 se agregara:

```text
SemanticNode
SemanticEdge
Evidence
Neo4j
Entity Extraction
Relation Extraction
Hybrid Retrieval
Explain by edge
Contradictions
```

Mapeo futuro:

```text
MemoryChunk permanece como fuente textual.
Embedding permanece para busqueda semantica.
SemanticEdge se vuelve la unidad factual.
Evidence conecta SemanticEdge con MemoryChunk.
```

---

## 20. Principio guia

El MVP vectorial no intenta entender todo.

Su valor es recordar por significado:

```text
texto -> embedding -> similitud -> contexto
```

La unidad principal del MVP 0.1 es:

```text
MemoryChunk
```

La recuperacion valiosa es:

```text
Vector Search + Compact Context
```
