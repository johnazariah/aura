-- Aura Foundation Schema - Complete Recreation
-- Run this to drop and recreate all Foundation tables with correct columns
-- WARNING: This will delete all existing data!

-- Enable required extensions
CREATE EXTENSION IF NOT EXISTS vector;

-- Drop existing tables in dependency order
DROP TABLE IF EXISTS message_rag_contexts CASCADE;
DROP TABLE IF EXISTS messages CASCADE;
DROP TABLE IF EXISTS agent_executions CASCADE;
DROP TABLE IF EXISTS conversations CASCADE;
DROP TABLE IF EXISTS code_edges CASCADE;
DROP TABLE IF EXISTS code_nodes CASCADE;
DROP TABLE IF EXISTS rag_chunks CASCADE;
DROP TABLE IF EXISTS index_metadata CASCADE;
DROP TABLE IF EXISTS "__EFMigrationsHistory" CASCADE;

-- ============================================
-- Conversations table
-- ============================================
CREATE TABLE conversations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    title VARCHAR(500) NOT NULL,
    agent_id VARCHAR(100) NOT NULL,
    repository_path VARCHAR(1000),  -- Renamed from workspace_path
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IX_conversations_agent_id ON conversations(agent_id);
CREATE INDEX IX_conversations_created_at ON conversations(created_at);

-- ============================================
-- Messages table
-- ============================================
CREATE TABLE messages (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    conversation_id UUID NOT NULL REFERENCES conversations(id) ON DELETE CASCADE,
    role VARCHAR(20) NOT NULL,
    content TEXT NOT NULL,
    model VARCHAR(100),
    tokens_used INTEGER,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IX_messages_conversation_id ON messages(conversation_id);
CREATE INDEX IX_messages_created_at ON messages(created_at);

-- ============================================
-- Message RAG Contexts table
-- ============================================
CREATE TABLE message_rag_contexts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    message_id UUID NOT NULL REFERENCES messages(id) ON DELETE CASCADE,
    query TEXT NOT NULL,
    content_id VARCHAR(500) NOT NULL,
    chunk_index INTEGER NOT NULL,
    chunk_content TEXT NOT NULL,
    score REAL,
    source_path VARCHAR(1000),
    content_type VARCHAR(50),
    retrieved_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IX_message_rag_contexts_message_id ON message_rag_contexts(message_id);
CREATE INDEX IX_message_rag_contexts_content_id ON message_rag_contexts(content_id);

-- ============================================
-- Agent Executions table
-- ============================================
CREATE TABLE agent_executions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    agent_id VARCHAR(100) NOT NULL,
    conversation_id UUID REFERENCES conversations(id) ON DELETE SET NULL,
    prompt TEXT NOT NULL,
    response TEXT,
    model VARCHAR(100),
    provider VARCHAR(50),
    tokens_used INTEGER,
    duration_ms BIGINT,
    success BOOLEAN NOT NULL DEFAULT FALSE,
    error_message TEXT,
    started_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at TIMESTAMPTZ
);

CREATE INDEX IX_agent_executions_agent_id ON agent_executions(agent_id);
CREATE INDEX IX_agent_executions_conversation_id ON agent_executions(conversation_id);
CREATE INDEX IX_agent_executions_started_at ON agent_executions(started_at);
CREATE INDEX IX_agent_executions_success ON agent_executions(success);

-- ============================================
-- RAG Chunks table (vector search)
-- ============================================
CREATE TABLE rag_chunks (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    content_id VARCHAR(500) NOT NULL,
    chunk_index INTEGER NOT NULL,
    content TEXT NOT NULL,
    content_type VARCHAR(50) NOT NULL,
    source_path VARCHAR(1000),
    embedding vector(768),
    metadata JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IX_rag_chunks_content_id ON rag_chunks(content_id);
CREATE UNIQUE INDEX IX_rag_chunks_content_id_chunk_index ON rag_chunks(content_id, chunk_index);

-- ============================================
-- Code Nodes table (graph RAG)
-- ============================================
CREATE TABLE code_nodes (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    node_type VARCHAR(50) NOT NULL,
    name VARCHAR(500) NOT NULL,
    full_name VARCHAR(2000),
    file_path VARCHAR(1000),
    line_number INTEGER,
    signature VARCHAR(2000),
    modifiers VARCHAR(200),
    repository_path VARCHAR(1000),
    properties JSONB,
    embedding vector(768),
    indexed_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IX_code_nodes_node_type ON code_nodes(node_type);
CREATE INDEX IX_code_nodes_name ON code_nodes(name);
CREATE INDEX IX_code_nodes_full_name ON code_nodes(full_name);
CREATE INDEX IX_code_nodes_repository_path ON code_nodes(repository_path);
CREATE INDEX IX_code_nodes_repository_path_node_type ON code_nodes(repository_path, node_type);

-- ============================================
-- Code Edges table (graph RAG)
-- ============================================
CREATE TABLE code_edges (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    edge_type VARCHAR(50) NOT NULL,
    source_id UUID NOT NULL REFERENCES code_nodes(id) ON DELETE CASCADE,
    target_id UUID NOT NULL REFERENCES code_nodes(id) ON DELETE CASCADE,
    properties JSONB
);

CREATE INDEX IX_code_edges_source_id ON code_edges(source_id);
CREATE INDEX IX_code_edges_target_id ON code_edges(target_id);
CREATE INDEX IX_code_edges_edge_type ON code_edges(edge_type);
CREATE INDEX IX_code_edges_source_id_edge_type ON code_edges(source_id, edge_type);
CREATE INDEX IX_code_edges_target_id_edge_type ON code_edges(target_id, edge_type);

-- ============================================
-- Index Metadata table (index freshness tracking)
-- ============================================
CREATE TABLE index_metadata (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_path VARCHAR(1024) NOT NULL,
    index_type VARCHAR(50) NOT NULL,
    indexed_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    commit_sha VARCHAR(40),
    commit_at TIMESTAMPTZ,
    files_indexed INTEGER,
    items_created INTEGER,
    stats JSONB
);

CREATE INDEX IX_index_metadata_workspace_path ON index_metadata(workspace_path);
CREATE INDEX IX_index_metadata_index_type ON index_metadata(index_type);
CREATE UNIQUE INDEX IX_index_metadata_workspace_path_index_type ON index_metadata(workspace_path, index_type);

-- ============================================
-- EF Migrations History (mark as applied)
-- ============================================
CREATE TABLE "__EFMigrationsHistory" (
    "MigrationId" VARCHAR(150) NOT NULL PRIMARY KEY,
    "ProductVersion" VARCHAR(32) NOT NULL
);

-- Mark all foundation migrations as applied
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES 
    ('20251127054022_InitialFoundation', '9.0.0'),
    ('20251128022829_AddRagChunks', '9.0.0'),
    ('20251201000000_AddCodeGraph', '9.0.0'),
    ('20251212000000_RenameWorkspaceToRepository', '9.0.0'),
    ('20250117000000_AddIndexMetadata', '10.0.0');

-- ============================================
-- Verification
-- ============================================
SELECT 'Foundation schema created successfully' AS result;
SELECT table_name, 
       (SELECT COUNT(*) FROM information_schema.columns c 
        WHERE c.table_name = t.table_name AND c.table_schema = 'public') as column_count
FROM information_schema.tables t 
WHERE table_schema = 'public' 
  AND table_name IN ('conversations', 'messages', 'message_rag_contexts', 
                     'agent_executions', 'rag_chunks', 'code_nodes', 'code_edges',
                     'index_metadata')
ORDER BY table_name;
