-- Recreate Workflows and WorkflowSteps tables to match EF Core entities
-- This drops existing tables first

-- Drop existing tables
DROP TABLE IF EXISTS workflow_steps CASCADE;
DROP TABLE IF EXISTS workflows CASCADE;

-- Create Workflows table matching the Workflow entity
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

-- Create WorkflowSteps table matching the WorkflowStep entity
CREATE TABLE workflow_steps (
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
CREATE INDEX idx_workflows_issue_id ON workflows(issue_id);
CREATE INDEX idx_workflows_status ON workflows(status);
CREATE INDEX idx_workflow_steps_workflow_id ON workflow_steps(workflow_id);
CREATE INDEX idx_workflow_steps_status ON workflow_steps(status);

-- Show results
SELECT 'Tables recreated successfully' AS result;
\d workflows
\d workflow_steps
