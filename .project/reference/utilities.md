# Aura Utilities Reference

Common utility classes and helpers in the Aura Foundation library.

## EnvHelper

**Namespace**: `Aura.Foundation.Tools`  
**Location**: `src/Aura.Foundation/Tools/EnvHelper.cs`

### Purpose

`EnvHelper` provides a cleaner, safer API for working with environment variables. It offers two key benefits over direct `Environment.GetEnvironmentVariable()` calls:

1. **Default values** - Easily provide fallback values without null-coalescing operators
2. **Required variables** - Fail fast with descriptive errors when critical configuration is missing

### Methods

#### GetOrDefault

Retrieves an environment variable or returns a default value if not set.

```csharp
public static string GetOrDefault(string key, string defaultValue)
```

**Example:**

```csharp
using Aura.Foundation.Tools;

// Get LLM provider with fallback to "ollama"
var provider = EnvHelper.GetOrDefault("AURA_LLM_PROVIDER", "ollama");

// Get database path with fallback
var dbPath = EnvHelper.GetOrDefault("AURA_DB_PATH", "./data/aura.db");

// Get optional feature flag
var enableDebugMode = EnvHelper.GetOrDefault("AURA_DEBUG", "false");
```

#### RequireEnv

Retrieves an environment variable and throws `InvalidOperationException` if not set or empty.

```csharp
public static string RequireEnv(string key)
```

**Example:**

```csharp
using Aura.Foundation.Tools;

// Require critical configuration at startup
var apiKey = EnvHelper.RequireEnv("OPENAI_API_KEY");
var workspaceRoot = EnvHelper.RequireEnv("AURA_WORKSPACE_ROOT");

// This will throw with message:
// "Required environment variable 'OPENAI_API_KEY' is not set."
```

### Usage Guidelines

#### When to Use EnvHelper

**✅ Use `EnvHelper.GetOrDefault()` when:**
- You have a sensible default value for the setting
- The environment variable is optional configuration
- You want cleaner code without null-coalescing operators

**✅ Use `EnvHelper.RequireEnv()` when:**
- The environment variable is critical for application startup
- Missing configuration should fail fast with a clear error message
- You want to avoid null-reference exceptions later in the code path

**❌ Avoid direct `Environment.GetEnvironmentVariable()` when:**
- You need default values (use `GetOrDefault` instead)
- You need validation (use `RequireEnv` instead)
- You're checking multiple environment variables (EnvHelper is more readable)

#### When Direct Access Is Fine

Direct `Environment.GetEnvironmentVariable()` is acceptable for:
- One-off checks where EnvHelper overhead isn't justified
- Dynamic variable names constructed at runtime
- System variables that don't need defaults (e.g., `PATH`, `USER`)

### Examples

#### Configuration Class

```csharp
public class AuraConfiguration
{
    // Required settings - fail fast if missing
    public string WorkspaceRoot { get; } = EnvHelper.RequireEnv("AURA_WORKSPACE_ROOT");
    
    // Optional settings with sensible defaults
    public string LlmProvider { get; } = EnvHelper.GetOrDefault("AURA_LLM_PROVIDER", "ollama");
    public string LogLevel { get; } = EnvHelper.GetOrDefault("AURA_LOG_LEVEL", "Information");
    public int MaxTokens { get; } = int.Parse(EnvHelper.GetOrDefault("AURA_MAX_TOKENS", "4096"));
}
```

#### Startup Validation

```csharp
public static void ValidateEnvironment()
{
    var requiredVars = new[]
    {
        "AURA_WORKSPACE_ROOT",
        "AURA_DB_CONNECTION",
        "OPENAI_API_KEY"
    };

    foreach (var varName in requiredVars)
    {
        // Will throw descriptive error if any are missing
        EnvHelper.RequireEnv(varName);
    }
}
```

#### Service Configuration

```csharp
public class OllamaService
{
    private readonly string _baseUrl;
    private readonly string _model;
    
    public OllamaService()
    {
        // Use defaults for local development
        _baseUrl = EnvHelper.GetOrDefault("OLLAMA_BASE_URL", "http://localhost:11434");
        _model = EnvHelper.GetOrDefault("OLLAMA_MODEL", "llama3.1");
    }
}
```

### Migration from Direct Access

**Before:**
```csharp
var provider = Environment.GetEnvironmentVariable("AURA_LLM_PROVIDER") ?? "ollama";
var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
if (string.IsNullOrEmpty(apiKey))
{
    throw new InvalidOperationException("OPENAI_API_KEY is required");
}
```

**After:**
```csharp
var provider = EnvHelper.GetOrDefault("AURA_LLM_PROVIDER", "ollama");
var apiKey = EnvHelper.RequireEnv("OPENAI_API_KEY");
```

---

## Adding New Utilities

When adding utilities to `Aura.Foundation.Tools`:

1. **Keep it focused** - One clear responsibility per class
2. **Static by default** - Unless state is needed
3. **Document thoroughly** - Include XML docs with `<summary>`, `<param>`, `<returns>`, `<exception>`
4. **Add to this reference** - Document usage patterns and guidelines
5. **Include examples** - Show common use cases and migration paths
