# Phase 2: LLM Providers

**Duration:** 1-2 hours  
**Dependencies:** Phase 1 (Core Infrastructure)  
**Output:** LLM provider abstraction with Ollama implementation

## Objective

Create the LLM provider abstraction layer that agents use to communicate with language models.

## Tasks

### 2.1 Create LLM Folder Structure

```
src/Aura/
└── LLM/
    ├── ILlmProvider.cs
    ├── ILlmProviderRegistry.cs
    ├── LlmProviderRegistry.cs
    ├── GenerateOptions.cs
    ├── ChatMessage.cs
    └── Providers/
        ├── OllamaProvider.cs
        └── MockLlmProvider.cs
```

### 2.2 Define Provider Interface

**ILlmProvider.cs:**
```csharp
namespace Aura.LLM;

public interface ILlmProvider
{
    string Name { get; }
    
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
    
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct = default);
}
```

**GenerateOptions.cs:**
```csharp
namespace Aura.LLM;

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

public record ChatMessage(string Role, string Content);
```

### 2.3 Implement Provider Registry

**ILlmProviderRegistry.cs:**
```csharp
namespace Aura.LLM;

public interface ILlmProviderRegistry
{
    void Register(ILlmProvider provider);
    ILlmProvider? GetProvider(string name);
    ILlmProvider GetProviderOrDefault(string? name);
    IReadOnlyList<ILlmProvider> GetAll();
}
```

**LlmProviderRegistry.cs:**
```csharp
namespace Aura.LLM;

public class LlmProviderRegistry : ILlmProviderRegistry
{
    private readonly Dictionary<string, ILlmProvider> _providers = 
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<LlmProviderRegistry> _logger;
    private readonly string _defaultProvider;
    
    public LlmProviderRegistry(
        ILogger<LlmProviderRegistry> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _defaultProvider = configuration["LlmProviders:Default"] ?? "ollama";
    }
    
    public void Register(ILlmProvider provider)
    {
        _providers[provider.Name] = provider;
        _logger.LogInformation("Registered LLM provider: {Name}", provider.Name);
    }
    
    public ILlmProvider? GetProvider(string name)
    {
        return _providers.GetValueOrDefault(name);
    }
    
    public ILlmProvider GetProviderOrDefault(string? name)
    {
        if (name is not null && _providers.TryGetValue(name, out var provider))
            return provider;
        
        if (name is not null)
            _logger.LogWarning("Provider '{Name}' not found, using default", name);
        
        return _providers[_defaultProvider];
    }
    
    public IReadOnlyList<ILlmProvider> GetAll() => _providers.Values.ToList();
}
```

### 2.4 Implement Ollama Provider

**OllamaProvider.cs:**
```csharp
namespace Aura.LLM.Providers;

public class OllamaProvider : ILlmProvider
{
    public string Name => "ollama";
    
    private readonly HttpClient _client;
    private readonly string _baseUrl;
    private readonly ILogger<OllamaProvider> _logger;
    
    public OllamaProvider(
        HttpClient client,
        IConfiguration configuration,
        ILogger<OllamaProvider> logger)
    {
        _client = client;
        _baseUrl = configuration["LlmProviders:Ollama:BaseUrl"] ?? "http://localhost:11434";
        _logger = logger;
    }
    
    public async Task<string> GenerateAsync(
        string prompt,
        string model,
        GenerateOptions? options = null,
        CancellationToken ct = default)
    {
        var request = new
        {
            model,
            prompt,
            stream = false,
            options = new
            {
                temperature = options?.Temperature ?? 0.7f,
                num_predict = options?.MaxTokens
            }
        };
        
        var response = await _client.PostAsJsonAsync(
            $"{_baseUrl}/api/generate", 
            request, 
            ct);
        
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(ct);
        return result?.Response ?? "";
    }
    
    public async Task<string> ChatAsync(
        IEnumerable<ChatMessage> messages,
        string model,
        ChatOptions? options = null,
        CancellationToken ct = default)
    {
        var request = new
        {
            model,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }),
            stream = false,
            options = new
            {
                temperature = options?.Temperature ?? 0.7f
            }
        };
        
        var response = await _client.PostAsJsonAsync(
            $"{_baseUrl}/api/chat",
            request,
            ct);
        
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(ct);
        return result?.Message?.Content ?? "";
    }
    
    public async IAsyncEnumerable<string> StreamAsync(
        string prompt,
        string model,
        GenerateOptions? options = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var request = new
        {
            model,
            prompt,
            stream = true,
            options = new { temperature = options?.Temperature ?? 0.7f }
        };
        
        var response = await _client.PostAsJsonAsync(
            $"{_baseUrl}/api/generate",
            request,
            ct);
        
        response.EnsureSuccessStatusCode();
        
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        
        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrEmpty(line)) continue;
            
            var chunk = JsonSerializer.Deserialize<OllamaStreamChunk>(line);
            if (chunk?.Response is not null)
                yield return chunk.Response;
        }
    }
    
    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _client.GetAsync($"{_baseUrl}/api/tags", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct = default)
    {
        var response = await _client.GetFromJsonAsync<OllamaTagsResponse>(
            $"{_baseUrl}/api/tags", ct);
        
        return response?.Models?.Select(m => m.Name).ToList() ?? [];
    }
    
    // Response DTOs
    private record OllamaGenerateResponse(string Response);
    private record OllamaChatResponse(OllamaChatMessage? Message);
    private record OllamaChatMessage(string Role, string Content);
    private record OllamaStreamChunk(string? Response, bool Done);
    private record OllamaTagsResponse(List<OllamaModel>? Models);
    private record OllamaModel(string Name);
}
```

### 2.5 Implement Mock Provider (for testing)

**MockLlmProvider.cs:**
```csharp
namespace Aura.LLM.Providers;

public class MockLlmProvider : ILlmProvider
{
    public string Name => "mock";
    
    private readonly Dictionary<string, string> _responses = new();
    private readonly List<GenerateCall> _calls = new();
    
    public record GenerateCall(string Prompt, string Model, DateTime Timestamp);
    
    public void SetResponse(string promptContains, string response)
    {
        _responses[promptContains] = response;
    }
    
    public IReadOnlyList<GenerateCall> GetCalls() => _calls.ToList();
    
    public Task<string> GenerateAsync(
        string prompt,
        string model,
        GenerateOptions? options = null,
        CancellationToken ct = default)
    {
        _calls.Add(new(prompt, model, DateTime.UtcNow));
        
        var match = _responses.FirstOrDefault(kv => 
            prompt.Contains(kv.Key, StringComparison.OrdinalIgnoreCase));
        
        return Task.FromResult(match.Value ?? "Mock response for: " + prompt[..50]);
    }
    
    public Task<string> ChatAsync(
        IEnumerable<ChatMessage> messages,
        string model,
        ChatOptions? options = null,
        CancellationToken ct = default)
    {
        var lastMessage = messages.LastOrDefault()?.Content ?? "";
        return GenerateAsync(lastMessage, model, options, ct);
    }
    
    public async IAsyncEnumerable<string> StreamAsync(
        string prompt,
        string model,
        GenerateOptions? options = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var response = await GenerateAsync(prompt, model, options, ct);
        foreach (var word in response.Split(' '))
        {
            yield return word + " ";
            await Task.Delay(10, ct);
        }
    }
    
    public Task<bool> IsAvailableAsync(CancellationToken ct = default) => 
        Task.FromResult(true);
    
    public Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct = default) => 
        Task.FromResult<IReadOnlyList<string>>(["mock-model"]);
}
```

### 2.6 DI Registration Extension

**ServiceCollectionExtensions.cs:**
```csharp
namespace Aura.LLM;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAuraLlm(this IServiceCollection services)
    {
        services.AddSingleton<ILlmProviderRegistry, LlmProviderRegistry>();
        
        // Ollama provider
        services.AddHttpClient<OllamaProvider>();
        services.AddSingleton<ILlmProvider, OllamaProvider>(sp =>
        {
            var provider = sp.GetRequiredService<OllamaProvider>();
            var registry = sp.GetRequiredService<ILlmProviderRegistry>();
            registry.Register(provider);
            return provider;
        });
        
        return services;
    }
    
    public static IServiceCollection AddMockLlm(this IServiceCollection services)
    {
        var mock = new MockLlmProvider();
        services.AddSingleton(mock);
        services.AddSingleton<ILlmProvider>(mock);
        services.AddSingleton<ILlmProviderRegistry>(sp =>
        {
            var registry = new LlmProviderRegistry(
                sp.GetRequiredService<ILogger<LlmProviderRegistry>>(),
                sp.GetRequiredService<IConfiguration>());
            registry.Register(mock);
            return registry;
        });
        
        return services;
    }
}
```

### 2.7 Add Unit Tests

```csharp
public class LlmProviderRegistryTests
{
    [Fact]
    public void GetProviderOrDefault_UnknownProvider_ReturnsDefault() { ... }
    
    [Fact]
    public void GetProvider_KnownProvider_ReturnsProvider() { ... }
}

public class OllamaProviderTests
{
    [Fact]
    public async Task GenerateAsync_ValidRequest_ReturnsResponse() { ... }
    
    [Fact]
    public async Task IsAvailableAsync_ServerDown_ReturnsFalse() { ... }
}

public class MockLlmProviderTests
{
    [Fact]
    public async Task GenerateAsync_RecordsCalls() { ... }
    
    [Fact]
    public async Task SetResponse_MatchesContains() { ... }
}
```

## Verification

1. ✅ `dotnet build src/Aura` succeeds
2. ✅ Unit tests pass
3. ✅ Integration test with real Ollama (optional, skip in CI)
4. ✅ Mock provider works for agent tests from Phase 1

## Deliverables

- [ ] `ILlmProvider` interface with generate, chat, stream methods
- [ ] `ILlmProviderRegistry` for provider lookup
- [ ] `OllamaProvider` implementation
- [ ] `MockLlmProvider` for testing
- [ ] DI registration extensions
- [ ] Unit tests

## Configuration

```json
{
  "LlmProviders": {
    "Default": "ollama",
    "Ollama": {
      "BaseUrl": "http://localhost:11434"
    }
  }
}
```

## Future Providers (Not in this phase)

- `AzureOpenAIProvider` - Azure-hosted OpenAI
- `AnthropicProvider` - Claude API
- `SemanticKernelProvider` - Microsoft Agent Framework
