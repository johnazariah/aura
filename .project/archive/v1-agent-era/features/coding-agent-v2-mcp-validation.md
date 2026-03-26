# Coding Agent 2.0: MCP Tools + Validation Loop

**Status:** ✅ Complete
**Completed:** 2026-01-28
**Priority:** High
**Type:** Architecture Overhaul

## Overview

Coding agents now have access to semantic code tools (`code.validate`) and enforced validation loops that prevent agents from finishing with broken code.

## Implementation

### 1. CodeValidateTool

**File:** `src/Aura.Foundation/Tools/CodeValidateTool.cs`

Multi-language build validation tool that:
- Auto-detects language from file extensions
- Runs appropriate build command (dotnet, tsc, go, etc.)
- Returns structured errors with line numbers
- Supports C#, TypeScript, Go, Python, Rust

### 2. ValidationTracker

**File:** `src/Aura.Foundation/Tools/ValidationTracker.cs`

Tracks code file modifications during ReAct execution:
- `HasUnvalidatedChanges` - true if code files modified since last validation
- `TrackModifiedFile()` - called by file.write, file.modify
- `RecordValidationSuccess()` - clears tracked files
- `RecordValidationFailure()` - increments failure counter
- `MaxFailures` - force-fail after 5 consecutive failures (default)

### 3. ReActExecutor Enforcement

**File:** `src/Aura.Foundation/Tools/ReActExecutor.cs`

When agent attempts to "finish":
```csharp
if (_validationTracker.HasUnvalidatedChanges)
{
    observation = "Cannot finish: you have unvalidated code changes. " +
                  "You MUST run code.validate before finishing...";
    continue; // Force agent back into loop
}
```

### 4. Prompt Integration

**File:** `prompts/step-execute.prompt`

```yaml
tools:
  - code.validate
```

Agents now have `code.validate` in their tool set and are instructed to validate before finishing.

## Unified Coding Tool Set

| Tool | Purpose | Status |
|------|---------|--------|
| `code.validate` | Build/compile check | ✅ Implemented |
| `code.refactor` | Rename, extract, move | Via MCP (`aura_refactor`) |
| `code.generate` | Create types, methods, tests | Via MCP (`aura_generate`) |
| `code.navigate` | Find usages, callers | Via MCP (`aura_navigate`) |
| `code.search` | Semantic codebase search | Via MCP (`aura_search`) |

Note: MCP tools are available to external Copilot but not yet exposed to internal agents. The validation loop (the critical path) is complete.

## Language Support

| Language | Validation Command |
|----------|-------------------|
| C# | `dotnet build` |
| TypeScript | `tsc --noEmit` |
| Go | `go build ./...` |
| Python | `python -m py_compile` |
| Rust | `cargo check` |

## Files Changed

- `src/Aura.Foundation/Tools/CodeValidateTool.cs` (new)
- `src/Aura.Foundation/Tools/ValidationTracker.cs` (new)
- `src/Aura.Foundation/Tools/ReActExecutor.cs` (enforcement)
- `src/Aura.Foundation/Tools/ToolRegistryInitializer.cs` (registration)
- `prompts/step-execute.prompt` (tool list)
- `tests/Aura.Foundation.Tests/Tools/ValidationTrackerTests.cs` (new)

## Success Metrics

- Agents cannot "finish" with unvalidated code changes
- Build errors are injected as observations for agent to fix
- Max 5 retries before force-fail
