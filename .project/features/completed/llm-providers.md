# LLM Providers

**Status:** ✅ Complete  
**Completed:** 2025-11-27  
**Last Updated:** 2025-12-12

## Overview

LLM Providers abstract the underlying language model service. This allows agents to use different backends (Ollama, Azure OpenAI, Anthropic) without code changes.

## Provider Interface

```csharp
public interface ILlmProvider
{
    string Name { get; }  // "ollama", "azure-openai", "anthropic"
    
    Task<string> GenerateAsync(
        string prompt, 
        string model, 
        GenerateOptions? options = null,
        CancellationToken ct = default);
    
    Task<string> ChatAsync(
        IEnumerable<ChatMessage> messages, 
        string model,
        ChatOptions? options = null,
        CancellationToken ct = default);
    
    IAsyncEnumerable<string> StreamAsync(
        string prompt, 
        string model,
        GenerateOptions? options = null,
        CancellationToken ct = default);
    
    Task<bool> IsModelAvailableAsync(string model, CancellationToken ct = default);
    Task<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken ct = default);
}

public record ChatMessage(string Role, string Content);  // "system", "user", "assistant"

public record GenerateOptions
{
    public float Temperature { get; init; } = 0.7f;
    public int? MaxTokens { get; init; }
    public float? TopP { get; init; }
    public string[]? Stop { get; init; }
}

public record ChatOptions : GenerateOptions
{
    public string? SystemPrompt { get; init; }
}

public record ModelInfo(string Name, long? ParameterCount, string[]? Capabilities);
```

## Provider Registry

Routes requests to the appropriate provider based on agent configuration.

```csharp
public interface ILlmProviderRegistry
{
    void Register(ILlmProvider provider);
    ILlmProvider? GetProvider(string name);
    ILlmProvider GetProviderOrDefault(string? name);  // Falls back to default
    IReadOnlyList<ILlmProvider> GetAll();
}
```

## Built-in Providers

### 1. Ollama (Default)

Local LLM inference via Ollama.

```csharp
public class OllamaProvider : ILlmProvider
{
    public string Name => "ollama";
    
    private readonly HttpClient _client;
    private readonly string _baseUrl;  // Default: http://localhost:11434
    
    public async Task<string> GenerateAsync(string prompt, string model, ...)
    {
        var response = await _client.PostAsJsonAsync($"{_baseUrl}/api/generate", new
        {
            model,
            prompt,
            stream = false,
            options = new { temperature = options?.Temperature ?? 0.7f }
        });
        
        var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();
        return result.Response;
    }
}
```

**Configuration:**
```json
{
  "LlmProviders": {
    "Ollama": {
      "BaseUrl": "http://localhost:11434",
      "DefaultModel": "qwen2.5-coder:7b"
    }
  }
}
```

### 2. Azure OpenAI

Azure-hosted OpenAI models.

```csharp
public class AzureOpenAIProvider : ILlmProvider
{
    public string Name => "azure-openai";
    
    private readonly OpenAIClient _client;  // From Azure.AI.OpenAI SDK
    
    public async Task<string> GenerateAsync(string prompt, string model, ...)
    {
        var response = await _client.GetChatCompletionsAsync(new ChatCompletionsOptions
        {
            DeploymentName = model,
            Messages = { new ChatRequestUserMessage(prompt) },
            Temperature = options?.Temperature ?? 0.7f
        });
        
        return response.Value.Choices[0].Message.Content;
    }
}
```

**Configuration:**
```json
{
  "LlmProviders": {
    "AzureOpenAI": {
      "Endpoint": "https://your-resource.openai.azure.com/",
      "ApiKey": "your-api-key",
      "DefaultModel": "gpt-4o"
    }
  }
}
```

### 3. Anthropic (Claude)

Direct Anthropic API.

```csharp
public class AnthropicProvider : ILlmProvider
{
    public string Name => "anthropic";
    
    public async Task<string> GenerateAsync(string prompt, string model, ...)
    {
        // Uses Anthropic.SDK or direct HTTP
        var response = await _client.CreateMessageAsync(new MessageRequest
        {
            Model = model,  // "claude-sonnet-4-20250514"
            MaxTokens = options?.MaxTokens ?? 4096,
            Messages = [new Message { Role = "user", Content = prompt }]
        });
        
        return response.Content[0].Text;
    }
}
```

**Configuration:**
```json
{
  "LlmProviders": {
    "Anthropic": {
      "ApiKey": "your-api-key",
      "DefaultModel": "claude-sonnet-4-20250514"
    }
  }
}
```

### 4. Semantic Kernel (Microsoft Agent Framework)

Wraps Microsoft Semantic Kernel for access to multiple backends.

```csharp
public class SemanticKernelProvider : ILlmProvider
{
    public string Name => "semantic-kernel";
    
    private readonly Kernel _kernel;
    
    public async Task<string> GenerateAsync(string prompt, string model, ...)
    {
        // Kernel routes to configured backend (Azure, OpenAI, etc.)
        var result = await _kernel.InvokePromptAsync(prompt);
        return result.GetValue<string>() ?? "";
    }
}
```

## Provider Selection

Agents specify their preferred provider in metadata:

```markdown
# Premium Coding Agent

## Metadata

- **Provider**: azure-openai
- **Model**: gpt-4o
```

The registry resolves at execution time:

```csharp
// In ConfigurableAgent
public async Task<AgentResult> ExecuteAsync(AgentContext context, CancellationToken ct)
{
    var provider = _providerRegistry.GetProviderOrDefault(Metadata.Provider);
    var response = await provider.GenerateAsync(_prompt, Metadata.Model, ct: ct);
    return new AgentResult { Success = true, Output = response };
}
```

## Fallback Behavior

If requested provider unavailable:

1. Log warning
2. Fall back to default provider (Ollama)
3. Continue execution

```csharp
public ILlmProvider GetProviderOrDefault(string? name)
{
    if (name is not null && _providers.TryGetValue(name, out var provider))
        return provider;
    
    _logger.LogWarning("Provider '{Name}' not found, using default", name);
    return _providers["ollama"];  // Default always registered
}
```

## Cost and Rate Limiting

Future consideration: Track usage and costs per provider.

```csharp
public interface ILlmProvider
{
    // Existing methods...
    
    // Future: Cost tracking
    Task<UsageStats> GetUsageAsync(DateRange range);
}

public record UsageStats(
    int TotalRequests,
    int TotalTokens,
    decimal EstimatedCost
);
```

## Testing Support

Mock provider for testing:

```csharp
public class MockLlmProvider : ILlmProvider
{
    public string Name => "mock";
    
    private readonly Dictionary<string, string> _responses = new();
    
    public void SetResponse(string promptContains, string response)
    {
        _responses[promptContains] = response;
    }
    
    public Task<string> GenerateAsync(string prompt, string model, ...)
    {
        var match = _responses.FirstOrDefault(kv => prompt.Contains(kv.Key));
        return Task.FromResult(match.Value ?? "Mock response");
    }
}
```

## Open Questions

1. **Model aliasing** - Should we support `model: "fast"` → `qwen2.5-coder:7b`?
2. **Retry policy** - Per-provider retry configuration?
3. **Token counting** - Pre-flight token estimation to avoid failures?
4. **Caching** - Cache identical prompts for cost savings?
