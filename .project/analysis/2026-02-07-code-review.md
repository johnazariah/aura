# Code Review: Structural Analysis

> **Date:** 2026-02-07
> **Scope:** All C# source under `src/`
> **Purpose:** Identify long methods, god classes, duplication, and code smells

---

## Executive Summary

The codebase has grown organically through rapid feature delivery. While partial classes have been used to split large files, the underlying types still carry too many responsibilities. Four classes exceed 1,500 lines across their partials, and 13+ methods exceed 100 lines. Code duplication is systemic in the story-fetch pattern, JSON parsing, and language-handler wrappers. The MCP handler layer re-implements Roslyn queries that already exist in the service layer.

---

## 1. Largest Files (Top 20)

| # | File | Lines | Notes |
|---|------|------:|-------|
| 1 | `Module.Developer/Services/Testing/RoslynTestGenerator.cs` | 1,896 | Test generation logic |
| 2 | `Module.Developer/Services/StoryService.cs` | 1,749 | Core partial of StoryService |
| 3 | `Module.Developer/Ingestors/TreeSitterIngesterAgent.cs` | 1,525 | TreeSitter parsing |
| 4 | `Module.Developer/Services/RoslynRefactoringService.cs` | 1,495 | Core partial of refactoring |
| 5 | `Api/Mcp/McpHandler.cs` | 1,252 | Core partial of MCP handler |
| 6 | `Api/Endpoints/DeveloperEndpoints.cs` | 1,189 | HTTP endpoints |
| 7 | `Foundation/Tools/ReActExecutor.cs` | 1,040 | ReAct loop |
| 8 | `Foundation/Llm/OllamaProvider.cs` | 890 | Ollama integration |
| 9 | `Module.Developer/Tools/AuraToolWrappers.cs` | 839 | Tool wrappers |
| 10 | `Module.Developer/Services/CodeGraphIndexer.cs` | 804 | Graph indexing |
| 11 | `Foundation/Rag/RagService.cs` | 764 | RAG retrieval |
| 12 | `Module.Developer/Services/RoslynRefactoringService.Generate.cs` | 738 | Code generation partial |
| 13 | `Foundation/Rag/BackgroundIndexer.cs` | 699 | Background indexing |
| 14 | `Api/Mcp/McpHandler.Navigate.cs` | 657 | Navigation partial |
| 15 | `Module.Developer/Services/IRoslynRefactoringService.cs` | 643 | Interface (!) |
| 16 | `Foundation/Git/GitService.cs` | 634 | Git operations |
| 17 | `Module.Developer/Services/TypeScriptLanguageService.cs` | 631 | TypeScript tools |
| 18 | `Api/Mcp/McpHandler.Languages.cs` | 616 | Language partial |
| 19 | `Foundation/Tools/BuiltInTools.cs` | 612 | Tool definitions |
| 20 | `Module.Developer/Services/StoryExporter.cs` | 601 | SDD export |

---

## 2. God Classes

### 2.1 `StoryService` — 4 partials, ~2,860 lines

| Partial File | Lines | Responsibilities |
|--------------|------:|------------------|
| `StoryService.cs` | 1,749 | CRUD, git ops, copilot config, PR body, squash, step management, approval/reject/skip/reset, RAG query building, file-reference parsing, response parsing |
| `StoryService.Execution.cs` | 582 | Wave-based execution, quality gates, step tool dispatch |
| `StoryService.Planning.cs` | 277 | LLM analysis, planning, decomposition |
| `StoryService.Chat.cs` | 282 | Chat with workflow, chat with step, RAG context building |

**Constructor:** 16 injected dependencies.

**At least 15 distinct responsibilities:**

1. Story CRUD (create, get, list, delete, update)
2. Git worktree management
3. Git branch management
4. Copilot/MCP configuration setup
5. RAG indexing orchestration
6. LLM-driven analysis
7. LLM-driven planning
8. LLM-driven decomposition
9. Step execution with tool dispatch
10. Wave-based orchestration
11. Quality gate management
12. Chat with workflow/steps
13. Step lifecycle management (add, remove, approve, reject, skip, reset, reassign)
14. PR/commit message generation
15. Response parsing (JSON, structured, text)

**Recommendation:** Extract at least:
- `StoryRepository` — CRUD + fetch helpers
- `StoryGitService` — worktree creation, branch naming, squash commits
- `StoryPromptBuilder` — RAG query building, prompt construction
- `StoryResponseParser` — JSON/structured/text parsing
- `StoryStatusMachine` — enforce valid status transitions instead of ad-hoc `if` checks

### 2.2 `McpHandler` — 12 partials, ~5,400 lines

| Partial File | Lines | Responsibilities |
|--------------|------:|------------------|
| `McpHandler.cs` | 1,252 | Tool list definitions, JSON-RPC dispatch, tree ops, docs ops |
| `McpHandler.Navigate.cs` | 657 | Callers, usages, attributes, extensions, definition |
| `McpHandler.Languages.cs` | 616 | Python & TypeScript refactoring wrappers |
| `McpHandler.Workflow.cs` | 508 | Workflow CRUD, enrichment, step updates |
| `McpHandler.Generate.cs` | 385 | Code generation dispatch |
| `McpHandler.Refactor.cs` | 293 | Refactoring dispatch |
| `McpHandler.Validate.cs` | 287 | Compilation & test validation |
| `McpHandler.Search.cs` | 263 | Semantic search |
| `McpHandler.Inspect.cs` | 236 | Type inspection |
| `McpHandler.Pattern.cs` | 231 | Pattern loading |
| `McpHandler.Edit.cs` | 201 | Text editing |
| `McpHandler.Workspaces.cs` | 89 | Workspace registry CRUD |

**Constructor:** 15 injected dependencies.

**Critical issue:** `McpHandler.Navigate.cs` re-implements Roslyn symbol-finding logic (usages, attributes, extension methods) inline instead of delegating to `IRoslynWorkspaceService` or `IRoslynRefactoringService`. This duplicates logic and bypasses the service layer's error handling.

**Recommendation:** The McpHandler should be a thin translation layer (JSON-RPC → service call → JSON response). All business logic should live in the service layer. Consider using a `McpToolDispatcher` pattern where each tool is a separate class implementing a common interface, auto-discovered by convention.

### 2.3 `RoslynRefactoringService` — 5 partials, ~2,750 lines

| Partial File | Lines |
|--------------|------:|
| `RoslynRefactoringService.cs` | 1,495 |
| `RoslynRefactoringService.Generate.cs` | 738 |
| `RoslynRefactoringService.Interface.cs` | 357 |
| `RoslynRefactoringService.Move.cs` | 251 |
| `RoslynRefactoringService.Rename.cs` | 210 |

**Recommendation:** Extract `RoslynGenerationService` (create type, add property/method, generate constructor) from the refactoring service. These are semantically different operations — one transforms existing code, the other creates new code.

### 2.4 `ReActExecutor` — single file, 1,040 lines

Contains the core agent execution loop, tool dispatch, retry logic, system prompt construction (~130 lines of inline string templates), response parsing, parameter normalization, and validation tracking.

**Recommendation:** Extract `ReActPromptBuilder` (move the inline system prompt to a template file) and `ReActResponseParser` (structured JSON and text parsing).

---

## 3. Code Duplication

### 3.1 Story Fetch + Validate Pattern (18+ occurrences)

Nearly every method in `StoryService` starts with:

```csharp
var workflow = await _db.Stories.Include(w => w.Steps)
    .FirstOrDefaultAsync(w => w.Id == workflowId, ct)
    ?? throw new InvalidOperationException($"Story {workflowId} not found");
```

**Fix:** Extract a `GetStoryOrThrowAsync(Guid id, CancellationToken ct)` helper. This is a one-line extraction that eliminates 18+ copies of the same pattern.

### 3.2 Duplicated `FindContainingNamespace` Method

The exact same method exists in both `McpHandler.Navigate.cs` (instance) and `RoslynRefactoringService.cs` (static). Both walk the syntax tree to find the enclosing namespace.

**Fix:** Move to a shared `RoslynSyntaxHelpers` utility class.

### 3.3 Chat History Serialization (2 copies)

Identical chat-history append logic in `StoryService.Chat.cs` — deserialize history, add user+assistant messages, re-serialize. Appears in both `ChatWithWorkflowAsync` and `ChatWithStepAsync`.

**Fix:** Extract a `ChatHistoryManager` helper.

### 3.4 Language Handler Boilerplate

In `McpHandler.Languages.cs`, the Python and TypeScript operation handlers all follow an identical pattern:
1. Extract parameters from `JsonElement`
2. Create a request object
3. Call service
4. Map result to anonymous object

This pattern repeats 6+ times with only the request/result types varying.

**Fix:** Consider a generic dispatch helper or code-generation approach.

### 3.5 RAG Context Building (2 implementations)

Two separate methods build RAG context with identical `---`/Source/Relevance formatting — one in `StoryService` and one in `CodebaseContextService`. The `StoryService` version may be dead code now.

**Fix:** Audit whether the `StoryService` version is still called. If not, remove it. If both are needed, extract a shared `RagContextFormatter`.

---

## 4. Code Smells

### 4.1 Magic Strings

Capability strings `"coding"`, `"testing"`, `"review"`, `"documentation"`, `"fixing"` are hardcoded throughout `StoryService` and `McpHandler`. A `BuiltInCapabilities` constants class exists but doesn't cover these values.

**Fix:** Add these to `BuiltInCapabilities` and use `nameof()` or constants everywhere.

### 4.2 Inline System Prompt (ReActExecutor)

The `BuildSystemPrompt` method in `ReActExecutor.cs` contains ~130 lines of inline string interpolation for the system prompt. Every other prompt in the codebase uses Handlebars template files in `prompts/`.

**Fix:** Move to `prompts/react-system.prompt` and use the existing `IPromptRegistry` infrastructure.

### 4.3 Catch-and-Swallow Anti-Pattern

Multiple places use empty or near-empty `catch` blocks:
- `StoryService.cs` — JSON parsing of chat history
- `StoryService.Planning.cs` — step action parsing (4+ instances)
- `ReActExecutor.cs` — response parsing

**Fix:** At minimum, log at `Debug` level. Consider using `TryDeserialize` patterns instead of exception-driven control flow.

### 4.4 Debug Logging as Warnings

12+ instances of `_logger.LogWarning("[REACT-DEBUG]...")` and `[STEP-DEBUG]` throughout `ReActExecutor` and `StoryService.Execution.cs`. These are development-time instrumentation leaking into production log levels.

**Fix:** Change to `LogDebug` or remove entirely.

### 4.5 643-Line Interface

`IRoslynRefactoringService.cs` is 643 lines. An interface this large is a strong signal that the implementing class has too many responsibilities. The Interface Segregation Principle (ISP) suggests splitting this into focused interfaces.

**Fix:** Consider `IRoslynRefactorer` (rename, extract, safe delete), `IRoslynGenerator` (create type, add member, implement interface), and `IRoslynValidator` (compilation check, build validation).

### 4.6 Tool Definitions as Inline Anonymous Objects

The `ListTools` method in `McpHandler.cs` is 370+ lines of inline anonymous object definitions describing JSON schemas for each MCP tool. These schemas are hand-maintained and easy to drift from the actual parameter handling code.

**Fix:** Generate tool definitions from the parameter records or load from JSON schema files. This also makes it possible to validate that tool definitions match their handlers.

### 4.7 Tight Coupling to `Process` in Service Layer

`PythonRefactoringService` and `TypeScriptLanguageService` directly create and manage `System.Diagnostics.Process` instances. This makes them untestable without actually having Python/Node installed.

**Fix:** Extract an `IProcessRunner` abstraction (already common in the .NET ecosystem — e.g., `CliWrap`).

---

## 5. Positive Observations

Not everything needs criticism. These aspects are done well:

- **Partial class usage** — While the classes are too large, the file-level decomposition is thoughtful and thematic.
- **Primary constructors** — Consistent use of C# 12 primary constructors for dependency injection.
- **Interface-driven design** — Every service has a corresponding interface, enabling DI and testing.
- **Nullable reference types** — Properly enabled and generally respected.
- **Records for DTOs** — Data transfer objects use records consistently.
- **Test coverage** — 849 passing tests is substantial.
- **Coding standards document** — The standards exist and are largely followed (aside from the issues above).

---

## 6. Recommended Refactoring Priority

| Priority | Action | Effort | Impact |
|----------|--------|--------|--------|
| 🔴 P0 | Extract `GetStoryOrThrowAsync` helper | 30 min | Eliminates 18+ duplication sites |
| 🔴 P0 | Move debug `LogWarning` → `LogDebug` | 15 min | Cleans production logs |
| 🟡 P1 | Extract `StoryRepository` from `StoryService` | 2-3 hrs | Reduces largest class by ~400 lines |
| 🟡 P1 | Extract `ReActPromptBuilder` | 1-2 hrs | Moves 130-line inline prompt to template |
| 🟡 P1 | Deduplicate `FindContainingNamespace` | 30 min | Eliminates cross-layer duplication |
| 🟢 P2 | Split `IRoslynRefactoringService` | 4-6 hrs | ISP compliance, clearer contracts |
| 🟢 P2 | Make McpHandler a thin dispatcher | 1-2 days | Removes inline Roslyn queries from API layer |
| 🟢 P2 | Generate MCP tool definitions from records | 4-6 hrs | Eliminates 370-line manual schema |
| 🔵 P3 | Extract `StoryGitService` | 2-3 hrs | Further decomposition of StoryService |
| 🔵 P3 | Extract `IProcessRunner` abstraction | 2-3 hrs | Testability for Python/TS services |
| 🔵 P3 | Add missing capability constants | 1 hr | Eliminates magic strings |
