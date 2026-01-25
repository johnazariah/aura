# Multi-Language Refactoring Support

**Status:** 📋 Planned  
**Priority:** Medium  
**Estimated Effort:** 2-3 weeks  
**Created:** 2026-01-15  
**Dependencies:** MCP Tools Enhancement (completed)

## Overview

Extend the Aura MCP refactoring tools to support additional languages beyond C# (Roslyn) and Python (rope). This builds on the consolidated `aura_refactor` meta-tool which already auto-detects language from file extension.

## Current State

| Language | Engine | Status |
|----------|--------|--------|
| C#, F#, VB.NET | Roslyn | ✅ Implemented |
| Python | rope | ✅ Implemented |
| TypeScript/JavaScript | ts-morph | ❌ Planned |
| Go | gopls (LSP) | ❌ Planned |
| Rust | rust-analyzer (LSP) | ❌ Planned |
| Java | jdtls (LSP) | ❌ Planned |
| Other | Generic LSP | ❌ Planned |

## Architecture

The `aura_refactor` tool already routes based on file extension. Adding new languages means:

1. Create a language-specific refactoring service (e.g., `ITypeScriptRefactoringService`)
2. Add file extension detection in `RefactorAsync` router
3. Implement the service using the appropriate engine

```
┌─────────────────────────────────────────────────────────────────┐
│                      aura_refactor                              │
├─────────────────────────────────────────────────────────────────┤
│  File Extension Detection                                       │
│  .cs, .fs, .vb  → Roslyn                                       │
│  .py            → rope                                          │
│  .ts, .tsx, .js → ts-morph (planned)                           │
│  .go            → gopls LSP (planned)                          │
│  .rs            → rust-analyzer LSP (planned)                  │
│  .java          → jdtls LSP (planned)                          │
│  other          → Generic LSP fallback (planned)               │
└─────────────────────────────────────────────────────────────────┘
```

## Phase 1: TypeScript/JavaScript (ts-morph)

**Engine:** `ts-morph` - TypeScript Compiler API wrapper

**Capabilities:**
- Rename symbol
- Extract method/function
- Extract variable
- Find references
- Find definition
- Change signature (add/remove parameters)

**Implementation:**
```typescript
// scripts/typescript/refactor.ts
import { Project, SourceFile } from "ts-morph";

async function renameSymbol(
  projectPath: string,
  filePath: string,
  offset: number,
  newName: string,
  preview: boolean
): Promise<RefactoringResult> {
  const project = new Project({ tsConfigFilePath: `${projectPath}/tsconfig.json` });
  // ... implementation
}
```

**Effort:** 3-4 days

## Phase 2: LSP Integration Framework

Create a generic LSP client that can communicate with any language server.

**Supported Operations:**
- `textDocument/rename` - Rename symbol
- `textDocument/references` - Find all references
- `textDocument/definition` - Go to definition
- `workspace/applyEdit` - Apply multi-file edits

**Implementation:**
```csharp
public interface ILspRefactoringService
{
    Task<RefactoringResult> RenameSymbolAsync(
        string filePath,
        int line,
        int column,
        string newName,
        bool preview,
        CancellationToken ct);
    
    Task<IReadOnlyList<Location>> FindReferencesAsync(
        string filePath,
        int line,
        int column,
        CancellationToken ct);
}
```

**Effort:** 4-5 days

## Phase 3: Language-Specific LSP Servers

| Language | Server | Package/Binary |
|----------|--------|----------------|
| Go | gopls | `go install golang.org/x/tools/gopls@latest` |
| Rust | rust-analyzer | `rustup component add rust-analyzer` |
| Java | jdtls | Eclipse JDT Language Server |

Each requires:
1. Auto-detection of installed server
2. Server lifecycle management (start/stop)
3. LSP protocol communication

**Effort:** 2-3 days per language

## Success Criteria

- [ ] `aura_refactor operation=rename filePath=app.ts` works via ts-morph
- [ ] `aura_refactor operation=rename filePath=main.go` works via gopls
- [ ] `aura_refactor operation=rename filePath=lib.rs` works via rust-analyzer
- [ ] Graceful fallback with clear error when language server not installed
- [ ] Tool descriptions updated to list supported languages

## What Tree-sitter Cannot Do

Tree-sitter is a parser, not a semantic analyzer:

| ✅ Tree-sitter Can | ❌ Tree-sitter Cannot |
|-------------------|----------------------|
| Find all method declarations | Know what type a variable has |
| Locate symbol by name in file | Find all references across files |
| Query syntax patterns | Determine if rename causes conflict |
| Fast incremental re-parse | Resolve imports/dependencies |

**Key insight:** Tree-sitter powers *discovery*, semantic engines power *editing*.

## References

- [ts-morph documentation](https://ts-morph.com/)
- [LSP Specification](https://microsoft.github.io/language-server-protocol/)
- [gopls](https://pkg.go.dev/golang.org/x/tools/gopls)
- [rust-analyzer](https://rust-analyzer.github.io/)
