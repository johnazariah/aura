# TypeScript Refactoring Support

**Status:** ✅ Complete
**Completed:** 2026-01-30
**Priority:** High
**Estimated Effort:** 1 week (Phase 1: 3 days, Phase 2: 2 days)
**Created:** 2026-01-30
**Dependencies:** MCP Tools Enhancement (completed)

## Overview

Extend `aura_refactor` and `aura_navigate` to support TypeScript/JavaScript via ts-morph. This enables Aura to help with its own VS Code extension and any TypeScript/JavaScript projects.

## Motivation

- **Dogfooding**: Aura's extension is TypeScript - we can't refactor our own code
- **Market fit**: TypeScript is one of the most popular languages
- **Consistency**: Users expect the same capabilities across languages

## Current State

| Language | Engine | aura_refactor | aura_navigate | aura_generate |
|----------|--------|---------------|---------------|---------------|
| C#, F#, VB.NET | Roslyn | ✅ | ✅ | ✅ |
| Python | rope | ✅ | ✅ | ❌ |
| TypeScript/JS | ts-morph | ✅ | ✅ | ❌ |

## Phased Delivery

### Phase 1: Core Operations (3 days)

**aura_refactor:**
- `rename` - Rename symbol across all files in project

**aura_navigate:**
- `references` - Find all references to symbol
- `definition` - Go to definition

**Deliverables:**
1. `scripts/typescript/refactor.ts` - ts-morph script
2. `TypeScriptRefactoringBridge.cs` - C# process bridge
3. MCP handler wiring for `.ts/.tsx/.js/.jsx` detection
4. Tests on Aura's own extension code

### Phase 2: Extract Operations (2 days)

**aura_refactor:**
- `extract_method` - Extract selection to function
- `extract_variable` - Extract expression to const/let

**Deliverables:**
1. Extend refactor.ts with extract operations
2. Handle selection ranges (startOffset, endOffset)
3. Additional test coverage

### Out of Scope
- `aura_generate` for TypeScript (separate feature if needed)
- React/Vue/Angular-specific refactorings
- JSX/TSX component extraction
- Change signature (complex for JS due to dynamic typing)

## Tool-by-Tool TypeScript Support Justification

| Tool | TS Support | Justification |
|------|------------|---------------|
| `aura_search` | ✅ Already works | Language-agnostic semantic search via embeddings. Tree-sitter indexes TS. |
| `aura_tree` | ✅ Already works | Tree-sitter parses TS/JS. Returns file/symbol structure. |
| `aura_navigate` | 🎯 **This feature** | Requires semantic analysis (find references, go to definition). ts-morph provides this. |
| `aura_refactor` | 🎯 **This feature** | Requires semantic analysis (rename across files, extract). ts-morph provides this. |
| `aura_inspect` | ❌ Not planned | Lists type members/types in a project. Useful for C# (large class hierarchies), less valuable for TS (use `aura_tree`). |
| `aura_generate` | ❌ Not planned | C#-specific: constructor DI, interface implementation, test scaffolding. TS has different patterns (no interfaces at runtime, no DI conventions). |
| `aura_validate` | ❌ Not planned | TS validation = `npx tsc --noEmit`. Trivial to run manually; agents already use terminal for this. |
| `aura_edit` | ✅ Already works | Language-agnostic line-based editing. Works on any file type. |
| `aura_docs` | ✅ Already works | Documentation search. Language-agnostic. |
| `aura_pattern` | ✅ Already works | Loads operational patterns. Language-agnostic. |
| `aura_workflow` | ✅ Already works | Story management. Language-agnostic. |
| `aura_workspace` | ✅ Already works | Workspace registry. Language-agnostic. |
| `aura_architect` | ❌ Placeholder | Not implemented for any language yet. |

### Why Not `aura_inspect` for TypeScript?

`aura_inspect` operations:
- `type_members` - List members of a class/interface
- `list_types` - List all types in a project

**For C#**: Valuable because Roslyn gives us rich metadata (interfaces, inheritance, attributes).

**For TypeScript**: Less valuable because:
1. `aura_tree` with `maxDepth=3` already shows file→class→members
2. TypeScript's structural typing means "list all types" is less meaningful
3. TS files are typically smaller/simpler than C# files

**Verdict**: Use `aura_tree` for TypeScript structure exploration.

### Why Not `aura_generate` for TypeScript?

`aura_generate` operations for C#:
- `create_type` - Create class/record/interface with namespace
- `implement_interface` - Generate method stubs
- `constructor` - Generate DI-style constructor
- `property` / `method` - Add members with proper formatting
- `tests` - Generate xUnit test scaffolding

**Why C#-specific?**
1. **Namespace management** - C# requires explicit namespaces; TS uses ES modules (just `export`)
2. **Interface implementation** - C# interfaces have runtime meaning; TS interfaces are erased
3. **Constructor patterns** - C# DI injection is convention-based; TS has no standard
4. **Test scaffolding** - xUnit/NSubstitute patterns don't translate to Jest/Vitest

**For TypeScript**: LLMs are good at generating TS code directly. The structured generation that `aura_generate` provides for C# (correct namespaces, usings, formatting) is less necessary for TS.

**Verdict**: Not worth the implementation effort. LLM + `aura_edit` handles TS generation well.

### Why Not `aura_validate` for TypeScript?

**For C#**: `aura_validate compilation` runs `dotnet build` and parses errors into structured output.

**For TypeScript**: 
- `npx tsc --noEmit` is the equivalent
- Agents already run this via terminal when needed
- Error output is simpler than MSBuild (line:column format)

**Verdict**: Terminal execution is sufficient. No dedicated tool needed.

## Technical Design

### Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  aura_refactor / aura_navigate                              │
│  (existing MCP handlers in McpHandler.cs)                   │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼ file extension detection
┌─────────────────────────────────────────────────────────────┐
│  .ts/.tsx/.js/.jsx → TypeScriptRefactoringBridge           │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼ spawns Node.js process
┌─────────────────────────────────────────────────────────────┐
│  scripts/typescript/refactor.ts (ts-morph)                  │
└─────────────────────────────────────────────────────────────┘
```

### Project Detection

1. Find nearest `tsconfig.json` walking up from file
2. If no tsconfig, create in-memory project with default settings
3. For JS-only projects, use `allowJs: true` in default config

```typescript
function findProject(filePath: string): Project {
  const tsConfigPath = findNearestTsConfig(filePath);
  
  if (tsConfigPath) {
    return new Project({ tsConfigFilePath: tsConfigPath });
  }
  
  // Fallback for JS-only or no tsconfig
  return new Project({
    compilerOptions: {
      allowJs: true,
      checkJs: false,
      target: ScriptTarget.ESNext,
      module: ModuleKind.ESNext,
    },
  });
}
```

### C# Bridge

```csharp
// src/Aura.Foundation/Refactoring/TypeScriptRefactoringBridge.cs
public sealed class TypeScriptRefactoringBridge : ITypeScriptRefactoringService
{
    private readonly ILogger<TypeScriptRefactoringBridge> _logger;
    private readonly string _scriptPath;
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);
    
    public async Task<RefactoringResult> RenameSymbolAsync(
        string filePath,
        int offset,
        string newName,
        bool preview,
        CancellationToken ct)
    {
        var request = new
        {
            operation = "rename",
            filePath = Path.GetFullPath(filePath),
            offset,
            newName,
            preview
        };
        
        return await ExecuteAsync<RefactoringResult>(request, ct);
    }
    
    public async Task<NavigationResult> FindReferencesAsync(
        string filePath,
        int offset,
        CancellationToken ct)
    {
        var request = new
        {
            operation = "references",
            filePath = Path.GetFullPath(filePath),
            offset
        };
        
        return await ExecuteAsync<NavigationResult>(request, ct);
    }
    
    private async Task<T> ExecuteAsync<T>(object request, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(request);
        var escapedJson = json.Replace("\"", "\\\"");
        
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "node",
                Arguments = $"\"{_scriptPath}\" \"{escapedJson}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
        
        process.Start();
        
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_timeout);
        
        await process.WaitForExitAsync(cts.Token);
        
        var output = await outputTask;
        var error = await errorTask;
        
        if (process.ExitCode != 0)
        {
            _logger.LogError("TypeScript refactor failed: {Error}", error);
            throw new RefactoringException($"TypeScript operation failed: {error}");
        }
        
        return JsonSerializer.Deserialize<T>(output)!;
    }
}
```

### TypeScript Script

```typescript
// scripts/typescript/refactor.ts
import { Project, Node, SyntaxKind, SourceFile, ts } from "ts-morph";
import * as path from "path";
import * as fs from "fs";

// ============================================================================
// Types
// ============================================================================

interface BaseRequest {
  operation: string;
  filePath: string;
  offset: number;
}

interface RenameRequest extends BaseRequest {
  operation: "rename";
  newName: string;
  preview: boolean;
}

interface ReferencesRequest extends BaseRequest {
  operation: "references";
}

interface DefinitionRequest extends BaseRequest {
  operation: "definition";
}

interface ExtractMethodRequest extends BaseRequest {
  operation: "extract_method";
  endOffset: number;
  newName: string;
  preview: boolean;
}

type Request = RenameRequest | ReferencesRequest | DefinitionRequest | ExtractMethodRequest;

interface FileChange {
  filePath: string;
  startLine: number;
  endLine: number;
  oldText: string;
  newText: string;
}

interface RefactoringResult {
  success: boolean;
  changes?: FileChange[];
  error?: string;
}

interface Location {
  filePath: string;
  line: number;
  column: number;
  text: string;
}

interface NavigationResult {
  success: boolean;
  locations?: Location[];
  error?: string;
}

// ============================================================================
// Project Loading
// ============================================================================

function findNearestTsConfig(filePath: string): string | null {
  let dir = path.dirname(filePath);
  
  while (dir !== path.dirname(dir)) {
    const tsConfigPath = path.join(dir, "tsconfig.json");
    if (fs.existsSync(tsConfigPath)) {
      return tsConfigPath;
    }
    dir = path.dirname(dir);
  }
  
  return null;
}

function loadProject(filePath: string): Project {
  const tsConfigPath = findNearestTsConfig(filePath);
  
  if (tsConfigPath) {
    return new Project({ tsConfigFilePath: tsConfigPath });
  }
  
  // Fallback for JS-only projects
  return new Project({
    compilerOptions: {
      allowJs: true,
      checkJs: false,
      target: ts.ScriptTarget.ESNext,
      module: ts.ModuleKind.ESNext,
      moduleResolution: ts.ModuleResolutionKind.NodeNext,
    },
  });
}

// ============================================================================
// Operations
// ============================================================================

function rename(req: RenameRequest): RefactoringResult {
  try {
    const project = loadProject(req.filePath);
    const sourceFile = project.addSourceFileAtPath(req.filePath);
    const node = sourceFile.getDescendantAtPos(req.offset);
    
    if (!node) {
      return { success: false, error: `No symbol found at offset ${req.offset}` };
    }
    
    // Find the renameable node (identifier, or its parent if it's a declaration)
    const identifier = findRenameableNode(node);
    if (!identifier) {
      return { success: false, error: "Cannot rename this symbol" };
    }
    
    if (req.preview) {
      // Return affected locations without modifying
      const refs = identifier.findReferencesAsNodes();
      const changes: FileChange[] = refs.map(ref => ({
        filePath: ref.getSourceFile().getFilePath(),
        startLine: ref.getStartLineNumber(),
        endLine: ref.getEndLineNumber(),
        oldText: ref.getText(),
        newText: req.newName,
      }));
      return { success: true, changes };
    }
    
    // Perform rename
    identifier.rename(req.newName);
    project.saveSync();
    
    return { success: true };
  } catch (err) {
    return { success: false, error: String(err) };
  }
}

function findReferences(req: ReferencesRequest): NavigationResult {
  try {
    const project = loadProject(req.filePath);
    const sourceFile = project.addSourceFileAtPath(req.filePath);
    const node = sourceFile.getDescendantAtPos(req.offset);
    
    if (!node) {
      return { success: false, error: `No symbol found at offset ${req.offset}` };
    }
    
    const identifier = findRenameableNode(node);
    if (!identifier) {
      return { success: false, error: "Cannot find references for this symbol" };
    }
    
    const refs = identifier.findReferencesAsNodes();
    const locations: Location[] = refs.map(ref => ({
      filePath: ref.getSourceFile().getFilePath(),
      line: ref.getStartLineNumber(),
      column: ref.getStartLinePos() - ref.getSourceFile().getLineStarts()[ref.getStartLineNumber() - 1] + 1,
      text: ref.getSourceFile().getFullText().substring(
        ref.getSourceFile().getLineStarts()[ref.getStartLineNumber() - 1],
        ref.getSourceFile().getLineStarts()[ref.getStartLineNumber()] || ref.getSourceFile().getFullText().length
      ).trim(),
    }));
    
    return { success: true, locations };
  } catch (err) {
    return { success: false, error: String(err) };
  }
}

function findDefinition(req: DefinitionRequest): NavigationResult {
  try {
    const project = loadProject(req.filePath);
    const sourceFile = project.addSourceFileAtPath(req.filePath);
    const node = sourceFile.getDescendantAtPos(req.offset);
    
    if (!node) {
      return { success: false, error: `No symbol found at offset ${req.offset}` };
    }
    
    const identifier = findRenameableNode(node);
    if (!identifier) {
      return { success: false, error: "Cannot find definition for this symbol" };
    }
    
    const definitions = identifier.getDefinitionNodes();
    const locations: Location[] = definitions.map(def => ({
      filePath: def.getSourceFile().getFilePath(),
      line: def.getStartLineNumber(),
      column: 1,
      text: def.getText().substring(0, 100) + (def.getText().length > 100 ? "..." : ""),
    }));
    
    return { success: true, locations };
  } catch (err) {
    return { success: false, error: String(err) };
  }
}

function findRenameableNode(node: Node): Node | null {
  // If it's an identifier, use it directly
  if (Node.isIdentifier(node)) {
    return node;
  }
  
  // If it's a declaration, find its name identifier
  if (Node.isFunctionDeclaration(node) || 
      Node.isClassDeclaration(node) ||
      Node.isVariableDeclaration(node) ||
      Node.isMethodDeclaration(node) ||
      Node.isPropertyDeclaration(node)) {
    return node.getNameNode() ?? null;
  }
  
  // Try parent
  const parent = node.getParent();
  if (parent && Node.isIdentifier(parent)) {
    return parent;
  }
  
  return null;
}

// ============================================================================
// Main
// ============================================================================

function main() {
  const args = process.argv[2];
  if (!args) {
    console.log(JSON.stringify({ success: false, error: "No arguments provided" }));
    process.exit(1);
  }
  
  let request: Request;
  try {
    request = JSON.parse(args);
  } catch {
    console.log(JSON.stringify({ success: false, error: "Invalid JSON arguments" }));
    process.exit(1);
  }
  
  let result: RefactoringResult | NavigationResult;
  
  switch (request.operation) {
    case "rename":
      result = rename(request);
      break;
    case "references":
      result = findReferences(request);
      break;
    case "definition":
      result = findDefinition(request);
      break;
    default:
      result = { success: false, error: `Unknown operation: ${request.operation}` };
  }
  
  console.log(JSON.stringify(result));
}

main();
```

### MCP Handler Wiring

```csharp
// In McpHandler.cs - RefactorAsync method

private static string? DetectLanguage(string filePath) => 
    Path.GetExtension(filePath).ToLowerInvariant() switch
    {
        ".cs" or ".fs" or ".vb" => "csharp",
        ".py" => "python",
        ".ts" or ".tsx" or ".js" or ".jsx" => "typescript",
        _ => null
    };

// In the operation switch:
case "typescript":
    return operation switch
    {
        "rename" => await _typescriptBridge.RenameSymbolAsync(
            filePath, offset, newName, preview, ct),
        "references" => await _typescriptBridge.FindReferencesAsync(
            filePath, offset, ct),
        "definition" => await _typescriptBridge.FindDefinitionAsync(
            filePath, offset, ct),
        _ => new { success = false, error = $"Operation '{operation}' not supported for TypeScript" }
    };
```

## Implementation Tasks

### Phase 1 (Days 1-3)

| Day | Task | Deliverable |
|-----|------|-------------|
| 1 | TypeScript script setup | `scripts/typescript/refactor.ts` with rename |
| 1 | Package.json for ts-morph | `scripts/typescript/package.json` |
| 2 | C# bridge | `TypeScriptRefactoringBridge.cs` |
| 2 | MCP handler wiring | Updated `McpHandler.cs` |
| 3 | Testing | Tests on Aura extension code |
| 3 | Error handling | Graceful failures, clear messages |

### Phase 2 (Days 4-5)

| Day | Task | Deliverable |
|-----|------|-------------|
| 4 | Extract method | Add to refactor.ts |
| 4 | Extract variable | Add to refactor.ts |
| 5 | Selection handling | startOffset/endOffset in bridge |
| 5 | Additional tests | Edge cases for extracts |

## Dependencies

**Runtime:**
- Node.js 18+ (already required for extension development)
- ts-morph npm package (installed in scripts/typescript)

**Install:**
```bash
cd scripts/typescript
npm install ts-morph
npx tsc  # Compile to JS
```

## Success Criteria

### Phase 1
- [x] `aura_refactor operation=rename filePath=extension/src/providers/chatProvider.ts offset=1234 newName=newFunction` works
- [x] `aura_navigate operation=references filePath=extension/src/foo.ts offset=1234` returns all refs
- [x] `aura_navigate operation=definition filePath=extension/src/foo.ts offset=1234` jumps to def
- [x] Can rename a function in Aura's own extension code
- [x] Graceful error when Node.js not installed
- [x] Graceful error when file not in TS project

### Phase 2
- [x] `aura_refactor operation=extract_method` extracts selection to function
- [x] `aura_refactor operation=extract_variable` extracts expression to const
- [x] Works on multi-line selections

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Node.js not installed | Clear error: "Node.js required for TypeScript refactoring" |
| Large projects slow | ts-morph uses TS compiler; accept ~2-3s for large projects |
| No tsconfig.json | Fallback to in-memory project with sensible defaults |
| JSX edge cases | Test on React components; may limit scope if problematic |
| Process spawn overhead | ~200ms startup; acceptable for refactoring operations |

## Testing Strategy

1. **Dogfood on Aura extension** - Real project, real complexity
2. **Unit test fixtures** - Small TS files for edge cases
3. **Error cases** - Invalid offset, readonly file, no symbol

```typescript
// tests/typescript-refactoring/fixtures/simple.ts
export function greet(name: string): string {
  return `Hello, ${name}!`;
}

export function useGreet() {
  const result = greet("world");  // Reference to greet
  console.log(result);
}
```

Test: Rename `greet` → `sayHello`, verify both occurrences updated.

## Future Enhancements (Out of Scope)

- [ ] `change_signature` - Add/remove parameters
- [ ] `inline_variable` - Inline a const
- [ ] `move_to_file` - Move function/class to new file
- [ ] Monorepo support - Multiple tsconfigs
- [ ] Watch mode - Keep project loaded for faster subsequent operations

