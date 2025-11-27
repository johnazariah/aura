# ADR-001: Local-First, Privacy-Safe Architecture

## Status

Accepted

## Date

2025-11-25

## Context

We are building an AI-powered system for knowledge work. The landscape of AI assistants is dominated by cloud-based solutions that require:

- API keys and subscriptions
- Uploading user data to third-party servers
- Internet connectivity for all operations
- Trust in provider privacy policies

Users increasingly want AI capabilities without sacrificing privacy. The rise of capable local LLMs (Llama, Qwen, Mistral) running on consumer hardware via tools like Ollama makes this feasible.

The question: Should Aura be cloud-first with local as an option, or local-first with cloud as an option?

## Decision

**Aura is local-first by design. Your data never leaves your machine.**

All core components run locally:

| Component | Implementation |
|-----------|----------------|
| **LLM** | Ollama on local GPU/CPU |
| **RAG** | Local embeddings + pgvector for semantic search |
| **Database** | PostgreSQL in container or native |
| **Vector Store** | pgvector extension (local) |
| **File Access** | Direct filesystem access |
| **Git Operations** | Local repositories and worktrees |

Cloud services are explicitly **opt-in** and **optional**:

- GitHub API access requires explicit authentication
- No telemetry or usage tracking
- No "phone home" behavior
- Works completely offline

## Consequences

### Positive

- **Complete privacy** - User data never leaves the machine
- **Works offline** - No internet dependency for core functionality
- **No API costs** - No per-token charges or subscription fees
- **Full control** - Users can inspect, modify, and extend everything
- **Regulatory compliance** - Easier for users in regulated industries
- **Trust** - Users can verify privacy claims by inspecting network traffic

### Negative

- **Hardware requirements** - Users need capable local hardware for LLM inference
- **No cloud-scale models** - Limited to models that fit on consumer hardware
- **Self-hosting burden** - Users responsible for updates and maintenance
- **No cross-device sync** - Data lives on one machine (by design)

### Mitigations

- Support for smaller, efficient models (7B parameter models run well on modern GPUs)
- Clear hardware requirements in documentation
- Aspire orchestration simplifies local infrastructure
- Future: Optional cloud LLM providers for users who choose convenience over privacy

## Alternatives Considered

### Cloud-First with Local Option

- **Pros**: Simpler setup, more powerful models, cross-device sync
- **Cons**: Privacy concerns, ongoing costs, vendor lock-in
- **Rejected**: Conflicts with core mission of privacy-safe AI

### Hybrid Default (Cloud + Local)

- **Pros**: Best of both worlds
- **Cons**: Complex configuration, unclear privacy guarantees
- **Rejected**: "Privacy by default" is a clearer value proposition

## References

- [Ollama](https://ollama.ai) - Local LLM runtime
- [pgvector](https://github.com/pgvector/pgvector) - Vector similarity for PostgreSQL
- [Local-First Software](https://www.inkandswitch.com/local-first/) - Ink & Switch research
