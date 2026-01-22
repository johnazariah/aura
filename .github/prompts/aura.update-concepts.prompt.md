# AURA Concepts Documentation - Deep Understanding

You are tasked with creating documentation that explains Aura's core concepts, architecture, and design decisions. This is for users who want to understand HOW and WHY Aura works the way it does.

## 🎯 Your Mission

Create documentation in `docs/concepts/` that provides deep understanding of Aura's architecture, emphasizing the design decisions that enable extensibility, local-first operation, and multi-agent orchestration.

## 📋 Required Files

### 1. `architecture.md` - System Architecture
- **High-level overview** (diagram + explanation)
- **Component breakdown**:
  - Aura API (ASP.NET Core, Aspire orchestration)
  - Agent Registry (dynamic + static agents)
  - LLM Provider System (multi-provider abstraction)
  - Workflow Orchestrator (sequential execution)
  - Git Workspace Manager (branching, commits)
  - Knowledge Store (RAG with pgvector)
  - VS Code Extension (TypeScript frontend)
  
- **Technology Stack**
  - .NET 10, C# 13
  - Aspire 13 for orchestration
  - PostgreSQL 17 + pgvector
  - OLLAMA for local LLM
  - LibGit2Sharp for Git
  - Octokit for GitHub
  - Roslyn for code analysis
  
- **Data Flow**
  - From workflow creation to PR merge
  - How context flows between agents
  - State persistence strategy
  
- **Design Patterns**
  - Result<T> monad for error handling
  - Repository pattern for data access
  - Strategy pattern for providers
  - Chain of Responsibility for agents
  - Plugin architecture for extensibility

### 2. `agents.md` - Understanding Agents
- **What is an agent?**
  - Specialized AI that performs one task well
  - Has specific capabilities and responsibilities
  - Can be prompt-based (markdown) or code-based (C#)
  
- **Built-in Agents**
  - BusinessAnalystAgent: Requirements analysis, task breakdown
  - CodingAgent: Code generation with functional patterns
  - TestingAgent: xUnit test generation
  - DocumentationAgent: README, API docs, comments
  - OrchestrationAgent: Workflow breakdown
  - ValidationAgent: Compilation, test execution
  - PrHealthMonitorAgent: PR status, feedback analysis
  
- **Agent Lifecycle**
  - Registration (discovery at startup)
  - Selection (by orchestrator)
  - Execution (with context)
  - Result handling (success/failure)
  
- **Agent Context**
  - WorkflowId, WorkItemId
  - Shared data dictionary
  - How agents communicate via context
  - Example: BA output → Coder input

### 3. `orchestration.md` - Workflow Orchestration
- **Sequential vs Parallel Execution**
  - Why sequential? (simpler debugging, clear dependencies)
  - Future: Parallel execution for independent tasks
  
- **Orchestration Phases**
  - Phase 1: Break down (OrchestrationAgent creates steps)
  - Phase 2: Execute steps sequentially
  - Phase 3: Validate and commit results
  
- **Step Execution Model**
  - Each step assigned to an agent
  - Agent receives context with previous outputs
  - Agent produces output for next agents
  - Validation before moving to next step
  
- **Error Handling**
  - Agent failure → workflow paused
  - Retry logic (inner validation loop)
  - Human intervention when needed
  - Resuming workflows

### 4. `local-first.md` - Local-First Philosophy
**(This file was already created earlier - see the detailed version)**

### 5. `llm-providers.md` - Multi-Provider LLM System
- **Provider Abstraction**
  - `ILlmProvider` interface
  - `ILlmProviderRegistry` for management
  - Why abstraction? (flexibility, no vendor lock-in)
  
- **Available Providers**
  - **OllamaProvider** (local, default)
    - Models: qwen2.5-coder, deepseek-coder, codellama
    - Pros: Privacy, free, offline
    - Cons: Requires GPU for speed
  - **MafProvider** (future: Microsoft Agent Framework)
    - Access to GPT-4, Claude, Azure OpenAI
    - Pros: Powerful models, no local GPU needed
    - Cons: Cost, privacy concerns
  
- **Per-Agent Provider Selection**
  - Frontmatter in markdown agents: `provider: "ollama"`
  - Constructor injection in code agents
  - Fallback logic when provider unavailable
  
- **Model Selection Strategy**
  - Small models for simple tasks (code formatting)
  - Large models for complex reasoning (architecture design)
  - Cost vs performance tradeoffs
  
- **Configuration Examples**
```yaml
# Simple agent, use local OLLAMA
---
id: format-code
provider: "ollama"
model: "qwen2.5-coder:7b"
---

# Complex reasoning, use GPT-4 (when available)
---
id: architecture-design
provider: "azure-openai"
model: "gpt-4-turbo"
---
```

### 6. `git-workspace.md` - Git Workspace Management
- **Why Git Workspaces?**
  - Isolation: Each workflow in its own branch
  - Reproducibility: All changes tracked in Git
  - Safety: Easy rollback if needed
  - Integration: Natural fit with PR workflow
  
- **Workspace Requirements**
  - Must be Git-initialized directory
  - Must have a remote (for PR creation)
  - Working directory should be clean
  
- **Branching Strategy**
  - Feature branches: `feature/{workflow-title-slug}`
  - Auto-created from current branch (usually main)
  - All agent outputs committed to feature branch
  
- **Commit Strategy**
  - One commit per agent output (granular history)
  - Descriptive commit messages
  - All files staged and committed automatically
  
- **PR Creation**
  - Pushed to remote repository
  - PR created via GitHub API (Octokit)
  - Linked back to original issue (if GitHub workflow)
  
- **Workspace Isolation**
  - Multiple concurrent workflows in different workspaces
  - No interference between workflows
  - Clean separation of concerns

### 7. `rag-knowledge.md` - RAG & Knowledge Ingestion
- **What is RAG?**
  - Retrieval-Augmented Generation
  - Agents search codebase knowledge before generating
  - More accurate, context-aware outputs
  
- **Knowledge Sources**
  - Roslyn semantic analysis (C# code)
  - Tree-sitter parsing (9 languages)
  - Documentation files
  - Previous workflow outputs
  
- **Ingestion Process**
  - Code parsed and analyzed (AST, symbols, dependencies)
  - Converted to text chunks
  - Embedded with LLM (vector embeddings)
  - Stored in PostgreSQL with pgvector
  
- **Search & Retrieval**
  - User query → embedded
  - Vector similarity search in pgvector
  - Top-K relevant code chunks retrieved
  - Provided as context to agent
  
- **Use Cases**
  - "How is authentication implemented?" → Find auth code
  - "Generate similar service" → Find existing services as examples
  - "Update all controllers" → Find controller patterns
  
- **Benefits**
  - Agents learn from your codebase
  - Consistent patterns across generated code
  - Fewer hallucinations

### 8. `validation-loop.md` - Inner Validation Loop
- **What is the Validation Loop?**
  - Agents validate their own output
  - Real compilation and test execution
  - Auto-retry with feedback if validation fails
  
- **Validation Steps**
  1. Agent generates code/tests
  2. ValidationAgent runs `dotnet build`
  3. If compilation fails → errors fed back to agent
  4. Agent regenerates code with fixes
  5. Repeat up to 3 times
  6. If still failing → escalate to human
  
- **What Gets Validated**
  - Code compiles without errors
  - Tests pass (if tests generated)
  - Code meets style guidelines (optional)
  - No security issues (optional)
  
- **Feedback Loop**
```
CodingAgent → Generated Code
    ↓
ValidationAgent → Compilation Error!
    ↓
CodingAgent → Fixed Code (attempt 2)
    ↓
ValidationAgent → Success! ✓
    ↓
Next Agent (TestingAgent)
```

- **Benefits**
  - Drastically reduces invalid code
  - Agents learn from their mistakes
  - Higher quality output
  - Fewer human interventions

## ✍️ Writing Guidelines

### Audience
- Developers who want to understand Aura deeply
- Potential contributors
- Users deciding if Aura fits their needs
- Architects evaluating the design

### Style
- **Technical but accessible**: Use proper terminology, but explain it
- **Design-focused**: Emphasize WHY, not just WHAT
- **Diagram-heavy**: ASCII art or descriptions of architecture
- **Example-driven**: Every concept with concrete example

### Structure Each Page
```markdown
# Concept Title

> **TL;DR**: One-sentence summary

## Overview
[High-level explanation]

## How It Works
[Detailed mechanism with examples]

## Design Decisions
[Why this approach? What alternatives were considered?]

## Benefits & Trade-offs
[Honest assessment]

## Examples
[Real code/scenarios]

## Related Concepts
[Links to related pages]
```

## 📊 Quality Criteria

Your concepts documentation succeeds if:

- ✅ Reader understands Aura's architecture in 15 minutes
- ✅ Design decisions are clearly explained with rationale
- ✅ Every concept has a concrete example
- ✅ Diagrams clarify complex relationships
- ✅ Honest about trade-offs (not just benefits)
- ✅ Technical depth without overwhelming
- ✅ Encourages further exploration

## 🔍 What to Review

- `README.md` - Architecture overview
- `docs/MULTI-PROVIDER-AGENTS.md` - Provider system
- `docs/ROSLYN-AGENT-FINAL-SUMMARY.md` - Code analysis
- `docs/RAG-FOUNDATION-SUMMARY.md` - Knowledge ingestion
- `docs/INGESTION-SERVICE-ARCHITECTURE.md` - Ingestion architecture
- `plan/05-architecture/` - Architectural decisions
- Source code architecture (project structure)

Now create documentation that makes developers say "Wow, this is well-designed!" 🚀
