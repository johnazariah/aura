# Settings Reference

Aura is configured through `appsettings.json` (or environment variables). The API listens on `http://localhost:5300` by default.

## Connection Strings

```json
{
  "ConnectionStrings": {
    "auradb": "Host=localhost;Port=5433;Database=auradb;Username=postgres"
  }
}
```

The Windows installer uses port **5433** to avoid conflicts with existing PostgreSQL installations. macOS defaults to port **5432**.

## Aura:Embedding

Controls which provider generates vector embeddings.

```json
{
  "Aura": {
    "Embedding": {
      "Provider": "auto",
      "BatchSize": 100,
      "FallbackModel": "nomic-embed-text"
    }
  }
}
```

| Key | Default | Description |
|-----|---------|-------------|
| `Provider` | `auto` | `auto` (try OpenAI, fall back to Ollama), `ollama`, or `openai` |
| `BatchSize` | `100` | Number of chunks embedded per batch |
| `FallbackModel` | `nomic-embed-text` | Model used when the primary provider is unavailable |

## Aura:Rag

RAG indexing and search parameters.

```json
{
  "Aura": {
    "Rag": {
      "EmbeddingModel": "nomic-embed-text",
      "EmbeddingDimension": 768,
      "ChunkSize": 2000,
      "ChunkOverlap": 200,
      "DefaultTopK": 5,
      "MinRelevanceScore": 0.3
    }
  }
}
```

| Key | Default | Description |
|-----|---------|-------------|
| `EmbeddingModel` | `nomic-embed-text` | Model name for embeddings |
| `EmbeddingDimension` | `768` | Vector dimension (must match the model) |
| `ChunkSize` | `2000` | Maximum characters per chunk |
| `ChunkOverlap` | `200` | Overlap between adjacent chunks |
| `DefaultTopK` | `5` | Default number of search results |
| `MinRelevanceScore` | `0.3` | Minimum similarity score to return |

## Aura:Llm

LLM provider configuration for code generation and analysis tasks.

```json
{
  "Aura": {
    "Llm": {
      "DefaultProvider": "Ollama",
      "Providers": {
        "Ollama": {
          "BaseUrl": "http://localhost:11434",
          "DefaultModel": "qwen2.5-coder:7b",
          "DefaultEmbeddingModel": "nomic-embed-text",
          "TimeoutSeconds": 300,
          "NumGpu": -1,
          "MaxEmbeddingTextLength": 30000
        },
        "OpenAI": {
          "ApiKey": "",
          "DefaultModel": "gpt-4o",
          "MaxTokens": 4096,
          "TimeoutSeconds": 120
        },
        "AzureOpenAI": {
          "Endpoint": "",
          "ApiKey": "",
          "DefaultDeployment": "gpt-4o",
          "MaxTokens": 4096,
          "TimeoutSeconds": 300
        }
      }
    }
  }
}
```

### Ollama Provider

| Key | Default | Description |
|-----|---------|-------------|
| `BaseUrl` | `http://localhost:11434` | Ollama API endpoint |
| `DefaultModel` | `qwen2.5-coder:7b` | Model for generation tasks |
| `DefaultEmbeddingModel` | `nomic-embed-text` | Model for embeddings |
| `TimeoutSeconds` | `300` | Request timeout |
| `NumGpu` | `-1` | GPU layers (`-1` = all, `0` = CPU only) |
| `MaxEmbeddingTextLength` | `30000` | Max input characters for embedding |

### OpenAI Provider

| Key | Default | Description |
|-----|---------|-------------|
| `ApiKey` | *(empty)* | OpenAI API key |
| `DefaultModel` | `gpt-4o` | Model for generation tasks |
| `MaxTokens` | `4096` | Max response tokens |
| `TimeoutSeconds` | `120` | Request timeout |

### Azure OpenAI Provider

| Key | Default | Description |
|-----|---------|-------------|
| `Endpoint` | *(empty)* | Azure OpenAI endpoint URL |
| `ApiKey` | *(empty)* | Azure OpenAI API key |
| `DefaultDeployment` | `gpt-4o` | Deployment name |
| `MaxTokens` | `4096` | Max response tokens |
| `TimeoutSeconds` | `300` | Request timeout |

## Researcher Module

```json
{
  "Researcher": {
    "StoragePath": "~/.aura/research",
    "AutoDownloadPdfs": true,
    "DefaultEnhancementLevel": "Basic",
    "SemanticScholarApiKey": null
  }
}
```

| Key | Default | Description |
|-----|---------|-------------|
| `StoragePath` | `~/.aura/research` | Where downloaded PDFs are stored |
| `AutoDownloadPdfs` | `true` | Automatically download linked PDFs |
| `DefaultEnhancementLevel` | `Basic` | PDF enhancement level |
| `SemanticScholarApiKey` | `null` | Optional API key for Semantic Scholar |

## Kestrel (HTTP Server)

```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://localhost:5300"
      }
    }
  }
}
```

## Logging

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Aura": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

Override per-component for debugging:

```json
{
  "Logging": {
    "LogLevel": {
      "Aura.Foundation.Rag": "Debug",
      "Aura.Module.Developer": "Debug"
    }
  }
}
```

### Log Locations

| Platform | Location |
|----------|----------|
| Windows (service) | `C:\ProgramData\Aura\logs\aura-YYYYMMDD.log` + Windows Event Log |
| Windows (console) | stdout |
| macOS | `/usr/local/var/log/aura/` or `~/.local/share/Aura/logs/` |

## Environment Variables

Any setting can be overridden via environment variables using the `__` separator:

```powershell
$env:ConnectionStrings__auradb = "Host=localhost;Port=5433;Database=auradb;Username=postgres"
$env:Aura__Embedding__Provider = "ollama"
$env:Aura__Llm__Providers__OpenAI__ApiKey = "sk-..."
```

