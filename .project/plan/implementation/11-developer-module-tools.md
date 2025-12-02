# Developer Module Implementation Plan

**Version:** 1.0
**Status:** Draft
**Created:** 2025-12-02
**Last Updated:** 2025-12-02

## Overview

This plan details the implementation of the Developer Module's code generation and testing capabilities, building on the decisions in ADR-012 through ADR-015.

**Goal:** Enable Aura to write code and tests for itself (bootstrap scenario).

**Silver Thread Target:** User creates workflow "Write tests for WorkflowService" → Agent uses tools to understand code → Agent generates test file → Tests compile and run.

## Architecture Summary

```
┌─────────────────────────────────────────────────────────────────────┐
│                         VS Code Extension                            │
│  Workflow Panel │ Step Execution │ Tool Trace View │ Output Display │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│                           Aura.Api                                   │
│              /api/developer/workflows/{id}/steps/{id}/execute        │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│                     Aura.Module.Developer                            │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐     │
│  │ WorkflowService │  │ RoslynTools     │  │ C# Ingestor     │     │
│  │                 │  │ (10 tools)      │  │ (Graph Builder) │     │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘     │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│                       Aura.Foundation                                │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐              │
│  │ IToolRegistry│  │ ReActExecutor│  │ Graph RAG    │              │
│  │ ITool        │  │              │  │ Schema       │              │
│  └──────────────┘  └──────────────┘  └──────────────┘              │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│                        Infrastructure                                │
│         PostgreSQL (Graph + Data)  │  Ollama (LLM)                  │
└─────────────────────────────────────────────────────────────────────┘
```

## Implementation Phases

### Phase 1: Foundation Tool Framework

**Goal:** Enable any agent to use tools via ReAct loop.

**Deliverables:**

1. **Tool Contracts** (`Aura.Foundation/Tools/Contracts/`)
   ```csharp
   public interface ITool
   {
       string ToolId { get; }
       string Name { get; }
       string Description { get; }
       Type InputType { get; }
       Type OutputType { get; }
       Task<object> ExecuteAsync(object input, CancellationToken ct);
   }
   
   public interface ITool<TInput, TOutput> : ITool
   {
       Task<TOutput> ExecuteAsync(TInput input, CancellationToken ct);
   }
   ```

2. **Tool Registry** (`Aura.Foundation/Tools/`)
   ```csharp
   public interface IToolRegistry
   {
       void Register(ITool tool);
       ITool? GetTool(string toolId);
       IReadOnlyList<ITool> GetAllTools();
       IReadOnlyList<ToolDescription> GetToolDescriptions();  // For LLM prompt
   }
   ```

3. **ReAct Executor** (`Aura.Foundation/Agents/`)
   ```csharp
   public interface IReActExecutor
   {
       Task<ReActResult> ExecuteAsync(
           string task,
           IReadOnlyList<ITool> availableTools,
           ILlmProvider llm,
           ReActOptions options,
           CancellationToken ct);
   }
   
   public record ReActResult
   {
       public required bool Success { get; init; }
       public required string FinalAnswer { get; init; }
       public required IReadOnlyList<ReActStep> Steps { get; init; }
       public string? Error { get; init; }
   }
   
   public record ReActStep
   {
       public required int StepNumber { get; init; }
       public required string Thought { get; init; }
       public required string Action { get; init; }
       public required string ActionInput { get; init; }
       public required string Observation { get; init; }
   }
   ```

4. **ReAct Prompt Template** (`prompts/react-loop.prompt`)

**Tests:**
- Unit tests for tool registry
- Unit tests for ReAct prompt parsing
- Integration test: Simple tool (calculator) + ReAct loop

**Estimated Effort:** 2-3 days

---

### Phase 2: Graph RAG Schema

**Goal:** Store code structure for intelligent retrieval.

**Deliverables:**

1. **Database Migration** (`scripts/migrations/`)
   - `code_nodes` table
   - `code_edges` table
   - Indexes for common query patterns

2. **EF Core Entities** (`Aura.Foundation/Data/Entities/`)
   ```csharp
   public class CodeNode { ... }
   public class CodeEdge { ... }
   ```

3. **Graph Query Service** (`Aura.Foundation/Rag/`)
   ```csharp
   public interface ICodeGraphService
   {
       Task<IReadOnlyList<CodeNode>> GetImplementationsAsync(string interfaceName, CancellationToken ct);
       Task<IReadOnlyList<CodeNode>> GetCallersAsync(string methodName, CancellationToken ct);
       Task<IReadOnlyList<CodeNode>> GetDependenciesAsync(string typeName, int depth, CancellationToken ct);
       Task<CodeNode?> GetTypeInfoAsync(string fullName, CancellationToken ct);
   }
   ```

**Tests:**
- Unit tests for graph queries
- Integration test: Insert sample graph, query relationships

**Estimated Effort:** 1-2 days

---

### Phase 3: Roslyn Tools (Developer Module)

**Goal:** Implement the 10 tools for code manipulation.

**Deliverables:**

1. **Roslyn Workspace Service** (`Aura.Module.Developer/Services/`)
   ```csharp
   public interface IRoslynWorkspaceService
   {
       Task<Solution> OpenSolutionAsync(string solutionPath, CancellationToken ct);
       Task<Compilation> GetCompilationAsync(string projectPath, CancellationToken ct);
       // Caching, lazy loading
   }
   ```

2. **Tool Implementations** (`Aura.Module.Developer/Tools/`)

   | Tool | File |
   |------|------|
   | `list_projects` | `ListProjectsTool.cs` |
   | `list_classes` | `ListClassesTool.cs` |
   | `get_class_info` | `GetClassInfoTool.cs` |
   | `read_file` | `ReadFileTool.cs` |
   | `write_file` | `WriteFileTool.cs` |
   | `modify_file` | `ModifyFileTool.cs` |
   | `find_usages` | `FindUsagesTool.cs` |
   | `get_project_references` | `GetProjectReferencesTool.cs` |
   | `validate_compilation` | `ValidateCompilationTool.cs` |
   | `run_tests` | `RunTestsTool.cs` |

3. **Tool Registration** (`Aura.Module.Developer/DeveloperModule.cs`)
   - Register all tools with `IToolRegistry` during module startup

4. **Tool Contracts** (`Aura.Module.Developer/Tools/Contracts/`)
   - Strongly-typed input/output for each tool

**Tests:**
- Unit tests for each tool (with test project/solution)
- Integration test: Full ReAct loop with real Roslyn tools

**Estimated Effort:** 4-5 days

---

### Phase 4: C# Code Ingestor

**Goal:** Parse C# code into Graph RAG.

**Deliverables:**

1. **C# Ingestor** (`Aura.Module.Developer/Ingestion/`)
   ```csharp
   public interface ICSharpIngestor
   {
       Task<int> IngestSolutionAsync(string solutionPath, string workspacePath, CancellationToken ct);
       Task<int> IngestProjectAsync(string projectPath, string workspacePath, CancellationToken ct);
       Task<int> IngestFileAsync(string filePath, string workspacePath, CancellationToken ct);
   }
   ```

2. **Roslyn Visitor** (`Aura.Module.Developer/Ingestion/`)
   - `CSharpGraphBuilder` - Walks syntax trees, extracts nodes and edges

3. **Integration with Workflow**
   - On workflow creation, ingest worktree into graph
   - Store with `workspace_path` for isolation

**Tests:**
- Unit tests: Parse sample C# file, verify graph structure
- Integration test: Ingest Aura.Foundation, query graph

**Estimated Effort:** 2-3 days

---

### Phase 5: Agent Contracts

**Goal:** Define strongly-typed contracts for all capabilities.

**Deliverables:**

1. **Capability Contracts** (`Aura.Foundation/Contracts/`)
   ```
   Contracts/
   ├── Coding/
   │   ├── CodingInput.cs
   │   └── CodingOutput.cs
   ├── Testing/
   │   ├── TestingInput.cs
   │   └── TestingOutput.cs
   ├── Review/
   │   ├── ReviewInput.cs
   │   └── ReviewOutput.cs
   ├── Analysis/
   │   ├── AnalysisInput.cs
   │   └── AnalysisOutput.cs
   └── Common/
       ├── FileArtifact.cs
       ├── CompileDiagnostic.cs
       └── TestResult.cs
   ```

2. **Contract Validation**
   - Compile-time: Agents implement `IAgent<TInput, TOutput>`
   - Runtime: JSON schema validation for markdown agents

3. **Agent Interface Updates** (`Aura.Foundation/Agents/`)
   ```csharp
   public interface IAgent<TInput, TOutput>
       where TInput : IAgentInput
       where TOutput : IAgentOutput
   {
       Task<TOutput> ExecuteAsync(TInput input, CancellationToken ct);
   }
   ```

**Tests:**
- Compile-time verification (if it builds, contracts are correct)
- Unit tests for JSON serialization round-trip

**Estimated Effort:** 1-2 days

---

### Phase 6: Tool-Using Coding Agent

**Goal:** Create a C# coding agent that uses Roslyn tools.

**Deliverables:**

1. **RoslynCodingAgent** (`Aura.Module.Developer/Agents/`)
   ```csharp
   public class RoslynCodingAgent : IAgent<CodingInput, CodingOutput>
   {
       // Uses ReActExecutor with Roslyn tools
       // Capabilities: ["csharp-coding", "coding"]
       // Priority: 30
   }
   ```

2. **Agent Registration**
   - Register as coded agent in Developer Module

3. **System Prompt** (`prompts/roslyn-coding-agent.prompt`)
   - Instructions for using tools effectively
   - Examples of tool sequences

**Tests:**
- Integration test: "Add a new class to Aura.Foundation"
- Verify: File created, compiles, correct structure

**Estimated Effort:** 2-3 days

---

### Phase 7: Tool-Using Testing Agent

**Goal:** Create a test generation agent that uses Roslyn tools.

**Deliverables:**

1. **TestingAgent** (`Aura.Module.Developer/Agents/`)
   ```csharp
   public class TestingAgent : IAgent<TestingInput, TestingOutput>
   {
       // Uses ReActExecutor with Roslyn tools
       // Capabilities: ["csharp-testing", "testing"]
       // Priority: 30
   }
   ```

2. **System Prompt** (`prompts/testing-agent.prompt`)
   - Test generation patterns
   - xUnit conventions
   - Mocking guidance

**Tests:**
- Integration test: "Write tests for AgentRegistry"
- Verify: Test file created, compiles, tests pass

**Estimated Effort:** 2-3 days

---

### Phase 8: UI Integration

**Goal:** Show tool execution trace in VS Code extension.

**Deliverables:**

1. **API Response Enhancement**
   - Include `ReActStep[]` in step execution response
   - Include tool inputs/outputs

2. **Workflow Panel Updates**
   - Show tool trace for each step
   - Expandable thought/action/observation
   - Syntax highlighting for code outputs

3. **File Diff View**
   - "View Diff" button for modified files
   - Opens VS Code diff editor

**Tests:**
- Manual: Execute workflow, verify trace visible
- Manual: Click "View Diff", verify diff opens

**Estimated Effort:** 2-3 days

---

## Phase Summary

| Phase | Description | Effort | Dependencies |
|-------|-------------|--------|--------------|
| 1 | Foundation Tool Framework | 2-3 days | None |
| 2 | Graph RAG Schema | 1-2 days | None |
| 3 | Roslyn Tools | 4-5 days | Phase 1 |
| 4 | C# Ingestor | 2-3 days | Phase 2, Phase 3 |
| 5 | Agent Contracts | 1-2 days | None |
| 6 | Roslyn Coding Agent | 2-3 days | Phase 1, 3, 5 |
| 7 | Testing Agent | 2-3 days | Phase 1, 3, 5 |
| 8 | UI Integration | 2-3 days | Phase 6, 7 |

**Total Estimated Effort:** 17-24 days

**Parallelization:**
- Phase 1 + 2 + 5 can run in parallel
- Phase 3 + 4 can run in parallel after Phase 1 + 2
- Phase 6 + 7 can run in parallel after Phase 3 + 5
- Phase 8 after Phase 6 + 7

**Optimistic Timeline:** 10-12 days with parallelization

---

## Success Criteria

### Silver Thread Test

1. User creates workflow: "Write tests for WorkflowService"
2. Workflow creates worktree, ingests code into graph
3. User triggers "Analyze" → Agent understands WorkflowService structure
4. User triggers "Plan" → Agent creates steps: "Create test file", "Write CreateAsync tests", etc.
5. User executes each step:
   - Agent uses tools (list_classes, get_class_info, write_file, validate_compilation)
   - Tool trace visible in UI
   - Output shows generated test code
6. User sees final test file in worktree
7. Tests compile and run: `dotnet test` passes

### Definition of Done

- [ ] Real API calls (no mocks)
- [ ] Real Ollama LLM calls (no stubs)
- [ ] Real PostgreSQL persistence
- [ ] Real Roslyn compilation
- [ ] Real test execution
- [ ] UI shows real results
- [ ] Full transparency of agent reasoning

---

## Open Questions

1. **Roslyn workspace caching**: How long to keep workspace open? Per-workflow? Global?
2. **Test framework detection**: Auto-detect xUnit vs NUnit vs MSTest, or assume xUnit?
3. **Graph update on file change**: Real-time sync or re-ingest on demand?
4. **Tool timeout**: How long should a tool be allowed to run?

---

## References

- ADR-012: Tool-Using Agents with ReAct Loop
- ADR-013: Strongly-Typed Agent Contracts
- ADR-014: Developer Module Roslyn Tools
- ADR-015: Graph RAG for Code Understanding
- [12-developer-module.md](../spec/12-developer-module.md) - Original spec
