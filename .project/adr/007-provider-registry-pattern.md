# ADR-007: LLM Provider Registry Pattern

## Status

Accepted

## Date

2025-11-26

## Context

Aura needs to support multiple LLM providers:

- **Ollama** (local, primary)
- **OpenAI** (cloud, opt-in)
- **Azure OpenAI** (enterprise, opt-in)
- **Anthropic** (cloud, opt-in)
- (Future) Custom providers

Agents specify their preferred provider and model:

```markdown
## Metadata

- **Provider**: ollama
- **Model**: qwen2.5-coder:7b
```

The question: How should providers be discovered, configured, and selected at runtime?

## Decision

**Use a Provider Registry with fallback logic and configuration-driven defaults.**

### Architecture

```text
┌──────────────────────────────────────────────────────────────┐
│                    ILlmProviderRegistry                       │
│  ┌─────────────────────────────────────────────────────────┐ │
│  │ GetProvider(string providerId) → ILlmProvider           │ │
│  │ GetProviderForAgent(AgentDefinition) → ILlmProvider     │ │
│  │ TryGetProvider(string) → ILlmProvider?                  │ │
│  └─────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────┘
                              │
          ┌───────────────────┼───────────────────┐
          ▼                   ▼                   ▼
    ┌──────────┐        ┌──────────┐        ┌──────────┐
    │  Ollama  │        │  OpenAI  │        │   Stub   │
    │ Provider │        │ Provider │        │ Provider │
    └──────────┘        └──────────┘        └──────────┘
```

### Provider Interface

```csharp
public interface ILlmProvider
{
    string ProviderId { get; }  // "ollama", "openai", etc.
    
    // Task<T> is already a monad - success returns LlmResponse, 
    // failure throws (LlmUnavailableException, ModelNotFoundException, etc.)
    Task<LlmResponse> GenerateAsync(
        string prompt,
        string model,
        double temperature,
        CancellationToken ct);
    
    Task<LlmResponse> ChatAsync(
        IReadOnlyList<ChatMessage> messages,
        string model,
        double temperature,
        CancellationToken ct);
    
    Task<bool> IsModelAvailableAsync(string model, CancellationToken ct);
    Task<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken ct);
}
```

### Registry Behavior

1. **Explicit provider**: Agent specifies `Provider: openai` → use OpenAI
2. **Default fallback**: Agent specifies nothing → use configured default (Ollama)
3. **Unavailable fallback**: Specified provider unavailable → fall back to default with warning
4. **Error on no provider**: No providers available → return error, don't silently fail

### Configuration

```json
{
  "Aura": {
    "Llm": {
      "DefaultProvider": "ollama",
      "Providers": {
        "ollama": {
          "BaseUrl": "http://localhost:11434"
        },
        "openai": {
          "ApiKey": "sk-..."
        }
      }
    }
  }
}
```

## Consequences

### Positive

- **Flexibility** - Easy to add new providers
- **Graceful degradation** - Fallback when provider unavailable
- **Configuration-driven** - No code changes to switch providers
- **Testability** - Stub provider for testing
- **Agent independence** - Agents declare intent, registry handles resolution

### Negative

- **Indirection** - Registry adds a layer between agent and provider
- **Fallback complexity** - Behavior can be non-obvious when falling back
- **Configuration surface** - More settings to understand and document

### Mitigations

- Clear logging when fallback occurs
- Health checks for each provider
- VS Code extension shows provider status
- Sensible defaults (Ollama works out of the box)

## Alternatives Considered

### Direct Provider Injection

```csharp
public class CodingAgent(IOllamaProvider ollama) { }
```

- **Pros**: Simple, explicit
- **Cons**: Hard-coded to one provider, no fallback
- **Rejected**: Doesn't support multi-provider scenarios

### Strategy Pattern per Agent

```csharp
public class CodingAgent(ILlmProviderStrategy strategy) { }
```

- **Pros**: Each agent chooses its strategy
- **Cons**: Every agent needs provider logic
- **Rejected**: Registry centralizes the complexity

### Service Locator

```csharp
var provider = ServiceLocator.Get<ILlmProvider>("ollama");
```

- **Pros**: Flexible
- **Cons**: Anti-pattern, hard to test, hidden dependencies
- **Rejected**: Registry is explicit dependency injection

## Implementation Notes

The `StubLlmProvider` is always registered for:

- Unit testing without real LLM
- Agents that don't need real inference (echo-agent)
- Development without Ollama running

```csharp
// StubLlmProvider returns echoed input
public Task<LlmResponse> GenerateAsync(string prompt, ...) =>
    Task.FromResult(new LlmResponse($"[Stub] {prompt}"));
```

## References

- [ILlmProvider.cs](../../src/Aura.Foundation/Llm/ILlmProvider.cs)
- [LlmProviderRegistry.cs](../../src/Aura.Foundation/Llm/LlmProviderRegistry.cs)
- [OllamaProvider.cs](../../src/Aura.Foundation/Llm/OllamaProvider.cs)
