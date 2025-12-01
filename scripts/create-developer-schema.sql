-- Complete Developer Module schema
-- Drop and recreate all Developer Module tables with correct columns

-- Drop existing tables
DROP TABLE IF EXISTS workflow_steps CASCADE;
DROP TABLE IF EXISTS workflows CASCADE;

-- Create Workflows table matching the Workflow entity exactly
CREATE TABLE workflows (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    issue_id UUID REFERENCES issues(id) ON DELETE SET NULL,
    repository_path VARCHAR(1000),
    work_item_id VARCHAR(500) NOT NULL,
    work_item_title VARCHAR(500) NOT NULL,
    work_item_description TEXT,
    work_item_url VARCHAR(2000),
    status VARCHAR(50) NOT NULL DEFAULT 'Created',
    workspace_path VARCHAR(1000),
    git_branch VARCHAR(255),
    digested_context TEXT,
    execution_plan TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at TIMESTAMPTZ
);

-- Create WorkflowSteps table matching the WorkflowStep entity exactly
CREATE TABLE workflow_steps (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workflow_id UUID NOT NULL REFERENCES workflows(id) ON DELETE CASCADE,
    "order" INT NOT NULL,
    name VARCHAR(500) NOT NULL,
    capability VARCHAR(255) NOT NULL,
    description TEXT,
    status VARCHAR(50) NOT NULL DEFAULT 'Pending',
    assigned_agent_id VARCHAR(255),
    input TEXT,
    output TEXT,
    error TEXT,
    attempts INT NOT NULL DEFAULT 0,
    started_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ
);

-- Create indexes
CREATE INDEX idx_workflows_issue_id ON workflows(issue_id);
CREATE INDEX idx_workflows_status ON workflows(status);
CREATE INDEX idx_workflow_steps_workflow_id ON workflow_steps(workflow_id);
CREATE INDEX idx_workflow_steps_status ON workflow_steps(status);

-- Show results
SELECT 'Developer Module schema created successfully' AS result;
SELECT table_name, 
       (SELECT COUNT(*) FROM information_schema.columns c WHERE c.table_name = t.table_name AND c.table_schema = 'public') as column_count
FROM information_schema.tables t 
WHERE table_schema = 'public' 
  AND table_name IN ('workflows', 'workflow_steps', 'issues')
ORDER BY table_name;
