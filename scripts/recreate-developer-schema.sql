-- Aura Developer Module Schema - Complete Recreation
-- Run this AFTER recreate-foundation-schema.sql
-- WARNING: This will delete all existing workflow data!

-- ============================================
-- Drop existing Developer tables
-- ============================================
DROP TABLE IF EXISTS workflow_steps CASCADE;
DROP TABLE IF EXISTS workflows CASCADE;

-- ============================================
-- Workflows table
-- ============================================
CREATE TABLE workflows (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    title VARCHAR(1000) NOT NULL,
    description TEXT,
    repository_path VARCHAR(1000),      -- Git repo root (where index lives)
    worktree_path VARCHAR(1000),        -- Isolated worktree (where work happens)
    git_branch VARCHAR(500),
    status VARCHAR(20) NOT NULL DEFAULT 'Created',
    analyzed_context JSONB,
    execution_plan JSONB,
    pull_request_url VARCHAR(1000),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at TIMESTAMPTZ
);

CREATE INDEX IX_workflows_status ON workflows(status);
CREATE INDEX IX_workflows_created_at ON workflows(created_at);
CREATE INDEX IX_workflows_repository_path ON workflows(repository_path);

-- ============================================
-- Workflow Steps table
-- ============================================
CREATE TABLE workflow_steps (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workflow_id UUID NOT NULL REFERENCES workflows(id) ON DELETE CASCADE,
    "order" INTEGER NOT NULL,
    name VARCHAR(500) NOT NULL,
    capability VARCHAR(100) NOT NULL,
    language VARCHAR(50),
    description TEXT,
    status VARCHAR(20) NOT NULL DEFAULT 'Pending',
    assigned_agent_id VARCHAR(100),
    input JSONB,
    output JSONB,
    error TEXT,
    attempts INTEGER NOT NULL DEFAULT 0,
    started_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,
    -- Assisted workflow UI fields
    approval VARCHAR(20),
    approval_feedback TEXT,
    skip_reason TEXT,
    chat_history JSONB,
    needs_rework BOOLEAN NOT NULL DEFAULT FALSE,
    previous_output JSONB
);

CREATE INDEX IX_workflow_steps_workflow_id_order ON workflow_steps(workflow_id, "order");
CREATE INDEX IX_workflow_steps_status ON workflow_steps(status);

-- ============================================
-- Verification
-- ============================================
SELECT 'Developer module schema created successfully' AS result;
SELECT table_name, 
       (SELECT COUNT(*) FROM information_schema.columns c 
        WHERE c.table_name = t.table_name AND c.table_schema = 'public') as column_count
FROM information_schema.tables t 
WHERE table_schema = 'public' 
  AND table_name IN ('workflows', 'workflow_steps')
ORDER BY table_name;

-- Show workflow columns for verification
SELECT column_name, data_type, character_maximum_length
FROM information_schema.columns 
WHERE table_name = 'workflows' AND table_schema = 'public'
ORDER BY ordinal_position;
