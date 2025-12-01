-- Aura Database Migrations
-- Run this script to set up the database schema

-- Enable pgvector extension (should already be done)
CREATE EXTENSION IF NOT EXISTS vector;

-- Create migrations history table
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" varchar(150) NOT NULL,
    "ProductVersion" varchar(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

-- ============================================
-- Migration: 20251127054022_InitialFoundation
-- ============================================

DO $$ 
BEGIN
    IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251127054022_InitialFoundation') THEN
        
        CREATE TABLE IF NOT EXISTS conversations (
            id uuid NOT NULL,
            title varchar(500) NOT NULL,
            agent_id varchar(100) NOT NULL,
            workspace_path varchar(1000),
            created_at timestamptz NOT NULL,
            updated_at timestamptz NOT NULL,
            CONSTRAINT "PK_conversations" PRIMARY KEY (id)
        );

        CREATE TABLE IF NOT EXISTS agent_executions (
            id uuid NOT NULL,
            agent_id varchar(100) NOT NULL,
            conversation_id uuid,
            prompt text NOT NULL,
            response text,
            model varchar(100),
            provider varchar(50),
            tokens_used integer,
            duration_ms bigint,
            success boolean NOT NULL,
            error_message text,
            started_at timestamptz NOT NULL,
            completed_at timestamptz,
            CONSTRAINT "PK_agent_executions" PRIMARY KEY (id),
            CONSTRAINT "FK_agent_executions_conversations_conversation_id" 
                FOREIGN KEY (conversation_id) REFERENCES conversations(id) ON DELETE SET NULL
        );

        CREATE TABLE IF NOT EXISTS messages (
            id uuid NOT NULL,
            conversation_id uuid NOT NULL,
            role varchar(20) NOT NULL,
            content text NOT NULL,
            model varchar(100),
            tokens_used integer,
            created_at timestamptz NOT NULL,
            CONSTRAINT "PK_messages" PRIMARY KEY (id),
            CONSTRAINT "FK_messages_conversations_conversation_id"
                FOREIGN KEY (conversation_id) REFERENCES conversations(id) ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS "IX_agent_executions_agent_id" ON agent_executions(agent_id);
        CREATE INDEX IF NOT EXISTS "IX_agent_executions_conversation_id" ON agent_executions(conversation_id);
        CREATE INDEX IF NOT EXISTS "IX_agent_executions_started_at" ON agent_executions(started_at);
        CREATE INDEX IF NOT EXISTS "IX_agent_executions_success" ON agent_executions(success);
        CREATE INDEX IF NOT EXISTS "IX_conversations_agent_id" ON conversations(agent_id);
        CREATE INDEX IF NOT EXISTS "IX_conversations_updated_at" ON conversations(updated_at);
        CREATE INDEX IF NOT EXISTS "IX_messages_conversation_id" ON messages(conversation_id);
        CREATE INDEX IF NOT EXISTS "IX_messages_created_at" ON messages(created_at);

        INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20251127054022_InitialFoundation', '9.0.0');
        
    END IF;
END $$;

-- ============================================
-- Migration: 20251128022829_AddRagChunks
-- ============================================

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251128022829_AddRagChunks') THEN

        CREATE TABLE IF NOT EXISTS rag_chunks (
            id uuid NOT NULL DEFAULT gen_random_uuid(),
            content_id varchar(500) NOT NULL,
            chunk_index integer NOT NULL,
            content text NOT NULL,
            content_type varchar(50) NOT NULL,
            source_path varchar(1000),
            embedding vector(768),
            metadata jsonb,
            created_at timestamptz NOT NULL,
            CONSTRAINT "PK_rag_chunks" PRIMARY KEY (id)
        );

        CREATE INDEX IF NOT EXISTS "IX_rag_chunks_content_id" ON rag_chunks(content_id);
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_rag_chunks_content_id_chunk_index" ON rag_chunks(content_id, chunk_index);

        INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20251128022829_AddRagChunks', '9.0.0');

    END IF;
END $$;

-- ============================================
-- Migration: 20251128045405_AddMessageRagContext
-- ============================================

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251128045405_AddMessageRagContext') THEN

        ALTER TABLE messages ADD COLUMN IF NOT EXISTS rag_context jsonb;

        INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20251128045405_AddMessageRagContext', '9.0.0');

    END IF;
END $$;

-- Done!
SELECT 'Migrations applied successfully!' as status;
SELECT * FROM "__EFMigrationsHistory";
