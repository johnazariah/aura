-- Add Language column to workflow_steps table
-- This separates capability (what to do) from language (what language)

ALTER TABLE workflow_steps 
ADD COLUMN IF NOT EXISTS language VARCHAR(50);

-- Verify
SELECT column_name, data_type 
FROM information_schema.columns 
WHERE table_name = 'workflow_steps' 
  AND column_name IN ('capability', 'language')
ORDER BY ordinal_position;
