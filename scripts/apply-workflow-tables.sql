-- Create Workflows and WorkflowSteps tables for Developer Module
-- Run this after apply-developer-migration.sql

-- Create Workflows table
CREATE TABLE IF NOT EXISTS workflows (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    issue_id UUID REFERENCES issues(id) ON DELETE SET NULL,
    title VARCHAR(500) NOT NULL,
    description TEXT,
    repository_path VARCHAR(1000),
    worktree_path VARCHAR(1000),
    worktree_branch VARCHAR(255),
    status VARCHAR(50) NOT NULL DEFAULT 'Created',
    execution_plan TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    started_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Create WorkflowSteps table
CREATE TABLE IF NOT EXISTS workflow_steps (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workflow_id UUID NOT NULL REFERENCES workflows(id) ON DELETE CASCADE,
    step_number INT NOT NULL,
    title VARCHAR(500) NOT NULL,
    description TEXT,
    agent_name VARCHAR(255),
    status VARCHAR(50) NOT NULL DEFAULT 'Pending',
    result TEXT,
    error_message TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    started_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ
);

-- Create indexes
CREATE INDEX IF NOT EXISTS idx_workflows_issue_id ON workflows(issue_id);
CREATE INDEX IF NOT EXISTS idx_workflows_status ON workflows(status);
CREATE INDEX IF NOT EXISTS idx_workflow_steps_workflow_id ON workflow_steps(workflow_id);
CREATE INDEX IF NOT EXISTS idx_workflow_steps_status ON workflow_steps(status);

-- Show results
SELECT 'Tables created successfully' AS result;
SELECT table_name, 
       (SELECT COUNT(*) FROM information_schema.columns WHERE table_name = t.table_name) as column_count
FROM information_schema.tables t 
WHERE table_schema = 'public' 
  AND table_name IN ('workflows', 'workflow_steps', 'issues')
ORDER BY table_name;
