# Configuring LLM Providers

Aura supports multiple LLM providers for AI capabilities. You can use local models with Ollama or cloud providers for better quality.

## Available Providers

| Provider | Type | Best For |
|----------|------|----------|

| **Ollama** | Local | Privacy, offline use, no API costs |
| **Azure OpenAI** | Cloud | Enterprise, compliance, GPT-4 quality |
| **OpenAI** | Cloud | Best quality, easy setup |

## Ollama (Default)

Ollama runs models locally on your machine.

### Setup

1. Install Ollama from [ollama.com](https://ollama.com)
2. Pull a model:

   ```powershell
   ollama pull qwen2.5-coder:7b
   ollama pull nomic-embed-text
   ```

3. Ollama is auto-detected by Aura

### Configuration

In `C:\Program Files\Aura\api\appsettings.json`:

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
          "TimeoutSeconds": 300
        }
      }
    }
  }
}
```

### Recommended Models

| Model | Size | Use Case |
|-------|------|----------|

| `qwen2.5-coder:7b` | ~4GB | General coding (recommended) |
| `qwen2.5-coder:14b` | ~8GB | Better quality, needs 16GB+ RAM |
| `codellama:7b` | ~4GB | Alternative coding model |
| `llama3.2:3b` | ~2GB | Faster, less capable |

For embeddings:

| Model | Size | Use Case |
|-------|------|----------|

| `nomic-embed-text` | ~275MB | Best quality (recommended) |
| `all-minilm` | ~45MB | Smaller, faster |

## Azure OpenAI

Use Azure's hosted OpenAI models for enterprise scenarios.

### Prerequisites

1. Azure subscription
2. Azure OpenAI resource created
3. Model deployment (e.g., gpt-4o)
4. API key or managed identity

### Configuration

```json
{
  "Aura": {
    "Llm": {
      "DefaultProvider": "AzureOpenAI",
      "Providers": {
        "AzureOpenAI": {
          "Endpoint": "https://your-resource.openai.azure.com/",
          "ApiKey": "your-api-key",
          "DefaultDeployment": "gpt-4o",
          "MaxTokens": 4096,
          "TimeoutSeconds": 120
        }
      }
    }
  }
}
```

### Using Managed Identity

For Azure VMs or App Service:

```json
{
  "Aura": {
    "Llm": {
      "Providers": {
        "AzureOpenAI": {
          "Endpoint": "https://your-resource.openai.azure.com/",
          "UseAzureAD": true,
          "DefaultDeployment": "gpt-4o"
        }
      }
    }
  }
}
```

## OpenAI

Use OpenAI's API directly.

### Prerequisites

1. OpenAI account
2. API key from [platform.openai.com](https://platform.openai.com)

### Configuration

```json
{
  "Aura": {
    "Llm": {
      "DefaultProvider": "OpenAI",
      "Providers": {
        "OpenAI": {
          "ApiKey": "sk-your-api-key",
          "DefaultModel": "gpt-4o",
          "MaxTokens": 4096,
          "TimeoutSeconds": 120
        }
      }
    }
  }
}
```

### Available Models

| Model | Quality | Speed | Cost |
|-------|---------|-------|------|

| `gpt-4o` | Best | Fast | $$ |
| `gpt-4o-mini` | Good | Fastest | $ |
| `gpt-4-turbo` | Best | Slower | $$$ |

## Multiple Providers

You can configure multiple providers and Aura will fall back:

```json
{
  "Aura": {
    "Llm": {
      "DefaultProvider": "AzureOpenAI",
      "FallbackProviders": ["Ollama"],
      "Providers": {
        "AzureOpenAI": { ... },
        "Ollama": { ... }
      }
    }
  }
}
```

If Azure is unavailable, Aura uses Ollama.

## Choosing a Provider

| Scenario | Recommended |
|----------|-------------|

| **Privacy-critical** | Ollama |
| **Offline use** | Ollama |
| **Best quality** | Azure OpenAI / OpenAI |
| **Enterprise compliance** | Azure OpenAI |
| **Low budget** | Ollama |
| **Mixed use** | Azure + Ollama fallback |

## Applying Changes

After editing `appsettings.json`:

1. Restart Aura service:

   ```powershell
   Restart-Service AuraService
   ```

2. Or restart from system tray:
   - Right-click Aura tray icon
   - Click "Restart"

## Security Best Practices

### API Keys

- Never commit API keys to source control
- Use environment variables for CI/CD:

  ```json
  {
    "Aura": {
      "Llm": {
        "Providers": {
          "OpenAI": {
            "ApiKey": "${OPENAI_API_KEY}"
          }
        }
      }
    }
  }
  ```

### Azure Managed Identity

Prefer managed identity over API keys when running on Azure.

### Local Development

For local development, use `appsettings.Local.json` (git-ignored):

```json
{
  "Aura": {
    "Llm": {
      "Providers": {
        "AzureOpenAI": {
          "ApiKey": "your-dev-key"
        }
      }
    }
  }
}
```
