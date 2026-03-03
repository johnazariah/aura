# ADR-027: Configurable Embedding Providers

## Status
Accepted

## Date
2026-03-03

## Context

Aura's embedding pipeline was hardwired to Ollama (local inference). This works well for privacy and small codebases, but large repositories (~100K+ files) take hours to index because local embedding throughput is ~50 chunks/sec on consumer hardware.

OpenAI's `text-embedding-3-small` API can process ~500+ chunks/sec with batching, at $0.02/1M tokens — making large-scale indexing practical while keeping the indexed vectors local in pgvector.

## Decision

1. **Create `OpenAiEmbeddingProvider`** implementing `IEmbeddingProvider` for OpenAI-compatible APIs (OpenAI, Azure OpenAI, any `/v1/embeddings` endpoint)
2. **Add `EmbeddingOptions`** config with `Provider` selector ("ollama" | "openai") and `BatchSize`
3. **DI auto-selects provider** based on `Aura:Embedding:Provider` configuration
4. **Configurable batching**: `BatchSize` (default 100, OpenAI supports up to 2048), `BatchDelayMs` for rate limiting
5. **Automatic rate limit handling**: 429 responses trigger retry with `Retry-After` header

### Configuration

```json
{
  "Aura": {
    "Embedding": {
      "Provider": "openai",
      "BatchSize": 200
    },
    "Llm": {
      "Providers": {
        "OpenAi": {
          "BaseUrl": "https://api.openai.com/",
          "ApiKey": "sk-...",
          "Model": "text-embedding-3-small",
          "BatchSize": 200,
          "BatchDelayMs": 0,
          "MaxTextLength": 30000
        }
      }
    }
  }
}
```

For Azure OpenAI, change `BaseUrl` to the deployment endpoint — the API is compatible.

## Consequences

### Positive
- ~10x faster indexing for large repositories
- Text chunks sent to cloud API but vectors stored locally — reasonable privacy tradeoff
- Both providers always registered — can switch via config without redeployment
- Batch size tunable per-provider for throughput optimization

### Negative
- Cloud embedding requires API key and internet access
- Cost grows with index size ($0.02/1M tokens ≈ $0.10 per 10K files)
- Different embedding dimensions between models (nomic-embed-text: 768d, text-embedding-3-small: 1536d) — must re-index when switching

### Migration
Switching providers requires re-indexing all workspaces because embedding dimensions differ. Use `aura_index` MCP tool with `index_directory` to trigger re-indexing.

## Relates To
- ADR-008 (Local RAG Foundation) — extended with cloud embedding option
- ADR-025 (Personal Knowledge MCP Pivot) — enables large-scale personal knowledge indexing
