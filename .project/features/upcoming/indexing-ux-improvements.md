# Indexing UX Improvements

**Status:** Proposed  
**Priority:** Medium  
**Created:** 2025-12-12

## Overview

Follow-up improvements to the unified indexing pipeline to enhance user experience.

## Features

### 1. Recursive Solution Discovery

**Problem:** `findSolutionPath` only looks in the repository root directory, missing solutions in subdirectories.

**Solution:** Search recursively for `.sln` files, then `.csproj` if no solution found.

**Files to modify:**
- `extension/src/providers/workflowPanelProvider.ts` - `findSolutionPath` method

**Implementation notes:**
- Use `glob` or recursive `fs.readdirSync` with depth limit
- Prefer `.sln` over `.csproj` (solution has full project context)
- If multiple `.sln` files found, prefer one at shallowest depth
- Consider caching result per workspace

---

### 2. Code Graph Status in Sidebar

**Problem:** The status sidebar shows RAG stats (symbols, embeddings) but not Code Graph stats.

**Solution:** Add a "Code Graph" section showing nodes, edges, types indexed.

**Files to modify:**
- `extension/src/services/healthCheckService.ts` - add `checkCodeGraph()` method
- `extension/src/providers/statusTreeProvider.ts` - add Code Graph tree item
- `src/Aura.Api/Program.cs` - add `/health/codegraph` endpoint if missing

**Display format:**
```
Code Graph
  ├── Nodes: 1,384
  ├── Edges: 2,490
  ├── Types: 183
  └── Projects: 12
```

---

### 3. Code Graph Query UI

**Problem:** Code graph queries (find implementations, callers, etc.) are only available via API.

**Solution:** Add query commands/UI to the extension.

**Proposed commands:**
- `Aura: Find Implementations` - prompts for interface name, shows results
- `Aura: Find Callers` - prompts for method name, shows call sites
- `Aura: Find Derived Types` - prompts for base class, shows hierarchy
- `Aura: Show Type Members` - prompts for type name, shows methods/properties

**Files to modify:**
- `extension/src/services/auraApiService.ts` - add query methods
- `extension/src/extension.ts` - register commands
- Consider: TreeView for results, or QuickPick for selection

**Future enhancement:** Right-click on symbol in editor → "Find Implementations with Aura"

---

## Success Criteria

- [ ] Code graph indexing runs automatically when .sln is found anywhere in repo
- [ ] Status sidebar shows Code Graph health and stats
- [ ] Users can query code graph from VS Code command palette
- [ ] Query results are actionable (click to open file at line)
