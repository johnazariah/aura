# Handoff: File Tools Consolidation & Step Execution Prompts

**Date**: December 7, 2025  
**Commit**: `b4f233f` (WIP)  
**Branch**: `main`

## Summary

This session consolidated file operation tools from the Developer module into Foundation, and enhanced step execution prompts to ensure the LLM actually writes files rather than just outputting content.

## What Was Done

### 1. File Tools Consolidated in Foundation

**Location**: `src/Aura.Foundation/Tools/BuiltInTools.cs`

All file tools are now in Foundation's `BuiltInTools.cs`:
- `file.read` - Read files with optional `startLine`/`endLine` range
- `file.write` - Write files (requires `overwrite: true` for existing files)
- `file.modify` - Search and replace text in files
- `file.list` - List directory contents
- `file.exists` - Check if file exists
- `file.delete` - Delete files

All file operations resolve relative paths against `WorkingDirectory` from context.

**Deleted from Developer module**:
- `src/Aura.Module.Developer/Tools/ReadFileTool.cs`
- `src/Aura.Module.Developer/Tools/WriteFileTool.cs`
- `src/Aura.Module.Developer/Tools/ModifyFileTool.cs`

### 2. ReActExecutor Enhanced

**Location**: `src/Aura.Foundation/Tools/ReActExecutor.cs`

- System prompt now includes explicit list of valid tool IDs
- Uses `$$"""` raw string literal for proper interpolation
- Added "WRONG" examples showing invalid tool names
- Better error message when tool not found reminds LLM to use "finish"

### 3. Step Execution Prompts Improved

**Files**:
- `prompts/step-execute.prompt`
- `prompts/step-execute-documentation.prompt`

Changes:
- Added keywords: "Add", "Integrate", "Insert" to trigger file-writing behavior
- Added **CRITICAL** callouts emphasizing `file.write`/`file.modify` MUST be called
- Added "The step is NOT complete until changes are saved to disk"

## Known Issue Being Investigated

### Problem
When executing workflow steps, the LLM sometimes:
1. Reads the file correctly
2. Generates excellent content in its "finish" output
3. But **never calls `file.write` or `file.modify`**

### Evidence
Workflow v9 step 3 ("Integrate usage examples into README.md"):
- Read README.md 3 times
- Generated a detailed usage section in finish output
- Never called file.write - README unchanged

### Root Cause Analysis
1. Step name "Integrate usage examples" didn't match keywords ("Draft", "Create", "Write", "Update")
2. LLM interpreted task as "planning" rather than "executing"
3. Prompt said "Output the content" rather than "Write the content to the file"

### Fix Applied (needs testing)
Updated prompts to:
- Include "Integrate", "Add", "Insert" as action keywords
- Add **CRITICAL** instructions that file tools MUST be called
- State "step is NOT complete until changes are saved to disk"

## How to Test

1. Start the API server: `Start-Api`
2. The prompts are hot-reloadable - no restart needed after prompt changes
3. Create a new workflow or re-run existing one:

```powershell
# Check existing workflow
$wf = Invoke-RestMethod -Uri "http://localhost:5300/api/developer/workflows/6a364bac-a945-47ab-bd34-81aafb4df3cc"
$wf.steps | Select-Object order, name, capability, status

# Execute a step (step 3 was the one with issues)
Invoke-RestMethod -Uri "http://localhost:5300/api/developer/workflows/6a364bac-a945-47ab-bd34-81aafb4df3cc/steps/9b70599d-69bc-47ea-8373-3bfaccfa9746/execute" -Method Post -ContentType "application/json" -Body "{}"

# Check if README was modified in the workflow workspace
Get-Content "C:\work\aura-workflow-add-usage-examples-to-readme-8504f0b08f424a2bbe5232e8e\README.md" | Select-Object -Last 30
```

## Files Changed (18 files)

| File | Change |
|------|--------|
| `src/Aura.Foundation/Tools/BuiltInTools.cs` | Enhanced with file.modify, line ranges, overwrite protection |
| `src/Aura.Foundation/Tools/ReActExecutor.cs` | Explicit tool list in system prompt |
| `src/Aura.Foundation/Tools/ITool.cs` | Minor changes |
| `src/Aura.Module.Developer/DeveloperModule.cs` | Removed file tool registration |
| `src/Aura.Module.Developer/Tools/*.cs` | Deleted 3 files |
| `prompts/step-execute.prompt` | Added keywords, CRITICAL instructions |
| `prompts/step-execute-documentation.prompt` | Added keywords, CRITICAL instructions |
| `tests/Aura.Foundation.Tests/Tools/BuiltInToolsTests.cs` | Comprehensive file tool tests |
| `tests/Aura.Foundation.Tests/SilverThreadTests.cs` | Fixed shell.execute test |

## Test Status

All 376 tests pass:
- 284 Foundation tests
- 92 Developer tests

## Next Steps

1. **Verify prompt fix works**: Re-run step 3 and confirm README is actually modified
2. **Complete workflow v9**: Execute steps 4 and 5
3. **Consider additional prompt improvements** if LLM still doesn't call file.write:
   - Make the instruction even more explicit in ReActExecutor system prompt
   - Add example showing the correct tool call sequence

## Key Code Locations

| Concept | File |
|---------|------|
| File tools | `src/Aura.Foundation/Tools/BuiltInTools.cs` |
| ReAct executor | `src/Aura.Foundation/Tools/ReActExecutor.cs` |
| Tool registry | `src/Aura.Foundation/Tools/ToolRegistryInitializer.cs` |
| Step execution | `src/Aura.Module.Developer/Services/WorkflowService.cs` (line ~500) |
| Step prompts | `prompts/step-execute*.prompt` |

## Context for Debugging

The workflow workspace is at:
```
C:\work\aura-workflow-add-usage-examples-to-readme-8504f0b08f424a2bbe5232e8e
```

This is a git worktree created for the workflow, containing a copy of the Aura codebase.
