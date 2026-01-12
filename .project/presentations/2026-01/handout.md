# Aura: Local-First AI for Knowledge Work

**Presentation Handout | January 2026**

---

## What is Aura?

Aura is a **local-first, privacy-safe AI foundation** for knowledge work. It runs entirely on your machine—no cloud required, no data leaves your control.

> *"Windows Recall, but local and safe"*

### The Privacy Promise

| Component | Location |
|-----------|----------|
| LLM (Ollama) | Your machine |
| Database (PostgreSQL) | Your machine |
| RAG Index (pgvector) | Your machine |
| Your Code & Documents | Your machine |

**No internet required. No telemetry. No API keys needed.**

---

## Why Local-First AI?

### Cloud AI Challenges

| Challenge | Impact |
|-----------|--------|
| **Privacy** | Your code goes to third-party servers |
| **Compliance** | HIPAA, GDPR, enterprise policies |
| **Cost** | $0.01-0.10 per request adds up |
| **Availability** | Requires internet, subject to outages |
| **Latency** | Network round-trip for every request |

### The Local Alternative

Modern local LLMs (Llama 3, Qwen 2.5 Coder) run well on consumer GPUs:
- 7B parameter models fit in 8GB VRAM
- Quality approaches cloud models for coding tasks
- Ollama makes hosting trivial

---

## Architecture Overview

```text
┌─────────────────────────────────────────────────────────────┐
│                    VS Code Extension                         │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                        Aura API                              │
└─────────────────────────────────────────────────────────────┘
                              │
         ┌────────────────────┼────────────────────┐
         ▼                    ▼                    ▼
┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐
│    Developer    │  │    Research     │  │    Personal     │
│     Module      │  │  Module (Future)│  │ Module (Future) │
└─────────────────┘  └─────────────────┘  └─────────────────┘
         │                    │                    │
         └────────────────────┼────────────────────┘
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                   Aura.Foundation                            │
│       Agents │ LLM │ RAG │ Tools │ Database │ Modules       │
└─────────────────────────────────────────────────────────────┘
                              │
         ┌────────────────────┼────────────────────┐
         ▼                    ▼                    ▼
┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐
│     Ollama      │  │   PostgreSQL    │  │  File System    │
│   (Local LLM)   │  │   + pgvector    │  │  (Your Code)    │
└─────────────────┘  └─────────────────┘  └─────────────────┘
```

### Key Principles

1. **Composable Modules** - Enable only what you need
2. **Foundation Layer** - Shared infrastructure (agents, RAG, LLM)
3. **No Module Dependencies** - Modules depend only on Foundation

---

## Core Features

### 1. RAG Pipeline (Semantic Search)

Index your codebase and documents for semantic search:

- **Embeddings**: Local models via Ollama (nomic-embed-text)
- **Storage**: PostgreSQL with pgvector extension
- **Indexing**: HNSW for fast approximate nearest neighbor

### 2. Code Graph (Structural Search)

Beyond text similarity—understand code structure:

| Query | Example |
|-------|---------|
| Find by name | `GET /api/graph/find/HttpClient` |
| Find implementations | `GET /api/graph/implementations/IService` |
| Find callers | `GET /api/graph/callers/SendAsync` |

Powered by TreeSitter (multi-language) + Roslyn (C# deep analysis).

### 3. Agent Framework

Agents are defined in Markdown—human-readable, hot-reloadable:

```markdown
# Coding Agent

## Metadata
- **Provider**: ollama
- **Model**: qwen2.5-coder:7b

## Capabilities
- software-development-csharp
- software-development-typescript

## System Prompt
You are an expert polyglot developer...
```

---

## Developer Workflow (Use Case)

The Developer Module provides a complete workflow for code automation:

```text
CREATE → ANALYZE → PLAN → EXECUTE → COMPLETE
  ↓        ↓         ↓        ↓         ↓
 Issue   RAG +     Steps   Human-in   Commit
         Graph              the-loop   + PR
```

### Workflow Steps

| Phase | What Happens |
|-------|--------------|
| **Create** | User describes task, git worktree created |
| **Analyze** | RAG finds relevant code, agent enriches requirements |
| **Plan** | Business analyst agent generates execution steps |
| **Execute** | Each step executed by appropriate agent |
| **Complete** | User reviews, commits, creates PR |

### Human-in-the-Loop

Each step requires user approval:

| Action | Effect |
|--------|--------|
| **Approve** | Accept output, continue |
| **Reject** | Provide feedback, agent retries |
| **Skip** | Mark skipped, continue |
| **Chat** | Ask questions, request changes |
| **Reassign** | Change to different agent |

---

## Technology Stack

| Layer | Technology |
|-------|------------|
| **Runtime** | .NET 9 |
| **Orchestration** | .NET Aspire |
| **Database** | PostgreSQL + pgvector |
| **Vector Search** | pgvector HNSW |
| **Local LLM** | Ollama |
| **C# Analysis** | Roslyn |
| **Multi-Lang Parse** | TreeSitter |
| **Extension** | VS Code (TypeScript) |

---

## Quick Reference

### Requirements

- .NET 9.0
- Docker (for PostgreSQL) or native PostgreSQL with pgvector
- Ollama (for local LLM)
- GPU recommended (but CPU works)

### Getting Started

```bash
# Clone
git clone https://github.com/johnazariah/aura.git
cd aura

# Build
dotnet build

# Run
dotnet run --project src/Aura.AppHost
```

### Key API Endpoints

```http
# Workflows
POST   /api/developer/workflows              # Create
POST   /api/developer/workflows/{id}/analyze # Enrich with RAG
POST   /api/developer/workflows/{id}/plan    # Generate steps
POST   /api/developer/workflows/{id}/steps/{stepId}/execute

# RAG
POST   /api/rag/search

# Code Graph
GET    /api/graph/find/{name}
GET    /api/graph/implementations/{interface}
```

### Configuration

```json
{
  "Aura": {
    "Modules": {
      "Enabled": ["developer"]
    }
  },
  "LlmProviders": {
    "default": {
      "Provider": "ollama",
      "Model": "llama3:8b"
    }
  }
}
```

---

## Links & Resources

| Resource | URL |
|----------|-----|
| **GitHub** | `github.com/johnazariah/aura` |
| **License** | MIT |
| **Documentation** | `./docs/` in repository |

---

## Key Takeaways

1. **Privacy Matters** - Your data should stay on your machine
2. **Local LLMs Are Ready** - 7B models are good enough for most tasks
3. **RAG + Code Graph** - Text similarity + structural queries
4. **Human-in-the-Loop** - AI assists, you decide
5. **Composable Design** - Enable only what you need

> *"The best software is built not by adding features until it works, but by removing complexity until it can't fail."*
