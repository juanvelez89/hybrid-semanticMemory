# Hybrid Semantic Memory

Hybrid Semantic Memory es un motor de memoria semantica para LLMs. El objetivo es recordar conocimiento de conversaciones, relacionarlo como hechos semanticos y recuperarlo despues como contexto compacto para un modelo.

Este repositorio implementa el MVP hibrido descrito en [hybrid-semanticMemory.md](hybrid-semanticMemory.md):

- `MemoryChunk` para guardar texto fuente.
- Embeddings deterministas en modo desarrollo.
- `SemanticNode` para entidades/conceptos.
- `SemanticEdge` como hecho semantico primario.
- `Evidence` para explicar de donde salio cada hecho.
- Retrieval hibrido: chunks similares + grafo + evidencia.
- Olvido logico por `MemoryStatus`.
- Aislamiento obligatorio por `TenantId + UserId`.

La version actual corre lista para desarrollo con adaptadores fake/in-memory. Esto permite probar la API sin API keys, Postgres ni Neo4j. El `docker-compose.yml` y la migracion SQL inicial quedan incluidos para conectar persistencia real en el siguiente paso.

## Requisitos

- .NET 8 SDK o superior.
- Docker Desktop, opcional para levantar PostgreSQL/pgvector y Neo4j.

## Estructura

```text
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

## Ejecutar en modo desarrollo

Restaurar dependencias:

```bash
dotnet restore SemanticMemory.sln
```

Compilar:

```bash
dotnet build SemanticMemory.sln
```

Ejecutar tests:

```bash
dotnet test SemanticMemory.sln
```

Levantar la API:

```bash
dotnet run --project src/SemanticMemory.Api
```

Swagger queda disponible en:

```text
https://localhost:7000/swagger
http://localhost:5000/swagger
```

Los puertos exactos pueden variar segun `src/SemanticMemory.Api/Properties/launchSettings.json`.

## Ejecutar infraestructura opcional

Crear un `.env` desde el ejemplo:

```bash
cp .env.example .env
```

Levantar servicios:

```bash
docker compose up -d
```

Servicios:

- PostgreSQL + pgvector: `localhost:5432`
- Neo4j Browser: `http://localhost:7474`
- Neo4j Bolt: `bolt://localhost:7687`

La API actual usa stores en memoria por defecto. Para persistencia real, implementar los puertos de `SemanticMemory.Application.Abstractions` en Infrastructure usando PostgreSQL/pgvector y Neo4j.

## Endpoints principales

### Ingesta

```http
POST /api/memory/ingest
```

```json
{
  "tenantId": "default",
  "userId": "juan",
  "conversationId": "conv-001",
  "text": "Estoy construyendo un motor de memoria semantica para LLMs usando Neo4j, pgvector y GraphQL."
}
```

### Recuperacion

```http
POST /api/memory/retrieve
```

```json
{
  "tenantId": "default",
  "userId": "juan",
  "prompt": "Que arquitectura estaba considerando para mi motor?"
}
```

### Hecho manual

```http
POST /api/memory/facts
```

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

### Explicacion

```http
GET /api/memory/explain/{edgeId}?tenantId=default&userId=juan
```

### Olvido logico

```http
DELETE /api/memory/{memoryChunkId}?tenantId=default&userId=juan
```

## Estado del MVP

Implementado:

- Clean Architecture.
- API REST.
- Servicios de aplicacion para ingesta, retrieval, facts, explain y forget.
- Embedding fake determinista.
- Entity/relation extraction fake por reglas.
- Vector store en memoria.
- Semantic graph store en memoria.
- Evidence store en memoria.
- Tests de flujo y composicion.
- Docker Compose para servicios externos.
- Migracion SQL inicial de referencia.

Pendiente para persistencia real:

- Implementar `IVectorMemoryStore` con PostgreSQL + pgvector.
- Implementar `ISemanticGraphStore` con Neo4j.Driver.
- Implementar `IEvidenceStore` y `IMemoryEventStore` con PostgreSQL.
- Agregar migraciones EF Core o pipeline SQL formal.
- Agregar proveedor real de embeddings/LLM.

## Principio de diseno

La unidad factual del MVP hibrido es:

```text
SemanticEdge
```

La fuente explicable de cada hecho es:

```text
Evidence -> MemoryChunk
```

La recuperacion principal es:

```text
Vector Search + Knowledge Graph + Evidence
```
