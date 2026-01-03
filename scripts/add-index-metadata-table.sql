-- Add index_metadata table for tracking index freshness
-- Run this to add the table without dropping existing data

-- ============================================
-- Index Metadata table (index freshness tracking)
-- ============================================
CREATE TABLE IF NOT EXISTS index_metadata (
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

CREATE INDEX IF NOT EXISTS IX_index_metadata_workspace_path ON index_metadata(workspace_path);
CREATE INDEX IF NOT EXISTS IX_index_metadata_index_type ON index_metadata(index_type);
CREATE UNIQUE INDEX IF NOT EXISTS IX_index_metadata_workspace_path_index_type ON index_metadata(workspace_path, index_type);

-- ============================================
-- Verification
-- ============================================
SELECT 'index_metadata table created successfully' AS result;
