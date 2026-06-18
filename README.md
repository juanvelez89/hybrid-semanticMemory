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

La version actual corre lista para desarrollo con adaptadores fake/in-memory para embeddings, extraccion, chunks y evidencia. El grafo semantico puede usar Neo4j Aura si configuras las variables `NEO4J_*`; si no existen, usa el store en memoria.

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

La API usa stores en memoria por defecto. Si configuras `NEO4J_URI`, `NEO4J_USERNAME`, `NEO4J_PASSWORD` y `NEO4J_DATABASE`, el puerto `ISemanticGraphStore` se conecta a Neo4j y crea automaticamente constraints/indices al arrancar.

Si configuras `POSTGRES_CONNECTION_STRING`, la API usa PostgreSQL/pgvector para `MemoryChunk`, embeddings, `Evidence` y `MemoryEvent`. Si no existe, usa stores en memoria.

## Configurar Supabase PostgreSQL

Define la conexion como variable de entorno. Para Supabase puedes usar la URL `postgresql://...` directamente:

```bash
export POSTGRES_CONNECTION_STRING="postgresql://postgres:your-password@db.your-project.supabase.co:5432/postgres"
```

La app normaliza esa URL para Npgsql y fuerza SSL requerido.

Si ves un error como `No route to host` hacia una direccion IPv6, usa el connection string del pooler de Supabase en lugar del host directo. En Supabase esta en:

```text
Project Settings -> Database -> Connection pooling
```

Normalmente usa un host tipo `*.pooler.supabase.com` y puerto `6543` para transaction/session pooler. Ese formato tambien funciona en `POSTGRES_CONNECTION_STRING`.

Luego valida schema y conectividad:

```bash
chmod +x scripts/verify-postgres.sh
scripts/verify-postgres.sh
```

Al arrancar la API con `POSTGRES_CONNECTION_STRING`, se ejecuta automaticamente la migracion equivalente a:

```text
migrations/001_init.sql
```

## Configurar Neo4j Aura

Define estas variables de entorno antes de levantar la API:

```bash
export NEO4J_URI="neo4j+s://your-instance.databases.neo4j.io"
export NEO4J_USERNAME="your-instance-id"
export NEO4J_PASSWORD="your-password"
export NEO4J_DATABASE="your-database"
```

Luego ejecuta:

```bash
dotnet run --project src/SemanticMemory.Api --launch-profile http
```

Al iniciar, la API verifica conectividad y crea este schema en Neo4j:

```text
neo4j/schema.cypher
```

No guardes credenciales reales en `.env.example`, `appsettings.json` ni commits. Usa variables de entorno locales, secretos del sistema o el gestor de despliegue.

Para validar la conexion y crear el schema sin levantar la API:

```bash
chmod +x scripts/verify-neo4j.sh
scripts/verify-neo4j.sh
```

## Integracion con OpenClaw

Para que OpenClaw pueda invocar la memoria como herramientas reales, importa este contrato OpenAPI:

```text
https://semantic-memory.hipatia.tech/swagger/v1/swagger.json
```

El prompt de OpenClaw debe usar estos nombres de herramienta, que ahora quedan publicados como `operationId` estables en Swagger:

```text
semantic_memory_ingest
semantic_memory_retrieve
semantic_memory_facts
semantic_memory_explain
semantic_memory_forget
```

Si OpenClaw responde que `semantic_memory_ingest` no existe, significa que el Swagger no fue importado como herramienta, que el deploy todavia sirve una version anterior del contrato, o que Swagger no esta habilitado con `ENABLE_SWAGGER=true`.

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
- Semantic graph store real para Neo4j Aura usando `Neo4j.Driver`.
- Inicializacion automatica de constraints/indices de Neo4j.
- Vector/evidence/event stores reales para PostgreSQL/pgvector cuando `POSTGRES_CONNECTION_STRING` esta configurado.
- Evidence store en memoria como fallback.
- Tests de flujo y composicion.
- Docker Compose para servicios externos.
- Migracion SQL inicial de referencia.

Pendiente para persistencia real:

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
