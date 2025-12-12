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

**Status:** ✅ Completed 2025-12-12

Moved to [completed/code-graph-status-panel.md](../completed/code-graph-status-panel.md).

---

### 3. Code Graph Indexing from Background API

**Problem:** The `/api/index/background` endpoint only triggers RAG indexing, not Code Graph indexing. Users who call the API directly (not through the extension UI) don't get Code Graph.

**Solution:** After RAG indexing completes, check for `.sln` files and trigger Code Graph indexing automatically.

**Files to modify:**
- `src/Aura.Foundation/Rag/BackgroundIndexer.cs` - add Code Graph triggering after RAG completes
- OR: Create a unified indexing orchestrator that sequences both

**Implementation notes:**
- Look for `.sln` files in the indexed directory (recursive search)
- Queue Code Graph indexing as a second background job
- Log but don't fail if Code Graph indexing fails (it's optional)
- Avoid re-indexing if Code Graph is already up to date

---

### 4. Prompt for Missing Code Graph Index

**Problem:** If RAG is indexed but Code Graph is not, the user doesn't get a prompt to index Code Graph. The "codebase is indexed" check only looks at RAG.

**Solution:** Update `getDirectoryIndexStatus` or add a separate check for Code Graph, and prompt user to index if missing.

**Files to modify:**
- `extension/src/providers/workflowPanelProvider.ts` - `handleEnrich` method
- `extension/src/services/auraApiService.ts` - add `getCodeGraphIndexStatus()` or extend existing

**User experience:**
1. User clicks "Analyze" on a workflow
2. Check: RAG indexed? If not, prompt to index → runs RAG + Code Graph
3. Check: Code Graph indexed? If not (but RAG is), prompt: "Code Graph not indexed. Index now for better structural queries?"
4. User can proceed without Code Graph or index it

---

### 5. Code Graph Query UI

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

- [x] Status sidebar shows Code Graph health and stats (#2 - completed)
- [ ] Code graph indexing runs automatically when .sln is found anywhere in repo (#1)
- [ ] Background API triggers Code Graph indexing after RAG (#3)
- [ ] User prompted if Code Graph is missing when RAG is present (#4)
- [ ] Users can query code graph from VS Code command palette (#5)
- [ ] Query results are actionable (click to open file at line)
