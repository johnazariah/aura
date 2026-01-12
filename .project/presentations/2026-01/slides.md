---
marp: true
theme: default
paginate: true
header: "**Aura** - Local-First AI for Knowledge Work"
footer: "January 2026"
style: |
  section {
    font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
  }
  h1 {
    color: #0078d4;
  }
  .columns {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 1rem;
  }
---

<!-- _class: lead -->

# Aura

## Local-First, Privacy-Safe AI Foundation for Knowledge Work

**John Azariah**  
January 2026

---

# Agenda

1. **Motivations** - Why local-first AI?
2. **Architecture** - How Aura is built
3. **Core Use Cases** - Developer workflows in action
4. **Technical Deep Dive** - Implementation details
5. **Q&A**

---

<!-- _class: lead -->

# Part 1: Motivations

## Why Local-First AI Matters

---

# The Problem with Cloud AI

<div class="columns">
<div>

### Privacy Concerns
- Your code goes to third-party servers
- Sensitive data leaves your control
- Compliance challenges (HIPAA, GDPR)

### Dependency Issues
- Requires internet connectivity
- API costs add up ($0.01-0.10 per request)
- Provider outages affect your work

</div>
<div>

### Trust Deficit
- "Trust us with your data"
- Black-box processing
- Terms of service can change

### Developer Pain Points
- Rate limits during crunch time
- Latency for every request
- Vendor lock-in

</div>
</div>

---

# The Vision: "Windows Recall, But Safe"

> **What if you had a powerful AI assistant that never phones home?**

<div class="columns">
<div>

### What Users Want
- AI that understands their codebase
- Semantic search across files
- Intelligent automation
- **Complete privacy**

</div>
<div>

### What Aura Delivers
- ✅ Local LLM (Ollama)
- ✅ Local database (PostgreSQL)
- ✅ Local RAG (pgvector)
- ✅ Works offline

</div>
</div>

---

# The Privacy Promise

```text
┌────────────────────────────────────────────────────────────┐
│                    YOUR MACHINE                             │
│                                                            │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐  │
│  │  Ollama  │  │ Postgres │  │   RAG    │  │   Aura   │  │
│  │   LLM    │  │   + vec  │  │  Index   │  │   API    │  │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘  │
│                                                            │
│  • No internet required                                    │
│  • No telemetry                                            │
│  • No API keys needed                                      │
│  • Inspect network traffic yourself                        │
└────────────────────────────────────────────────────────────┘
```

Cloud services (GitHub, Azure OpenAI) are **explicitly opt-in**.

---

# Why Now?

### The Enabling Technologies

| Technology | What Changed |
|------------|--------------|
| **Local LLMs** | Llama 3, Qwen 2.5 Coder run well on consumer GPUs |
| **Ollama** | Dead-simple local LLM hosting |
| **pgvector** | Production-quality vector search in PostgreSQL |
| **.NET Aspire** | Orchestration without Kubernetes complexity |

### The Sweet Spot
- 7B parameter models fit in 8GB VRAM
- Inference is fast enough for interactive use
- Quality approaches cloud models for coding tasks

---

<!-- _class: lead -->

# Part 2: Architecture

## How Aura is Built

---

# High-Level Architecture

```text
┌─────────────────────────────────────────────────────────────┐
│                  VS Code Extension                           │
│         (Workflow UI, Chat, Status Panels)                   │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                      Aura API                                │
│                 (REST Endpoints)                             │
└─────────────────────────────────────────────────────────────┘
                              │
         ┌────────────────────┼────────────────────┐
         ▼                    ▼                    ▼
┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐
│ Aura.Module.    │  │ Aura.Module.    │  │ Aura.Module.    │
│   Developer     │  │   Research      │  │   Personal      │
│                 │  │   (Future)      │  │   (Future)      │
└─────────────────┘  └─────────────────┘  └─────────────────┘
         │                    │                    │
         └────────────────────┼────────────────────┘
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                   Aura.Foundation                            │
│    Agents │ LLM │ RAG │ Tools │ Database │ Modules          │
└─────────────────────────────────────────────────────────────┘
                              │
         ┌────────────────────┼────────────────────┐
         ▼                    ▼                    ▼
┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐
│     Ollama      │  │   PostgreSQL    │  │  File System    │
│ (Local LLM)     │  │  + pgvector     │  │  (Your Code)    │
└─────────────────┘  └─────────────────┘  └─────────────────┘
```

---

# Composable Modules

### Design Principle: Enable Only What You Need

```json
{
  "Aura": {
    "Modules": {
      "Enabled": ["developer"]
    }
  }
}
```

| Module | Purpose | Status |
|--------|---------|--------|
| **Developer** | Git automation, code generation, workflows | ✅ Complete |
| **Research** | Paper indexing, synthesis, citations | 🔮 Future |
| **Personal** | Receipts, budgets, general assistant | 🔮 Future |

### Key Rule: Modules Never Depend on Each Other
All modules depend only on Foundation.

---

# Foundation Layer

<div class="columns">
<div>

### Agents
- Markdown definitions (hot-reload)
- Capability-based routing
- ReAct tool execution

### RAG Pipeline
- Local embeddings (nomic-embed)
- pgvector storage
- Semantic search

</div>
<div>

### LLM Providers
- Ollama (default, local)
- Azure OpenAI (opt-in)
- OpenAI (opt-in)

### Data Layer
- PostgreSQL repositories
- Entity Framework Core
- Transactional consistency

</div>
</div>

---

# The RAG Pipeline

```text
┌─────────────────────────────────────────────────────────────┐
│                      RAG Pipeline                            │
│                                                              │
│  ┌──────────┐    ┌──────────┐    ┌──────────┐               │
│  │  Ingest  │───▶│  Embed   │───▶│  Store   │               │
│  │  Files   │    │ (Ollama) │    │(pgvector)│               │
│  └──────────┘    └──────────┘    └──────────┘               │
│                                                              │
│  ┌──────────┐    ┌──────────┐    ┌──────────┐               │
│  │  Query   │───▶│  Search  │───▶│ Retrieve │               │
│  │          │    │ (vector) │    │ Context  │               │
│  └──────────┘    └──────────┘    └──────────┘               │
│                                                              │
│  Embedding Model: nomic-embed-text (137M params, fast)       │
│  Index: HNSW for approximate nearest neighbor                │
└─────────────────────────────────────────────────────────────┘
```

---

# Code Graph (Beyond Vector RAG)

### The Problem with Text-Only RAG
Vector similarity finds **"similar text"**, not **"related code"**.

> "What implements `IWorkflowService`?" requires graph traversal, not cosine similarity.

### Solution: Entity-Relationship Graph

| Node Types | Edge Types |
|------------|------------|
| Solution, Project, Namespace | contains, references |
| Type (Class/Interface/Record) | inherits, implements |
| Member (Method/Property) | calls, uses |
| File | contains |

Powered by **TreeSitter** (multi-language) + **Roslyn** (C# deep analysis).

---

# Markdown Agent Definitions

```markdown
# Coding Agent

## Metadata
- **Type**: Coder
- **Name**: Coding Agent
- **Provider**: ollama
- **Model**: qwen2.5-coder:7b
- **Temperature**: 0.7

## Capabilities
- software-development-csharp
- software-development-typescript

## System Prompt
You are an expert polyglot developer...
```

### Benefits
- Human-readable and editable
- Hot-reloadable (no restart needed)
- Version-controllable (git-friendly)

---

<!-- _class: lead -->

# Part 3: Core Use Cases

## Developer Workflows in Action

---

# The Developer Workflow

```text
┌─────────────────────────────────────────────────────────────┐
│                LOCAL-ONLY WORKFLOW                           │
│                                                              │
│  1. CREATE WORKFLOW                                          │
│     └─> "Add retry logic to HTTP client"                     │
│     └─> Create git worktree for isolated development         │
│                                                              │
│  2. ANALYZE (Enrich with RAG)                                │
│     └─> Agent finds related code, patterns, docs             │
│     └─> Structured requirements generated                    │
│                                                              │
│  3. PLAN (Generate Steps)                                    │
│     └─> Business analyst agent creates execution plan        │
│     └─> Each step: capability + description + agent          │
│                                                              │
│  4. EXECUTE (Human-in-the-Loop)                              │
│     └─> One step at a time                                   │
│     └─> Review, approve/reject, iterate                      │
│                                                              │
│  5. COMPLETE                                                 │
│     └─> Commit, push, create PR                              │
└─────────────────────────────────────────────────────────────┘
```

---

# Step Types and Capabilities

| Capability | Agent | What It Does |
|------------|-------|--------------|
| `software-development-csharp` | C# Coding Agent | Write/modify C# code |
| `software-development-typescript` | TS Coding Agent | Write/modify TypeScript |
| `code-review` | Code Review Agent | Analyze code quality |
| `documentation` | Documentation Agent | Write docs, comments |
| `testing` | Testing Agent | Generate unit tests |

### Capability Matching
1. Step specifies required capability (e.g., "software-development-csharp")
2. System finds agents with matching capability
3. User can reassign if needed

---

# Human-in-the-Loop Philosophy

> *"The user orchestrates. Aura executes."*

### Why Not Full Automation?

<div class="columns">
<div>

**The Problem**
- LLMs make mistakes
- Context matters
- Users know best

</div>
<div>

**Our Solution**
- Review each step output
- Approve, reject, or skip
- Chat with agent for refinement

</div>
</div>

### Step Actions

| Action | Effect |
|--------|--------|
| **Approve** | Accept output, move to next step |
| **Reject** | Provide feedback, agent retries |
| **Skip** | Mark step as skipped, continue |
| **Chat** | Ask questions, request changes |
| **Reassign** | Change to different agent |

---

# Demo: VS Code Extension

### Key UI Components

| Panel | Purpose |
|-------|---------|
| **Workflow Tree** | List all workflows, grouped by status |
| **Workflow Panel** | Create, analyze, plan, execute |
| **Step Details** | Approve/reject/chat with step output |
| **Agent Tree** | Browse available agents by capability |
| **Status Panel** | Health, Ollama models, RAG stats |

---

<!-- _class: lead -->

# Part 4: Technical Implementation

## How the Code Works

---

# Project Structure

```text
src/
├── Aura.Foundation/          # Core infrastructure
│   ├── Agents/               # Agent registry, execution
│   ├── LlmProviders/         # Ollama, Azure, OpenAI
│   ├── Rag/                  # Indexing, search, embeddings
│   ├── Tools/                # Tool registry, execution
│   └── Data/                 # EF Core, repositories
│
├── Aura.Module.Developer/    # Developer vertical
│   ├── Services/             # WorkflowService, GitService
│   ├── CodeGraph/            # Graph storage, queries
│   └── Ingesters/            # TreeSitter, Roslyn
│
├── Aura.Api/                 # REST API host
│   └── Program.cs            # All endpoints (single file)
│
└── Aura.AppHost/             # .NET Aspire orchestration

extension/                    # VS Code extension (TypeScript)
agents/                       # Markdown agent definitions
prompts/                      # Handlebars prompt templates
```

---

# Agent Execution Flow

```text
┌──────────────────────────────────────────────────────────────┐
│                   Agent Execution                             │
│                                                              │
│  ┌────────────────┐                                          │
│  │ Agent Markdown │  "Who you are" (system prompt)           │
│  └───────┬────────┘                                          │
│          │                                                   │
│          ▼                                                   │
│  ┌────────────────┐                                          │
│  │  RAG Context   │  Auto-injected from indexed code         │
│  └───────┬────────┘                                          │
│          │                                                   │
│          ▼                                                   │
│  ┌────────────────┐                                          │
│  │ Prompt Template│  "What to do" (Handlebars + YAML)        │
│  └───────┬────────┘                                          │
│          │                                                   │
│          ▼                                                   │
│  ┌────────────────┐                                          │
│  │  LLM Provider  │  Ollama / Azure OpenAI / OpenAI          │
│  └────────────────┘                                          │
└──────────────────────────────────────────────────────────────┘
```

---

# ReAct Tool Execution

```text
┌─────────────────────────────────────────────────────────────┐
│                    ReAct Loop                                │
│                                                              │
│  1. Agent receives task + available tools                   │
│                                                              │
│  2. THINK: "I need to find the HttpClient class"            │
│                                                              │
│  3. ACT: call read_file("src/HttpClient.cs")                │
│                                                              │
│  4. OBSERVE: <file contents returned>                       │
│                                                              │
│  5. THINK: "Now I'll add retry logic"                       │
│                                                              │
│  6. ACT: call write_file(...)                               │
│                                                              │
│  7. DONE: Return final result                               │
│                                                              │
│  ┌─────────┐    ┌─────────┐    ┌─────────┐                  │
│  │  THINK  │───▶│   ACT   │───▶│ OBSERVE │───┐              │
│  └─────────┘    └─────────┘    └─────────┘   │              │
│       ▲                                       │              │
│       └───────────────────────────────────────┘              │
└─────────────────────────────────────────────────────────────┘
```

### Why ReAct over Function Calling?
Model-agnostic, debuggable, works with any Ollama model.

---

# Code Graph with TreeSitter

### Multi-Language Support

```yaml
# agents/languages/csharp.yaml
extensions: [".cs"]
queries:
  class: "(class_declaration name: (identifier) @name)"
  interface: "(interface_declaration name: (identifier) @name)"
  method: "(method_declaration name: (identifier) @name)"
  property: "(property_declaration name: (identifier) @name)"
```

### Supported Languages
C#, TypeScript, Python, Rust, Go, F#, Haskell, Elm, and more.

### Graph Queries via API

```http
GET /api/graph/find/{name}
GET /api/graph/implementations/{interface}
GET /api/graph/callers/{method}
GET /api/graph/members/{type}
```

---

# API Design (Single File)

All endpoints in `src/Aura.Api/Program.cs` (~2000 lines):

```csharp
// Workflow endpoints
app.MapPost("/api/developer/workflows", CreateWorkflow);
app.MapGet("/api/developer/workflows/{id}", GetWorkflow);
app.MapPost("/api/developer/workflows/{id}/analyze", AnalyzeWorkflow);
app.MapPost("/api/developer/workflows/{id}/plan", PlanWorkflow);
app.MapPost("/api/developer/workflows/{id}/steps/{stepId}/execute", ExecuteStep);

// Step management (Assisted UI)
app.MapPost("/api/developer/workflows/{id}/steps/{stepId}/approve", ApproveStep);
app.MapPost("/api/developer/workflows/{id}/steps/{stepId}/reject", RejectStep);
app.MapPost("/api/developer/workflows/{id}/steps/{stepId}/chat", ChatWithStep);

// RAG and Graph
app.MapPost("/api/rag/search", SearchRag);
app.MapGet("/api/graph/find/{name}", FindInGraph);
```

### Why Single File?
Easy to find, no hunting through controllers.

---

# Key Technologies

| Technology | Purpose |
|------------|---------|
| **.NET 9** | High-performance runtime |
| **Aspire** | Local orchestration (Postgres, Ollama) |
| **EF Core** | Database access |
| **pgvector** | Vector similarity search |
| **TreeSitter** | Fast multi-language parsing |
| **Roslyn** | Deep C# analysis |
| **Ollama** | Local LLM inference |
| **TypeScript** | VS Code extension |

### Test Coverage
- 400+ unit tests
- Integration tests with real Postgres
- Extension tests with VS Code test runner

---

# What's Next

### Near-Term
- Workspace onboarding UX improvements
- MCP (Model Context Protocol) server
- macOS native support

### Future Modules
- **Research Module** - Paper management, synthesis
- **Personal Module** - Receipts, budgets, general assistant

### Community
- Open source (MIT License)
- Contributions welcome
- GitHub: `github.com/johnazariah/aura`

---

<!-- _class: lead -->

# Questions?

**Aura** - Local-First AI for Knowledge Work

GitHub: `github.com/johnazariah/aura`  
License: MIT

---

<!-- _class: lead -->

# Thank You!

```text
┌────────────────────────────────────────────────────────────┐
│                                                            │
│   "The best software is built not by adding features       │
│    until it works, but by removing complexity until        │
│    it can't fail."                                         │
│                                                            │
└────────────────────────────────────────────────────────────┘
```
