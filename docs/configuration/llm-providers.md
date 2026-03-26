# LLM Providers

Aura uses LLM providers for two purposes: **embeddings** (vector search) and **generation** (code analysis and creation). Embedding and generation can use different providers.

## Embedding Providers

The `Aura:Embedding:Provider` setting controls which provider generates embeddings:

| Value | Behavior |
|-------|----------|
| `auto` (default) | Try OpenAI first; fall back to Ollama if OpenAI is unavailable or unconfigured |
| `ollama` | Always use local Ollama |
| `openai` | Always use OpenAI API |

### Ollama (Local — Recommended for Getting Started)

Ollama runs entirely on your machine. No API keys, no network calls.

**Setup:**

1. Install Ollama from [ollama.com](https://ollama.com/)
2. Pull the embedding model:

```bash
ollama pull nomic-embed-text
```

3. Verify it's running:

```bash
ollama list
curl http://localhost:11434/api/tags
```

**Configuration:**

```json
{
  "Aura": {
    "Embedding": {
      "Provider": "ollama"
    },
    "Llm": {
      "Providers": {
        "Ollama": {
          "BaseUrl": "http://localhost:11434",
          "DefaultEmbeddingModel": "nomic-embed-text",
          "NumGpu": -1
        }
      }
    }
  }
}
```

**GPU settings:**

| `NumGpu` | Meaning |
|----------|---------|
| `-1` | Use all available GPU layers (default) |
| `0` | CPU only |
| `N` | Use N GPU layers |

**Recommended models:**

| Purpose | Model | Size |
|---------|-------|------|
| Embeddings | `nomic-embed-text` | ~270 MB |
| Code generation | `qwen2.5-coder:7b` | ~4 GB |

### OpenAI (Hosted)

Uses the OpenAI API for embeddings. Higher quality on some benchmarks, but requires an API key and network access.

**Configuration:**

```json
{
  "Aura": {
    "Embedding": {
      "Provider": "openai"
    },
    "Llm": {
      "Providers": {
        "OpenAI": {
          "ApiKey": "sk-..."
        }
      }
    }
  }
}
```

Or via environment variable:

```bash
export Aura__Llm__Providers__OpenAI__ApiKey="sk-..."
```

The default embedding model is `text-embedding-3-small` (1536 dimensions). When using OpenAI embeddings, the `Aura:Rag:EmbeddingDimension` should match the model's output dimension.

### Azure OpenAI

If you have an Azure OpenAI resource:

```json
{
  "Aura": {
    "Llm": {
      "DefaultProvider": "AzureOpenAI",
      "Providers": {
        "AzureOpenAI": {
          "Endpoint": "https://my-resource.openai.azure.com/",
          "ApiKey": "...",
          "DefaultDeployment": "gpt-4o"
        }
      }
    }
  }
}
```

## Auto-Failover

When `Aura:Embedding:Provider` is set to `auto` (the default):

1. Aura checks whether an OpenAI API key is configured
2. If yes, it uses OpenAI for embeddings
3. If OpenAI is unavailable or returns errors, it falls back to the `FallbackModel` on Ollama (`nomic-embed-text`)
4. If Ollama is also unavailable, embedding fails and the health endpoint reports unhealthy

This means you can configure both providers and Aura will use the best available option.

## Generation Providers

The `Aura:Llm:DefaultProvider` setting controls which provider handles generation (code analysis, test generation, etc.):

```json
{
  "Aura": {
    "Llm": {
      "DefaultProvider": "Ollama"
    }
  }
}
```

Valid values: `Ollama`, `OpenAI`, `AzureOpenAI`.

## Verifying Provider Health

```bash
# Check RAG health (includes embedding provider status)
curl http://localhost:5300/health/rag

# Check Ollama directly
curl http://localhost:11434/api/tags
ollama list
```

## Switching Providers

To switch from Ollama to OpenAI embeddings:

1. Set the provider:
   ```json
   { "Aura": { "Embedding": { "Provider": "openai" } } }
   ```
2. Configure the API key
3. Restart AuraService
4. Re-index your workspaces (embeddings from different models are not compatible)

> **Important:** When switching embedding providers, you must re-index all workspaces. Vectors from different models live in incompatible embedding spaces.

