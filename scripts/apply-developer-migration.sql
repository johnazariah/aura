-- Developer Module Migration: Issues and Workflow Enhancements
-- Run this script against the auradb database to add Developer Module tables

-- Create issues table
CREATE TABLE IF NOT EXISTS issues (
    id UUID PRIMARY KEY,
    title VARCHAR(1000) NOT NULL,
    description TEXT,
    status VARCHAR(20) NOT NULL DEFAULT 'Open',
    repository_path VARCHAR(1000),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Add indexes on issues
CREATE INDEX IF NOT EXISTS ix_issues_status ON issues(status);
CREATE INDEX IF NOT EXISTS ix_issues_created_at ON issues(created_at);

-- Add new columns to workflows table if they don't exist
DO $$
BEGIN
    -- Add issue_id column
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'workflows' AND column_name = 'issue_id') THEN
        ALTER TABLE workflows ADD COLUMN issue_id UUID REFERENCES issues(id) ON DELETE SET NULL;
        CREATE UNIQUE INDEX ix_workflows_issue_id ON workflows(issue_id) WHERE issue_id IS NOT NULL;
    END IF;

    -- Add repository_path column
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'workflows' AND column_name = 'repository_path') THEN
        ALTER TABLE workflows ADD COLUMN repository_path VARCHAR(1000);
    END IF;

    -- Add execution_plan column
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'workflows' AND column_name = 'execution_plan') THEN
        ALTER TABLE workflows ADD COLUMN execution_plan JSONB;
    END IF;

    -- Add completed_at column
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'workflows' AND column_name = 'completed_at') THEN
        ALTER TABLE workflows ADD COLUMN completed_at TIMESTAMPTZ;
    END IF;
END $$;

-- Verify tables exist
SELECT 'issues' as table_name, COUNT(*) as row_count FROM issues
UNION ALL
SELECT 'workflows', COUNT(*) FROM workflows
UNION ALL
SELECT 'workflow_steps', COUNT(*) FROM workflow_steps;
