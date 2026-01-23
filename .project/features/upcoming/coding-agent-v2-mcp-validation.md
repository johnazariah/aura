# Coding Agent 2.0: MCP Tools + Validation Loop

**Status:** 📋 Design
**Priority:** High
**Type:** Architecture Overhaul
**Estimated Effort:** 2-3 days

## Problem Statement

Current coding agents are hobbled:

1. **No semantic tools** - They have `file.modify` but not `aura_refactor`. They grep instead of using `aura_navigate`. They can't generate code structurally.
2. **No enforced validation** - Agents "finish" with broken code. The prompt says "validate" but agents ignore it.
3. **Language silos** - Each language has separate tool definitions, duplicating validation logic.

## Vision

Coding agents should have the same power as a human developer using Copilot + MCP tools:

```
Agent thinks: "I need to rename this class"
Agent uses: aura_refactor(operation: "rename", ...)  ← semantic, safe
NOT: grep + file.modify across 20 files  ← error-prone
```

## Design

### 1. Expose MCP Tools to Agents

The Aura MCP tools (`aura_*`) are currently only available to the external MCP server. We need to expose them internally to the ReAct executor.

**Option A: Internal Tool Wrappers**
Create internal `ToolDefinition` wrappers that call the same underlying services:

```csharp
// New tool: code.refactor (wraps aura_refactor)
public class CodeRefactorTool : TypedToolBase<RefactorInput, RefactorOutput>
{
    public override string ToolId => "code.refactor";
    
    // Delegates to the same RefactorService that MCP uses
}
```

**Option B: Self-MCP Call**
Agent calls the MCP server via HTTP (localhost). Simpler but adds latency.

**Recommendation: Option A** - Direct service calls, no HTTP overhead.

### 2. Unified Coding Tool Set

Replace fragmented language tools with a unified set:

| Tool | Purpose | Replaces |
|------|---------|----------|
| `code.refactor` | Rename, extract, move | Manual grep+modify |
| `code.generate` | Create types, methods, tests | Manual file.write |
| `code.navigate` | Find usages, callers, implementations | grep_search |
| `code.inspect` | Examine type structure | file.read + parsing |
| `code.validate` | Build/compile check | roslyn.validate_compilation |
| `code.search` | Semantic codebase search | grep_search |

These delegate to language-specific implementations:
- C#: Roslyn APIs
- TypeScript: TypeScript compiler API
- Python: AST + mypy/pyright
- Go: go/ast + go build

### 3. Validation Loop Built Into Agent

Not a post-hoc check - validation is part of the agent's core loop:

```yaml
# In step-execute.prompt or coding-agent.md
## Workflow

For EVERY code change:
1. Make the change (code.refactor, code.generate, or file.modify)
2. Validate immediately: code.validate
3. If errors → fix and repeat
4. Only proceed when validation passes

You CANNOT call "finish" until code.validate returns success.
```

**Enforcement**: The `code.validate` tool tracks state. If code files were modified since last successful validation, `finish` is rejected:

```csharp
// In ReActExecutor, when handling "finish":
if (_validationTracker.HasUnvalidatedChanges)
{
    observation = "Cannot finish: you have unvalidated code changes. Run code.validate first.";
    continue;
}
```

### 4. Language Detection & Dispatch

The `code.*` tools auto-detect language from file paths and dispatch to the right implementation:

```csharp
public class CodeValidateTool : TypedToolBase<ValidateInput, ValidateOutput>
{
    public override async Task<ValidateOutput> ExecuteAsync(ValidateInput input, ...)
    {
        var language = DetectLanguage(input.WorkingDirectory);
        
        return language switch
        {
            "csharp" => await _roslynValidator.ValidateAsync(...),
            "typescript" => await _tscValidator.ValidateAsync(...),
            "python" => await _pythonValidator.ValidateAsync(...),
            _ => await _shellValidator.ValidateAsync(...) // Fallback: run build script
        };
    }
}
```

### 5. Tool Registration

Update the prompt registry to include semantic tools for coding prompts:

```yaml
# step-execute.prompt
---
tools:
  # File tools (basic)
  - file.read
  - file.write
  - file.modify
  - file.list
  
  # Semantic code tools (new)
  - code.refactor
  - code.generate
  - code.navigate
  - code.inspect
  - code.validate
  - code.search
  
  # Git tools
  - git.status
  - git.commit
  
  # Shell (fallback)
  - shell.execute
---
```

## Implementation Plan

### Phase 1: Core Infrastructure (Day 1)

1. Create `ICodeToolService` interface with language dispatch
2. Implement `CodeValidateTool` wrapping existing validators
3. Add `ValidationTracker` to ReActExecutor
4. Enforce "no finish without validation" rule

### Phase 2: Semantic Tools (Day 2)

1. Create `CodeRefactorTool` wrapping `RefactorService`
2. Create `CodeGenerateTool` wrapping `GenerateService`  
3. Create `CodeNavigateTool` wrapping `NavigateService`
4. Create `CodeSearchTool` wrapping semantic search

### Phase 3: Multi-Language (Day 3)

1. TypeScript validator (tsc --noEmit)
2. Python validator (mypy or py_compile)
3. Go validator (go build)
4. Language detection from project files

### Phase 4: Agent Updates

1. Update `step-execute.prompt` with new tools
2. Update `coding-agent.md` with validation workflow
3. Update language YAMLs to reference unified tools
4. Remove deprecated individual language tools

## File Changes

| File | Change |
|------|--------|
| `src/Aura.Foundation/Tools/CodeValidateTool.cs` | New - unified validation |
| `src/Aura.Foundation/Tools/CodeRefactorTool.cs` | New - wraps refactor |
| `src/Aura.Foundation/Tools/CodeGenerateTool.cs` | New - wraps generate |
| `src/Aura.Foundation/Tools/CodeNavigateTool.cs` | New - wraps navigate |
| `src/Aura.Foundation/Tools/CodeSearchTool.cs` | New - wraps search |
| `src/Aura.Foundation/Tools/ValidationTracker.cs` | New - tracks unvalidated changes |
| `src/Aura.Foundation/Tools/ReActExecutor.cs` | Add validation enforcement |
| `prompts/step-execute.prompt` | Add code.* tools |
| `agents/coding-agent.md` | Update workflow |

## Tool Schemas

### code.validate

```json
{
  "name": "code.validate",
  "description": "Compile/build the project and report errors. MUST be called after any code changes before finishing.",
  "inputSchema": {
    "properties": {
      "projectPath": { "type": "string", "description": "Path to project/solution. Auto-detected if omitted." },
      "language": { "type": "string", "enum": ["csharp", "typescript", "python", "go", "auto"], "default": "auto" }
    }
  }
}
```

**Output:**
```json
{
  "success": false,
  "language": "csharp",
  "errorCount": 2,
  "warningCount": 1,
  "errors": [
    { "file": "src/Services/OrderService.cs", "line": 42, "code": "CS1002", "message": "; expected" },
    { "file": "src/Services/OrderService.cs", "line": 45, "code": "CS0103", "message": "The name 'ordr' does not exist" }
  ],
  "command": "dotnet build Aura.sln"
}
```

### code.refactor

```json
{
  "name": "code.refactor",
  "description": "Semantic code refactoring: rename, extract, move. Safer than manual file.modify.",
  "inputSchema": {
    "properties": {
      "operation": { "type": "string", "enum": ["rename", "extract_method", "extract_variable", "extract_interface", "move_type", "safe_delete"] },
      "filePath": { "type": "string", "description": "File containing the symbol" },
      "symbolName": { "type": "string", "description": "Symbol to refactor" },
      "newName": { "type": "string", "description": "New name (for rename/extract operations)" },
      "startLine": { "type": "integer", "description": "Start line (for extract operations)" },
      "endLine": { "type": "integer", "description": "End line (for extract operations)" },
      "preview": { "type": "boolean", "default": false, "description": "If true, show what would change without applying" }
    },
    "required": ["operation", "filePath"]
  }
}
```

### code.generate

```json
{
  "name": "code.generate",
  "description": "Generate code structurally: types, methods, tests, constructors.",
  "inputSchema": {
    "properties": {
      "operation": { "type": "string", "enum": ["create_type", "method", "property", "constructor", "tests", "implement_interface"] },
      "targetFile": { "type": "string", "description": "File to modify/create" },
      "className": { "type": "string" },
      "methodName": { "type": "string" },
      "signature": { "type": "string", "description": "Method signature for method/constructor" },
      "body": { "type": "string", "description": "Method body" }
    },
    "required": ["operation"]
  }
}
```

### code.navigate

```json
{
  "name": "code.navigate",
  "description": "Find code relationships: usages, callers, implementations, definitions.",
  "inputSchema": {
    "properties": {
      "operation": { "type": "string", "enum": ["usages", "callers", "implementations", "derived_types", "definition", "references"] },
      "symbolName": { "type": "string", "description": "Symbol to find relationships for" },
      "filePath": { "type": "string", "description": "File containing the symbol (helps disambiguation)" },
      "limit": { "type": "integer", "default": 20, "description": "Max results to return" }
    },
    "required": ["operation", "symbolName"]
  }
}
```

### code.search

```json
{
  "name": "code.search",
  "description": "Semantic search across codebase. Better than grep for concepts.",
  "inputSchema": {
    "properties": {
      "query": { "type": "string", "description": "Natural language query or code pattern" },
      "filePattern": { "type": "string", "description": "Glob pattern to filter files" },
      "limit": { "type": "integer", "default": 10 }
    },
    "required": ["query"]
  }
}
```

## ValidationTracker Design

### State Management

```csharp
public class ValidationTracker
{
    private readonly HashSet<string> _modifiedFiles = new();
    private bool _lastValidationPassed = true;
    private int _consecutiveFailures = 0;
    
    public bool HasUnvalidatedChanges => _modifiedFiles.Count > 0;
    public int ConsecutiveFailures => _consecutiveFailures;
    
    // Called by file.write, file.modify tools
    public void TrackFileChange(string filePath)
    {
        if (IsCodeFile(filePath))
            _modifiedFiles.Add(filePath);
    }
    
    // Called by code.validate
    public void RecordValidationResult(bool success)
    {
        if (success)
        {
            _modifiedFiles.Clear();
            _consecutiveFailures = 0;
        }
        else
        {
            _consecutiveFailures++;
        }
        _lastValidationPassed = success;
    }
    
    private static bool IsCodeFile(string path) =>
        path.EndsWith(".cs") || path.EndsWith(".ts") || 
        path.EndsWith(".py") || path.EndsWith(".go") ||
        path.EndsWith(".js") || path.EndsWith(".tsx");
}
```

### Integration with ReActExecutor

```csharp
// In ExecuteAsync, when handling "finish":
if (parsed.Action.Equals("finish", StringComparison.OrdinalIgnoreCase))
{
    if (_validationTracker.HasUnvalidatedChanges)
    {
        if (_validationTracker.ConsecutiveFailures >= 5)
        {
            // Force fail after 5 attempts
            return new ReActResult
            {
                Success = false,
                Error = "Build validation failed after 5 attempts. Manual intervention required.",
                FinalAnswer = "Failed to produce compiling code.",
                Steps = steps
            };
        }
        
        observation = $"""
            ❌ Cannot finish: You have modified code files that haven't been validated.
            Modified files: {string.Join(", ", _validationTracker.ModifiedFiles)}
            
            Run code.validate to check for compilation errors, then fix any issues.
            """;
        
        steps.Add(new ReActStep { ... });
        continue; // Continue the loop
    }
    
    // Validation passed, allow finish
    return new ReActResult { Success = true, ... };
}
```

## Language Support Matrix

| Tool | C# | TypeScript | Python | Go | Fallback |
|------|----|------------|--------|-----|----------|
| `code.validate` | ✅ `dotnet build` | ✅ `tsc --noEmit` | ⚠️ `python -m py_compile` | ✅ `go build` | Shell script |
| `code.refactor` | ✅ Roslyn | ❌ Phase 2 | ⚠️ Rope (limited) | ❌ Phase 2 | ❌ Not available |
| `code.generate` | ✅ Roslyn | ❌ Phase 2 | ❌ Phase 2 | ❌ Phase 2 | Template-based |
| `code.navigate` | ✅ Roslyn | ❌ Phase 2 | ❌ Phase 2 | ❌ Phase 2 | Grep fallback |
| `code.search` | ✅ RAG | ✅ RAG | ✅ RAG | ✅ RAG | ✅ RAG |

**Phase 1 Focus:** C# (full support) + all languages (validate only)

## Error Recovery Strategy

### On Validation Failure

1. **First failure:** Inject errors as observation, agent tries to fix
2. **Failures 2-4:** Same, agent continues attempting
3. **Failure 5:** Force-fail the step with clear error message
4. **User intervention:** Reset step status via API, agent retries with fresh context

### On Tool Failure

- `code.refactor` fails → Fall back to `file.modify` with warning
- `code.navigate` fails → Fall back to `grep_search`
- All failures logged for debugging

## Test Strategy

### Unit Tests

1. `ValidationTrackerTests` - state management, file tracking
2. `CodeValidateToolTests` - language detection, error parsing
3. `CodeRefactorToolTests` - delegation to underlying services

### Integration Tests

1. **Happy path:** Agent makes change → validates → passes → finishes
2. **Fix loop:** Agent makes error → validates (fails) → fixes → validates (passes) → finishes
3. **Max retries:** Agent can't fix → hits limit → force-fails
4. **Multi-language:** Same workflow for TypeScript project

### Manual Smoke Test

Create a story: "Rename OrderService to PurchaseService"
- Agent should use `code.refactor` (not grep+modify)
- Agent should call `code.validate` before finishing
- Resulting code should compile

## Acceptance Criteria

- [ ] Coding agents have access to `code.refactor`, `code.generate`, `code.navigate`, `code.search`
- [ ] `code.validate` works for C#, TypeScript, Python, Go
- [ ] Agent cannot "finish" with unvalidated code changes
- [ ] Validation errors are injected as observations, loop continues
- [ ] Max 5 validation failures before force-fail
- [ ] Tools have clear schemas with descriptions
- [ ] Language detection works from project files
- [ ] Fallbacks exist when semantic tools unavailable
- [ ] All existing agent tests pass
- [ ] New integration test: agent fixes compilation error before finishing

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Roslyn tools only work for C# | Language dispatch with fallbacks |
| Increased token usage from validation | Only validate on finish attempt |
| Agent confusion with too many tools | Clear tool descriptions, examples |
| Breaking existing workflows | Feature flag, gradual rollout |

## Success Metrics

- **Before**: 40% of coding steps produce compiling code
- **After**: 95%+ of coding steps produce compiling code
- **Bonus**: Agents use semantic refactoring instead of text manipulation

## Dependencies

- Existing MCP tool implementations (aura_refactor, etc.)
- Language-specific validators
- ReActExecutor modifications

## Open Questions

1. Should `code.validate` run tests too, or just compilation?
2. How to handle validation in non-compiled languages (Python, JS)?
3. Should we expose ALL MCP tools or a curated subset?
