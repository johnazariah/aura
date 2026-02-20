# ADR-024: Hybrid Architecture — Local Infrastructure with Cloud LLM

## Status

Accepted (supersedes [ADR-001](001-local-first-architecture.md))

## Date

2026-02-20

## Context

ADR-001 (November 2025) established Aura as "local-first by design" where "your data never leaves your machine." This was accurate at the time — the only LLM provider was Ollama running locally.

Since then, three changes have made that description inaccurate:

1. **Cloud LLM providers became default** (Dec 2025) — Azure OpenAI and OpenAI were added as providers, and Azure OpenAI became the default in `appsettings.json`.
2. **GitHub Copilot CLI became the execution engine** (Jan 2026) — Story steps are executed via GitHub Copilot CLI, requiring internet connectivity and a Copilot subscription.
3. **Internal agents were removed** (Feb 6, 2026) — 7,093 lines of internal agent execution code were deleted. "Copilot Chat + MCP is now the only execution path" (STATUS.md).

The result is that Aura's core workflow — creating a story, planning steps, and executing code changes — requires internet connectivity and sends code context to cloud LLMs. Describing this as "local-first" or "privacy-safe" is misleading.

## Decision

**Aura is a hybrid system: local infrastructure with cloud-hosted LLM inference.**

### What remains local

| Component | Implementation |
|-----------|---------------|
| **Database** | PostgreSQL (container or native) |
| **Vector index** | pgvector for semantic search |
| **Code graph** | Roslyn (C#) and TreeSitter (polyglot) — built and queried locally |
| **RAG pipeline** | Embeddings generated and stored locally |
| **Git operations** | Local repositories, worktrees, branches |
| **Agent definitions** | Markdown files, hot-reloadable, stored on disk |
| **File access** | Direct filesystem — no remote file storage |

### What is cloud-hosted

| Component | Implementation | Data sent |
|-----------|---------------|-----------|
| **LLM inference** | Azure OpenAI (default), OpenAI, or Ollama (opt-in local) | Prompts containing code snippets, RAG context, user queries |
| **Code execution** | GitHub Copilot CLI (YOLO mode) | Step instructions, file contents, tool call results |
| **GitHub integration** | GitHub API | Issue data, PR metadata, commit info |

### Privacy model

- **Code index stays local** — the database, vector store, and code graph never leave the machine.
- **Prompts are sent to cloud LLMs** — this includes code snippets retrieved by RAG, user queries, and agent instructions.
- **Ollama remains a supported provider** — users who want fully local inference can configure it, but Copilot-mediated execution still requires internet.
- **No telemetry or tracking** — Aura does not phone home or collect usage data.

## Consequences

### Positive

- **Honest positioning** — documentation matches reality
- **Better model quality** — cloud LLMs (GPT-4o) significantly outperform local 7B models for code generation
- **Simpler setup** — users don't need a GPU capable of running local models
- **Focus on value** — the local infrastructure (code graph, RAG, git worktrees) is Aura's differentiator, not the LLM itself

### Negative

- **Internet required** — core workflow doesn't work offline
- **API costs** — cloud LLM usage incurs per-token charges
- **Privacy trade-off** — code context is sent to cloud providers
- **Copilot dependency** — story execution requires a GitHub Copilot subscription

### Mitigations

- Ollama provider is maintained for users who prefer local inference
- Azure OpenAI deployments can be in the user's own Azure tenant for data residency
- The MCP architecture means Aura's tools are reusable with any Copilot-compatible host

## Alternatives Considered

### Keep the "local-first" branding with disclaimers

- **Pros**: No documentation/marketing disruption
- **Cons**: Misleading, erodes trust when users discover cloud dependencies
- **Rejected**: Honesty is a better foundation for trust than asterisks

### Remove cloud LLM support and return to local-only

- **Pros**: Would make local-first claims accurate again
- **Cons**: Major regression in model quality, eliminates Copilot integration
- **Rejected**: The Copilot/MCP integration is core to the product's value

## Impact on ADR-001

ADR-001 is now superseded. Its status should be updated to: `Superseded by ADR-024`.

The local infrastructure principles from ADR-001 remain valid — Aura's database, index, code graph, and git operations are still fully local. What changed is the LLM layer and execution engine.

## References

- [ADR-001: Local-First Architecture](001-local-first-architecture.md) (now superseded)
- [ADR-023: MCP Over Copilot SDK](023-mcp-over-copilot-sdk.md)
- [STATUS.md — Feb 6, 2026 entry](../STATUS.md) (internal agent removal)
