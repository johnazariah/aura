# ADR-008: Local RAG as Foundation Component

## Status

Accepted

## Date

2025-11-27

## Context

Aura aims to be a knowledge assistant that understands the user's documents, code, and files. This requires:

1. **Semantic search** - Find relevant content by meaning, not just keywords
2. **Context retrieval** - Provide relevant background to LLM prompts
3. **Knowledge indexing** - Process and store representations of user content

Traditional RAG (Retrieval-Augmented Generation) systems typically rely on:

- Cloud embedding APIs (OpenAI, Cohere, etc.)
- Hosted vector databases (Pinecone, Weaviate Cloud, etc.)

This conflicts with our local-first architecture (ADR-001).

The question: Should RAG be a Foundation component available to all modules, or should each module implement its own retrieval?

## Decision

**RAG is a Foundation component. All embedding and retrieval happens locally.**

### Architecture

```text
┌─────────────────────────────────────────────────────────────┐
│                    Aura.Foundation                           │
│  ┌─────────────────────────────────────────────────────────┐ │
│  │                      RAG Pipeline                        │ │
│  │                                                          │ │
│  │  ┌──────────┐    ┌──────────┐    ┌──────────┐           │ │
│  │  │  Ingest  │───▶│  Embed   │───▶│  Store   │           │ │
│  │  │          │    │ (local)  │    │(pgvector)│           │ │
│  │  └──────────┘    └──────────┘    └──────────┘           │ │
│  │                                                          │ │
│  │  ┌──────────┐    ┌──────────┐    ┌──────────┐           │ │
│  │  │  Query   │───▶│  Search  │───▶│ Retrieve │           │ │
│  │  │          │    │ (vector) │    │          │           │ │
│  │  └──────────┘    └──────────┘    └──────────┘           │ │
│  └─────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

### Local Embedding Options

| Model | Size | Quality | Speed |
|-------|------|---------|-------|
| **nomic-embed-text** | 137M | Good | Fast |
| **all-minilm** | 22M | Moderate | Very Fast |
| **mxbai-embed-large** | 334M | Excellent | Moderate |

Embeddings generated via Ollama (same as LLM inference).

### Vector Storage

PostgreSQL with pgvector extension:

- No additional infrastructure (reuse existing PostgreSQL)
- HNSW indexes for fast approximate nearest neighbor search
- SQL for filtering and hybrid search
- Transactional consistency with other data

### Foundation Services

```csharp
public interface IRagService
{
    // Indexing
    Task IndexDocumentAsync(string path, CancellationToken ct);
    Task IndexDirectoryAsync(string path, bool recursive, CancellationToken ct);
    Task RemoveFromIndexAsync(string path, CancellationToken ct);
    
    // Retrieval
    Task<IReadOnlyList<RagResult>> SearchAsync(
        string query, 
        int topK = 5, 
        CancellationToken ct = default);
    
    // Context building
    Task<string> BuildContextAsync(
        string query, 
        int maxTokens = 2000,
        CancellationToken ct = default);
}
```

Modules use Foundation's RAG service rather than implementing their own.

## Consequences

### Positive

- **Consistent retrieval** - All modules benefit from the same index
- **Single index** - No duplicate storage of embeddings
- **Local privacy** - Embeddings never leave the machine
- **Shared infrastructure** - One PostgreSQL, one embedding model
- **Cross-domain search** - Developer module can find relevant research notes

### Negative

- **Resource usage** - Embedding models consume GPU/CPU memory
- **Index size** - Vector storage grows with content volume
- **Cold start** - Initial indexing takes time
- **Model limitations** - Local embedding models less capable than cloud

### Mitigations

- Lazy indexing (index on first access or background)
- Configurable embedding model (trade quality for speed)
- Index pruning for old/deleted content
- Incremental updates (only re-embed changed files)

## Module Usage

Modules don't implement RAG - they consume it:

```csharp
// Developer module using Foundation RAG
public class CodingAgent(IRagService rag, ILlmProvider llm)
{
    public async Task<string> GenerateCodeAsync(string prompt)
    {
        // Get relevant context from codebase
        var context = await rag.BuildContextAsync(prompt);
        
        // Include in LLM prompt
        var fullPrompt = $"""
            Context from codebase:
            {context}
            
            User request:
            {prompt}
            """;
        
        return await llm.GenerateAsync(fullPrompt, ...);
    }
}
```

## Alternatives Considered

### Cloud Embedding APIs

- **Pros**: Higher quality embeddings, no local compute
- **Cons**: Violates local-first principle, ongoing costs
- **Rejected**: Conflicts with ADR-001

### Per-Module RAG

- **Pros**: Modules fully independent
- **Cons**: Duplicate indexes, inconsistent retrieval, wasted storage
- **Rejected**: Foundation exists to share common capabilities

### No RAG (Keyword Search Only)

- **Pros**: Simpler, faster, no embedding compute
- **Cons**: Poor semantic understanding, misses relevant context
- **Rejected**: Semantic search is core to knowledge assistant value

## References

- [ADR-001: Local-First Architecture](001-local-first-architecture.md)
- [pgvector](https://github.com/pgvector/pgvector) - Vector extension for PostgreSQL
- [Ollama Embeddings](https://ollama.ai/blog/embedding-models) - Local embedding models
- [nomic-embed-text](https://ollama.com/library/nomic-embed-text) - Recommended embedding model
