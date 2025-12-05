# LLM Providers Specification

**Status**: ✅ Implemented
**Created**: 2025-12-05
**Last Updated**: 2025-12-05

## Overview

Aura supports multiple LLM providers to enable flexibility between local-first privacy and cloud-based performance. The system allows configuring different providers per agent or using a global default.

## Supported Providers

| Provider | Type | Status | Use Case |
|----------|------|--------|----------|
| **Ollama** | Local | ✅ Default | Privacy-first, offline, no cost |
| **Azure OpenAI** | Cloud | ✅ Implemented | Enterprise, high quality |
| **OpenAI** | Cloud | ✅ Implemented | Direct access, latest models |

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Agent Execution                           │
│  ConfigurableAgent → ILlmProvider → Provider Implementation  │
└─────────────────────────────────────────────────────────────┘
                              │
         ┌────────────────────┼────────────────────┐
         ▼                    ▼                    ▼
  ┌─────────────┐    ┌──────────────┐    ┌──────────────┐
  │   Ollama    │    │ Azure OpenAI │    │    OpenAI    │
  │  (Local)    │    │   (Cloud)    │    │   (Cloud)    │
  └─────────────┘    └──────────────┘    └──────────────┘
```

## Configuration

### appsettings.json

```json
{
  "LlmProviders": {
    "default": {
      "Provider": "ollama",
      "Endpoint": "http://localhost:11434",
      "Model": "llama3:8b"
    },
    "cloud": {
      "Provider": "azureopenai",
      "Endpoint": "https://your-resource.openai.azure.com/",
      "ApiKey": "your-api-key",
      "DeploymentName": "gpt-4o-mini"
    },
    "openai": {
      "Provider": "openai",
      "ApiKey": "sk-your-api-key",
      "Model": "gpt-4o"
    }
  }
}
```

### Environment Variables (Recommended for Secrets)

```bash
# Azure OpenAI
export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/"
export AZURE_OPENAI_API_KEY="your-api-key"

# OpenAI
export OPENAI_API_KEY="sk-your-api-key"
```

## Provider Selection

### Per-Agent Configuration

Agents can specify their preferred provider in their definition:

```markdown
# My Agent

## Metadata

- **Provider**: azureopenai
- **Model**: gpt-4o

## System Prompt
...
```

### Fallback Chain

1. Agent-specific provider (if specified)
2. Named provider from config (e.g., `cloud`)
3. Default provider (`LlmProviders:default`)
4. Ollama at `localhost:11434`

## Provider Implementations

### Ollama Provider

- **Location**: `src/Aura.Foundation/Llm/OllamaLlmProvider.cs`
- **Default**: Yes
- **Features**:
  - Local execution (privacy-safe)
  - No API key required
  - Model auto-download
  - Streaming support

### Azure OpenAI Provider

- **Location**: `src/Aura.Foundation/Llm/AzureOpenAILlmProvider.cs`
- **Authentication**: API Key or Azure AD (future)
- **Features**:
  - Enterprise compliance
  - Regional deployment
  - Content filtering
  - Streaming support

### OpenAI Provider

- **Location**: `src/Aura.Foundation/Llm/OpenAILlmProvider.cs`
- **Authentication**: API Key
- **Features**:
  - Latest models (GPT-4, GPT-4o)
  - High rate limits
  - Streaming support

## Interface

```csharp
public interface ILlmProvider
{
    string ProviderId { get; }
    
    Task<string> GenerateAsync(
        string prompt,
        string? systemPrompt = null,
        LlmOptions? options = null,
        CancellationToken ct = default);
    
    IAsyncEnumerable<string> StreamAsync(
        string prompt,
        string? systemPrompt = null,
        LlmOptions? options = null,
        CancellationToken ct = default);
}

public record LlmOptions
{
    public string? Model { get; init; }
    public float Temperature { get; init; } = 0.7f;
    public int? MaxTokens { get; init; }
    public float? TopP { get; init; }
}
```

## Usage in Code

```csharp
// Inject the provider registry
public class MyService(ILlmProviderRegistry providers)
{
    public async Task DoWorkAsync()
    {
        // Get default provider
        var llm = providers.GetProvider("default");
        
        // Or get specific provider
        var azure = providers.GetProvider("azureopenai");
        
        // Generate
        var response = await llm.GenerateAsync(
            prompt: "Explain this code...",
            systemPrompt: "You are a helpful coding assistant.",
            options: new LlmOptions { Temperature = 0.3f });
    }
}
```

## Cost Considerations

| Provider | Cost Model | Typical Cost |
|----------|-----------|--------------|
| Ollama | Free (local compute) | $0 |
| Azure OpenAI | Per 1K tokens | ~$0.002-0.06 |
| OpenAI | Per 1K tokens | ~$0.002-0.06 |

**Recommendation**: Use Ollama for development/testing, cloud for production quality.

## Security

### API Keys

- Never commit API keys to source control
- Use environment variables or Azure Key Vault
- Rotate keys periodically

### Data Privacy

- Ollama: All data stays local
- Azure OpenAI: Data processed in your Azure region
- OpenAI: Data sent to OpenAI servers (check their data policy)

## Related Files

| File | Purpose |
|------|---------|
| `src/Aura.Foundation/Llm/ILlmProvider.cs` | Provider interface |
| `src/Aura.Foundation/Llm/ILlmProviderRegistry.cs` | Registry interface |
| `src/Aura.Foundation/Llm/LlmProviderRegistry.cs` | Implementation |
| `src/Aura.Foundation/Llm/OllamaLlmProvider.cs` | Ollama implementation |
| `src/Aura.Foundation/Llm/AzureOpenAILlmProvider.cs` | Azure implementation |
| `src/Aura.Foundation/Llm/OpenAILlmProvider.cs` | OpenAI implementation |
| `src/Aura.Api/appsettings.json` | Configuration |

## Implementation Status

- [x] Ollama provider (default, local-first)
- [x] Azure OpenAI provider
- [x] OpenAI provider  
- [x] Provider registry with fallback
- [x] Per-agent provider selection
- [x] Configuration via appsettings.json
- [x] Environment variable support
- [ ] Azure AD authentication (future)
- [ ] Usage/cost tracking (future)
- [ ] Rate limiting (future)

## Commits

- `7f8f34a` - Add Azure OpenAI and OpenAI providers for cloud LLM support
- `79d6825` - WIP: Centralize LLM provider configuration
