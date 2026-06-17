CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE IF NOT EXISTS memory_chunks (
    id uuid PRIMARY KEY,
    tenant_id text NOT NULL,
    user_id text NOT NULL,
    conversation_id text NULL,
    raw_text text NOT NULL,
    summary text NULL,
    embedding vector(1536) NULL,
    memory_type text NOT NULL,
    status text NOT NULL,
    source_type text NOT NULL,
    importance double precision NOT NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    forgotten_at timestamptz NULL
);

CREATE TABLE IF NOT EXISTS evidence (
    id uuid PRIMARY KEY,
    tenant_id text NOT NULL,
    user_id text NOT NULL,
    edge_id uuid NOT NULL,
    memory_chunk_id uuid NOT NULL REFERENCES memory_chunks(id),
    quote text NULL,
    source_type text NOT NULL,
    confidence double precision NOT NULL,
    created_at timestamptz NOT NULL
);

CREATE TABLE IF NOT EXISTS memory_events (
    id uuid PRIMARY KEY,
    tenant_id text NOT NULL,
    user_id text NOT NULL,
    event_type text NOT NULL,
    entity_type text NOT NULL,
    entity_id uuid NOT NULL,
    payload_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at timestamptz NOT NULL
);

CREATE INDEX IF NOT EXISTS memory_chunks_tenant_user_status_created_idx
    ON memory_chunks (tenant_id, user_id, status, created_at);

CREATE INDEX IF NOT EXISTS memory_chunks_tenant_user_source_idx
    ON memory_chunks (tenant_id, user_id, source_type);

CREATE INDEX IF NOT EXISTS evidence_tenant_user_edge_idx
    ON evidence (tenant_id, user_id, edge_id);

CREATE INDEX IF NOT EXISTS evidence_tenant_user_chunk_idx
    ON evidence (tenant_id, user_id, memory_chunk_id);

CREATE INDEX IF NOT EXISTS memory_events_tenant_user_created_idx
    ON memory_events (tenant_id, user_id, created_at);

CREATE INDEX IF NOT EXISTS memory_chunks_embedding_idx
    ON memory_chunks
    USING ivfflat (embedding vector_cosine_ops);
