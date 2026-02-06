# Polyglot MCP Tools — TypeScript Parity

**Status:** 📋 Proposed
**Created:** 2026-02-06
**Priority:** High
**Estimated Effort:** 2 weeks (4 phases)
**Dependencies:** TypeScript Refactoring (completed 2026-01-30)

## Overview

Extend the Aura MCP tools so they work effectively across all supported languages — not just C#. Today, `aura_refactor` and `aura_navigate` route to TypeScript/Python based on file extension, but `aura_inspect`, `aura_generate`, and `aura_validate` are hardcoded to C#/Roslyn. The copilot-instructions tell agents to use Aura tools, but when editing TypeScript (e.g., Aura's own VS Code extension), every tool call fails or returns C#-only results.

**Goal:** An agent working in a TypeScript project can use `aura_inspect`, `aura_validate`, and core `aura_navigate` operations (callers, usages) with the same UX as C#.

## Motivation

1. **Dogfooding** — Aura's extension is TypeScript. We tell agents to use `aura_inspect` and `aura_validate` but they don't work on `.ts` files.
2. **copilot-instructions gap** — The instructions say "prefer `aura_generate` over `replace_string_in_file`" but `aura_generate` requires `solutionPath` (a `.sln` file). This is wrong for TS/Python projects.
3. **Agent quality** — During story execution, agents waste tokens calling tools that return C# errors when working on TypeScript code.
4. **Market** — TypeScript is the #2 language on GitHub. Users with TS projects get a degraded experience.

## Current State (as of 2026-02-06)

### What Already Works

| Tool | Polyglot? | Notes |
|------|-----------|-------|
| `aura_search` | ✅ | Language-agnostic (RAG/pgvector). Tree-sitter indexes TS. |
| `aura_tree` | ✅ | Language-agnostic (RAG chunks from tree-sitter). |
| `aura_edit` | ✅ | Language-agnostic (line-based text ops). |
| `aura_docs` | ✅ | Language-agnostic (documentation search). |
| `aura_pattern` | ✅ | Language-agnostic. Already has `typescript` overlay support. |
| `aura_workflow` | ✅ | Language-agnostic (story management). |
| `aura_workspace` | ✅ | Language-agnostic (workspace registry). |
| `aura_architect` | N/A | Placeholder — not implemented for any language. |

### What Partially Works

| Tool | Operation | C# | Python | TypeScript |
|------|-----------|-----|--------|------------|
| `aura_refactor` | `rename` | ✅ Roslyn | ✅ rope | ✅ ts-morph |
| `aura_refactor` | `extract_method` | ❌ (not impl) | ✅ rope | ✅ ts-morph |
| `aura_refactor` | `extract_variable` | ❌ (not impl) | ✅ rope | ✅ ts-morph |
| `aura_refactor` | `change_signature` | ✅ Roslyn | ❌ | ❌ |
| `aura_refactor` | `extract_interface` | ✅ Roslyn | ❌ | ❌ |
| `aura_refactor` | `safe_delete` | ✅ Roslyn | ❌ | ❌ |
| `aura_refactor` | `move_type_to_file` | ✅ Roslyn | ❌ | ❌ |
| `aura_refactor` | `move_members_to_partial` | ✅ Roslyn | ❌ | ❌ |
| `aura_navigate` | `references` | ✅ code graph | ✅ rope | ✅ ts-morph |
| `aura_navigate` | `definition` | ✅ Roslyn | ✅ rope | ✅ ts-morph |
| `aura_navigate` | `callers` | ✅ code graph | ❌ | ❌ |
| `aura_navigate` | `usages` | ✅ Roslyn | ❌ | ❌ |
| `aura_navigate` | `implementations` | ✅ Roslyn | ❌ | ❌ |
| `aura_navigate` | `derived_types` | ✅ Roslyn | ❌ | ❌ |
| `aura_navigate` | `by_attribute` | ✅ code graph | ❌ | ❌ |
| `aura_navigate` | `extension_methods` | ✅ code graph | N/A | N/A |
| `aura_navigate` | `by_return_type` | ✅ code graph | ❌ | ❌ |

### What Doesn't Work At All

| Tool | Operations | Blocker |
|------|-----------|---------|
| `aura_inspect` | `type_members`, `list_types` | Hardcoded to Roslyn `INamedTypeSymbol` + code graph |
| `aura_generate` | All 6 operations | Requires `solutionPath` (.sln); uses `IRoslynRefactoringService` |
| `aura_validate` | `compilation` | Hardcoded to `dotnet build` via Roslyn |
| `aura_validate` | `tests` | Hardcoded to `dotnet test` |

## Design Decisions

### What We WILL Build

| Tool | Operation | TypeScript Implementation | Rationale |
|------|-----------|--------------------------|-----------|
| `aura_inspect` | `type_members` | ts-morph: list exports, class members, interface members | Agents need to explore TS types before editing |
| `aura_inspect` | `list_types` | ts-morph: list all exported types in a project | Agents need project-level type overview |
| `aura_navigate` | `callers` | ts-morph: find all call sites for a function/method | Critical for understanding impact of changes |
| `aura_navigate` | `usages` | ts-morph: alias for `references` (same as C# behavior) | Consistency — C# `usages` uses Roslyn find-all-references |
| `aura_navigate` | `implementations` | ts-morph: find classes implementing an interface | Useful for TS codebases with interfaces |
| `aura_validate` | `compilation` | Shell out to `npx tsc --noEmit` | Agents need structured type-check feedback |
| `aura_validate` | `tests` | Shell out to `npx vitest run` / `npx jest --json` | Agents need structured test results |

### What We Will NOT Build (and why)

| Tool | Operation | Why Not |
|------|-----------|---------|
| `aura_generate` | All | TypeScript code generation doesn't benefit from AST manipulation the way C# does. C#'s value comes from namespace management, `using` statements, Roslyn formatting, and interface stub generation. TS has no namespaces, no `using` statements, and interfaces are erased at runtime. LLM + `aura_edit` handles TS generation well. |
| `aura_refactor` | `change_signature` | JavaScript's dynamic typing makes this extremely fragile. Not worth the effort. |
| `aura_refactor` | `extract_interface` | TS interfaces have no runtime representation. Extract manually. |
| `aura_refactor` | `safe_delete` | Use `references` to check for usages, then delete manually. |
| `aura_refactor` | `move_type_to_file` | TS files are typically one-export-per-file already. |
| `aura_navigate` | `derived_types` | TS uses structural typing — "derived" is less meaningful. Use `implementations` for interface→class. |
| `aura_navigate` | `by_attribute` | TS decorators are uncommon outside Angular. Low priority. |
| `aura_navigate` | `extension_methods` | JS/TS has no extension methods. |
| `aura_navigate` | `by_return_type` | TS type inference makes explicit return types rare. Low value. |

### Schema Changes

Today, `aura_inspect` and `aura_validate` require `solutionPath` (a `.sln` file). This is wrong for non-C# projects. We need to support `projectPath` as an alternative.

**Before:**
```
aura_inspect(operation: "type_members", typeName: "McpHandler", solutionPath: "c:\work\aura\Aura.sln")
aura_validate(operation: "compilation", solutionPath: "c:\work\aura\Aura.sln")
```

**After:**
```
// C# — unchanged
aura_inspect(operation: "type_members", typeName: "McpHandler", solutionPath: "c:\work\aura\Aura.sln")

// TypeScript — uses projectPath (directory containing tsconfig.json)
aura_inspect(operation: "type_members", typeName: "McpHandler", projectPath: "c:\work\aura\extension")
aura_validate(operation: "compilation", projectPath: "c:\work\aura\extension")
```

**Language detection logic:**
1. If `solutionPath` is provided → C# (Roslyn)
2. If `projectPath` is provided → detect from directory:
   - Has `tsconfig.json` → TypeScript
   - Has `setup.py` / `pyproject.toml` / `requirements.txt` → Python
   - Else → error with guidance

**For `aura_validate` specifically:**
- `solutionPath` → `dotnet build` / `dotnet test`
- `projectPath` + TS detected → `npx tsc --noEmit` / `npx vitest run --reporter=json`
- `projectPath` + Python detected → `python -m py_compile` / `python -m pytest --json-report`

## Technical Design

### Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│  MCP Meta-Tools (aura_inspect, aura_navigate, aura_validate)   │
│  McpHandler.Inspect.cs / McpHandler.Navigate.cs / etc.         │
└──────────────────────────┬──────────────────────────────────────┘
                           │ language detection (solutionPath vs projectPath)
              ┌────────────┼────────────────────┐
              ▼            ▼                    ▼
     ┌──────────────┐ ┌──────────┐ ┌─────────────────────┐
     │ Roslyn (C#)  │ │ rope (Py)│ │ ts-morph (TS/JS)    │
     │ In-process   │ │ Process  │ │ Process (Node.js)   │
     └──────────────┘ └──────────┘ └─────────────────────┘
```

### Extending the TypeScript Script

The existing `scripts/typescript/src/refactor.ts` (549 lines) already supports 5 commands. We add 5 more:

| New Command | For Tool | What It Does |
|-------------|----------|--------------|
| `inspect-type` | `aura_inspect(type_members)` | List members of a class/interface/type alias |
| `list-types` | `aura_inspect(list_types)` | List all exported types in a project |
| `find-callers` | `aura_navigate(callers)` | Find all call sites of a function/method |
| `find-implementations` | `aura_navigate(implementations)` | Find classes implementing an interface |
| `check` | `aura_validate(compilation)` | Run `tsc --noEmit` and return structured errors |

**Note:** `aura_validate(tests)` does NOT use ts-morph. It shells out to the test runner directly from C#, same as `dotnet test` does for C#.

### New Interface Methods

Extend `ITypeScriptRefactoringService` (or create a new `ITypeScriptLanguageService`):

```csharp
// Option A: Extend existing interface
public interface ITypeScriptRefactoringService
{
    // ... existing 5 methods ...

    // New: Inspect
    Task<TypeScriptInspectResult> InspectTypeAsync(
        TypeScriptInspectTypeRequest request, CancellationToken ct = default);
    Task<TypeScriptListTypesResult> ListTypesAsync(
        TypeScriptListTypesRequest request, CancellationToken ct = default);

    // New: Navigate
    Task<TypeScriptFindReferencesResult> FindCallersAsync(
        TypeScriptFindCallersRequest request, CancellationToken ct = default);
    Task<TypeScriptFindReferencesResult> FindImplementationsAsync(
        TypeScriptFindImplementationsRequest request, CancellationToken ct = default);

    // New: Validate
    Task<TypeScriptCheckResult> CheckAsync(
        TypeScriptCheckRequest request, CancellationToken ct = default);
}
```

**Option B (preferred): Rename to `ITypeScriptLanguageService`** since it's no longer just refactoring. This is a breaking rename but it's internal — only `McpHandler` and tests reference it.

### Request/Response Types

```csharp
// Inspect
public record TypeScriptInspectTypeRequest
{
    public required string ProjectPath { get; init; }
    public required string TypeName { get; init; }
    public string? FilePath { get; init; }  // Optional: narrow search to a file
}

public record TypeScriptInspectResult
{
    public required bool Success { get; init; }
    public string? Error { get; init; }
    public string? TypeName { get; init; }
    public string? Kind { get; init; }  // class, interface, type, enum
    public string? FilePath { get; init; }
    public int? Line { get; init; }
    public IReadOnlyList<TypeScriptMemberInfo>? Members { get; init; }
}

public record TypeScriptMemberInfo
{
    public required string Name { get; init; }
    public required string Kind { get; init; }  // method, property, getter, setter, constructor
    public string? Type { get; init; }           // Return type or property type
    public string? Visibility { get; init; }     // public, private, protected
    public bool IsStatic { get; init; }
    public bool IsAsync { get; init; }
    public int Line { get; init; }
}

// List types
public record TypeScriptListTypesRequest
{
    public required string ProjectPath { get; init; }
    public string? FilePattern { get; init; }    // e.g., "src/**/*.ts"
    public string? NameFilter { get; init; }     // Partial match on type name
}

public record TypeScriptListTypesResult
{
    public required bool Success { get; init; }
    public string? Error { get; init; }
    public IReadOnlyList<TypeScriptTypeInfo>? Types { get; init; }
    public int Count { get; init; }
}

public record TypeScriptTypeInfo
{
    public required string Name { get; init; }
    public required string Kind { get; init; }
    public required string FilePath { get; init; }
    public int Line { get; init; }
    public bool IsExported { get; init; }
    public int MemberCount { get; init; }
}

// Find callers
public record TypeScriptFindCallersRequest
{
    public required string ProjectPath { get; init; }
    public required string FilePath { get; init; }
    public required int Offset { get; init; }
}

// Find implementations
public record TypeScriptFindImplementationsRequest
{
    public required string ProjectPath { get; init; }
    public required string FilePath { get; init; }
    public required int Offset { get; init; }  // Offset of the interface name
}

// Validate
public record TypeScriptCheckRequest
{
    public required string ProjectPath { get; init; }
    public string? FilePath { get; init; }  // Optional: check single file
}

public record TypeScriptCheckResult
{
    public required bool Success { get; init; }
    public string? Error { get; init; }
    public bool CompilationSucceeded { get; init; }
    public int ErrorCount { get; init; }
    public int WarningCount { get; init; }
    public IReadOnlyList<TypeScriptDiagnostic>? Diagnostics { get; init; }
}

public record TypeScriptDiagnostic
{
    public required string FilePath { get; init; }
    public required int Line { get; init; }
    public required int Column { get; init; }
    public required string Severity { get; init; }  // error, warning
    public required string Code { get; init; }       // e.g., "TS2345"
    public required string Message { get; init; }
}
```

### Test Runner for `aura_validate(tests)`

This does NOT use the ts-morph script. Instead, `McpHandler.Validate.cs` detects the project type and shells out:

```
if projectPath has tsconfig.json:
    if package.json has vitest → npx vitest run --reporter=json
    if package.json has jest → npx jest --json
    else → error "No test runner detected"
```

The C# side parses the JSON output into a structured response matching the existing `aura_validate(tests)` response shape.

### McpHandler Routing Changes

#### `McpHandler.Inspect.cs`

```csharp
private async Task<object> InspectAsync(JsonElement? args, CancellationToken ct)
{
    var operation = args?.GetProperty("operation").GetString()
        ?? throw new ArgumentException("operation is required");

    // Detect language from provided path parameters
    var language = DetectLanguageFromArgs(args);

    return (operation, language) switch
    {
        ("type_members", "typescript") => await TypeScriptInspectTypeAsync(args, ct),
        ("list_types", "typescript") => await TypeScriptListTypesAsync(args, ct),
        ("type_members", _) => await GetTypeMembersAsync(args, ct),
        ("list_types", _) => await ListClassesFromInspect(args, ct),
        _ => throw new ArgumentException($"Unknown inspect operation: {operation}")
    };
}
```

#### `McpHandler.Navigate.cs`

The `callers` and `implementations` operations currently only work via code graph (C#). Add TS routing:

```csharp
private async Task<object> FindCallersAsync(JsonElement? args, CancellationToken ct)
{
    // If filePath is a TS file, route to ts-morph
    if (IsTypeScriptFile(args))
    {
        return await TypeScriptFindCallersAsync(args, ct);
    }

    // Existing C# code graph logic...
}
```

#### `McpHandler.Validate.cs`

```csharp
private async Task<object> ValidateCompilationAsync(JsonElement? args, CancellationToken ct)
{
    // If projectPath provided (and no solutionPath), detect language
    if (HasProjectPath(args) && !HasSolutionPath(args))
    {
        var language = DetectLanguageFromProjectPath(args);
        if (language == "typescript")
        {
            return await TypeScriptCheckAsync(args, ct);
        }
    }

    // Existing Roslyn logic...
}
```

### Language Detection Helper

Add a shared helper to `McpHandler`:

```csharp
/// <summary>
/// Detects language from MCP tool arguments.
/// Priority: filePath extension > solutionPath (C#) > projectPath (detect from directory).
/// </summary>
private static string DetectLanguageFromArgs(JsonElement? args)
{
    if (!args.HasValue) return "csharp";

    // 1. Check filePath extension
    if (args.Value.TryGetProperty("filePath", out var fpEl))
    {
        var fp = fpEl.GetString() ?? "";
        if (fp.EndsWith(".ts", OrdinalIgnoreCase) || fp.EndsWith(".tsx", OrdinalIgnoreCase) ||
            fp.EndsWith(".js", OrdinalIgnoreCase) || fp.EndsWith(".jsx", OrdinalIgnoreCase))
            return "typescript";
        if (fp.EndsWith(".py", OrdinalIgnoreCase))
            return "python";
    }

    // 2. solutionPath → always C#
    if (args.Value.TryGetProperty("solutionPath", out _))
        return "csharp";

    // 3. projectPath → detect from directory contents
    if (args.Value.TryGetProperty("projectPath", out var ppEl))
    {
        var pp = ppEl.GetString() ?? "";
        if (File.Exists(Path.Combine(pp, "tsconfig.json"))) return "typescript";
        if (File.Exists(Path.Combine(pp, "package.json"))) return "typescript";
        if (File.Exists(Path.Combine(pp, "pyproject.toml"))) return "python";
        if (File.Exists(Path.Combine(pp, "setup.py"))) return "python";
        if (File.Exists(Path.Combine(pp, "requirements.txt"))) return "python";
    }

    return "csharp"; // default
}
```

### Schema Updates

Update the MCP tool schema definitions in `McpHandler.cs` `HandleToolsList`:

1. **`aura_inspect`** — Add `projectPath` parameter; update description to mention TypeScript support.
2. **`aura_navigate`** — Update description to mention TypeScript `callers` and `implementations` support.
3. **`aura_validate`** — Add `projectPath` parameter; update description to mention TypeScript compilation and test support. Remove `solutionPath` from `required` (make it optional, since TS uses `projectPath`).
4. **`aura_generate`** — Remove `solutionPath` from `required` array (it's only needed for C#; TS doesn't use generate but the schema shouldn't force it).

### copilot-instructions.md Updates

Update the tool selection table and Aura tool capabilities table to reflect polyglot support. Update the "Path Resolution" section to include `projectPath` for TypeScript projects.

## Phased Delivery

### Phase 1: Language Detection & Schema (1 day)

**Goal:** Unified language detection + schema updates so tools accept `projectPath` for non-C# projects.

**Deliverables:**
1. `DetectLanguageFromArgs()` helper in McpHandler
2. Extract duplicated `.ts/.tsx/.js/.jsx` checks from Navigate/Refactor to use the helper
3. Update `aura_inspect` schema: add `projectPath` parameter
4. Update `aura_validate` schema: add `projectPath`, remove `solutionPath` from `required`
5. Update `aura_generate` schema: remove `solutionPath` from `required`
6. Graceful error messages when TS operation is called but not yet implemented

**Tests:**
- Unit tests for `DetectLanguageFromArgs` with all file extensions and directory combos
- Schema validation tests

### Phase 2: Inspect for TypeScript (2 days)

**Goal:** `aura_inspect(type_members)` and `aura_inspect(list_types)` work for TypeScript projects.

**Deliverables:**
1. Add `inspect-type` and `list-types` commands to `scripts/typescript/src/refactor.ts`
2. Add `InspectTypeAsync` and `ListTypesAsync` to `ITypeScriptRefactoringService`
3. Add request/response DTOs
4. Add routing in `McpHandler.Inspect.cs`
5. Add `TypeScriptInspectTypeAsync` and `TypeScriptListTypesAsync` to `McpHandler.Languages.cs`

**Tests:**
- Unit tests for new C# service methods (mocked process runner)
- Integration test: inspect Aura's own extension types
- McpHandler routing tests (`.ts` file → TS path, `.cs` file → Roslyn path)

### Phase 3: Navigate Callers & Implementations (2 days)

**Goal:** `aura_navigate(callers)` and `aura_navigate(implementations)` work for TypeScript.

**Deliverables:**
1. Add `find-callers` and `find-implementations` commands to `refactor.ts`
2. Add `FindCallersAsync` and `FindImplementationsAsync` to `ITypeScriptRefactoringService`
3. Add request/response DTOs
4. Add routing in `McpHandler.Navigate.cs`
5. Add handler methods in `McpHandler.Languages.cs`

**Tests:**
- Unit tests for service methods
- McpHandler routing tests
- Test on Aura extension code (find callers of a known function)

### Phase 4: Validate for TypeScript (2 days)

**Goal:** `aura_validate(compilation)` runs `tsc --noEmit`; `aura_validate(tests)` runs vitest/jest.

**Deliverables:**
1. Add `check` command to `refactor.ts` (runs `tsc --noEmit`, parses diagnostics)
2. Add `CheckAsync` to `ITypeScriptRefactoringService`
3. Add test runner detection and execution in `McpHandler.Validate.cs` (direct process spawn, no ts-morph)
4. Parse vitest/jest JSON output into structured response
5. Add routing in `McpHandler.Validate.cs`
6. Update copilot-instructions.md

**Tests:**
- Unit tests for compilation check (with mocked tsc output)
- Unit tests for test runner detection (vitest vs jest vs none)
- McpHandler routing tests

### Phase 5: Documentation & Polish (1 day)

**Goal:** Update all documentation, copilot-instructions, and run full test suite.

**Deliverables:**
1. Update `copilot-instructions.md` tool tables
2. Update `docs/mcp-tools/` documentation
3. Add TypeScript examples to tool descriptions
4. Update `.project/STATUS.md`
5. Run all 849+ tests, ensure green
6. Manual smoke test: use Aura tools on extension/ directory

## Interface Rename Consideration

The current interface is `ITypeScriptRefactoringService`, which was appropriate when it only did refactoring. With inspect, navigate, and validate operations, consider renaming to `ITypeScriptLanguageService`.

**Blast radius:** Small — only referenced by:
- `McpHandler.cs` (constructor injection)
- `McpHandler.Languages.cs` (method calls)
- `McpHandlerTypeScriptTests.cs` (mock)
- `TypeScriptRefactoringService.cs` (implementation)
- DI registration in `DeveloperModule.cs`

**Decision:** Do the rename in Phase 1 using `aura_refactor(rename)` before adding new methods. This keeps the interface name honest.

## File Change Inventory

| File | Change |
|------|--------|
| `scripts/typescript/src/refactor.ts` | Add 5 new commands (~200 lines) |
| `src/Aura.Module.Developer/Services/ITypeScriptRefactoringService.cs` | Rename to `ITypeScriptLanguageService`, add 5 methods, add DTOs |
| `src/Aura.Module.Developer/Services/TypeScriptRefactoringService.cs` | Rename to `TypeScriptLanguageService`, implement 5 new methods |
| `src/Aura.Module.Developer/DeveloperModule.cs` | Update DI registration |
| `src/Aura.Api/Mcp/McpHandler.cs` | Add `DetectLanguageFromArgs`, update tool schemas |
| `src/Aura.Api/Mcp/McpHandler.Inspect.cs` | Add TS routing + handler methods |
| `src/Aura.Api/Mcp/McpHandler.Navigate.cs` | Add TS routing for callers/implementations |
| `src/Aura.Api/Mcp/McpHandler.Validate.cs` | Add TS routing + test runner logic |
| `src/Aura.Api/Mcp/McpHandler.Languages.cs` | Add 5 new TS handler methods |
| `tests/Aura.Api.Tests/Mcp/McpHandlerTypeScriptTests.cs` | Add tests for new operations |
| `tests/Aura.Module.Developer.Tests/Services/TypeScriptRefactoringServiceTests.cs` | Add tests for new methods |
| `.github/copilot-instructions.md` | Update tool tables, path resolution |
| `prompts/mcp-tools-instructions.md` | Update tool descriptions if used |

## Success Criteria

1. `aura_inspect(operation: "type_members", projectPath: "c:\work\aura\extension", typeName: "AuraApiService")` returns the class members
2. `aura_inspect(operation: "list_types", projectPath: "c:\work\aura\extension")` returns all exported types
3. `aura_navigate(operation: "callers", filePath: "c:\work\aura\extension\src\services\auraApiService.ts", offset: <offset>, projectPath: "c:\work\aura\extension")` returns call sites
4. `aura_navigate(operation: "implementations", filePath: "...", offset: <offset>, projectPath: "c:\work\aura\extension")` returns implementing classes
5. `aura_validate(operation: "compilation", projectPath: "c:\work\aura\extension")` returns tsc diagnostics
6. `aura_validate(operation: "tests", projectPath: "c:\work\aura\extension")` runs vitest/jest and returns structured results
7. All existing C# and Python operations continue to work unchanged
8. copilot-instructions.md accurately reflects polyglot capabilities
9. All tests pass (existing + new)

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| ts-morph process spawn is slow (~1-2s per invocation) | Acceptable for now. Future: long-running Node.js server with JSON-RPC. |
| Node.js not installed on user machine | `ITypeScriptLanguageService` methods return clear error: "Node.js is required for TypeScript operations" |
| `refactor.ts` script not built | Check `dist/refactor.js` exists; return clear error: "Run `npm run build` in scripts/typescript/" |
| Test runner detection is fragile | Support vitest + jest only. Clear error for unknown runners. |
| Large TS projects may be slow | ts-morph loads full project. Mitigate with `--files` flag and file pattern filters. |

## Out of Scope

- Python `aura_inspect` / `aura_validate` (separate feature)
- `aura_generate` for TypeScript (LLM + `aura_edit` is sufficient)
- Long-running TypeScript language server (optimization for later)
- React/Vue/Angular-specific operations
- Decorator-based navigation (`by_attribute` for TS)
