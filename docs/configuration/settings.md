# Settings Reference

Complete reference for Aura configuration options.

## Configuration Files

| File | Purpose |
|------|---------|

| `appsettings.json` | Main configuration |
| `appsettings.Development.json` | Development overrides |
| `appsettings.Local.json` | Local overrides (git-ignored) |

Location: `C:\Program Files\Aura\api\`

## Connection Strings

```json
{
  "ConnectionStrings": {
    "auradb": "Host=localhost;Port=5432;Database=auradb;Username=postgres"
  }
}
```

| Setting | Default | Description |
|---------|---------|-------------|

| `Host` | `localhost` | PostgreSQL server |
| `Port` | `5432` | PostgreSQL port |
| `Database` | `auradb` | Database name |
| `Username` | `postgres` | Database user |

## LLM Configuration

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
        },
        "AzureOpenAI": {
          "Endpoint": "",
          "ApiKey": "",
          "DefaultDeployment": "gpt-4o",
          "MaxTokens": 4096,
          "TimeoutSeconds": 120
        },
        "OpenAI": {
          "ApiKey": "",
          "DefaultModel": "gpt-4o",
          "MaxTokens": 4096,
          "TimeoutSeconds": 120
        }
      }
    }
  }
}
```

### LLM Settings

| Setting | Type | Description |
|---------|------|-------------|

| `DefaultProvider` | string | Which provider to use: `Ollama`, `AzureOpenAI`, `OpenAI` |
| `Providers.*.BaseUrl` | string | API endpoint URL |
| `Providers.*.ApiKey` | string | API key for authentication |
| `Providers.*.DefaultModel` | string | Model to use for generation |
| `Providers.*.DefaultEmbeddingModel` | string | Model for embeddings (Ollama only) |
| `Providers.*.TimeoutSeconds` | int | Request timeout |
| `Providers.*.MaxTokens` | int | Max tokens in response |

## Agent Configuration

```json
{
  "Aura": {
    "Agents": {
      "Directories": ["agents"],
      "EnableHotReload": true
    }
  }
}
```

| Setting | Default | Description |
|---------|---------|-------------|

| `Directories` | `["agents"]` | Paths to agent definition files |
| `EnableHotReload` | `true` | Reload agents when files change |

## Prompt Configuration

```json
{
  "Aura": {
    "Prompts": {
      "Directories": ["prompts"]
    }
  }
}
```

| Setting | Default | Description |
|---------|---------|-------------|

| `Directories` | `["prompts"]` | Paths to prompt template files |

## Developer Module

```json
{
  "Aura": {
    "Modules": {
      "Developer": {
        "BranchPrefix": "workflow",
        "WorktreeDirectory": ".worktrees"
      }
    }
  }
}
```

| Setting | Default | Description |
|---------|---------|-------------|
| `BranchPrefix` | `workflow` | Prefix for workflow branches |
| `WorktreeDirectory` | `.worktrees` | Where to create git worktrees |

## Verification

Aura automatically runs verification checks (build, tests) before completing workflows.

```json
{
  "Aura": {
    "Modules": {
      "Developer": {
        "Verification": {
          "TimeoutSeconds": 300,
          "RunTests": true
        }
      }
    }
  }
}
```

| Setting | Default | Description |
|---------|---------|-------------|
| `TimeoutSeconds` | `300` | Maximum time for verification steps |
| `RunTests` | `true` | Whether to run tests as part of verification |

### Project Detection

Aura auto-detects project type by looking for:

| File | Project Type | Verification Steps |
|------|--------------|-------------------|
| `*.sln` or `*.csproj` | .NET | `dotnet build`, `dotnet test` |
| `package.json` | Node.js | `npm run build`, `npm test` |
| `Cargo.toml` | Rust | `cargo build`, `cargo test` |
| `go.mod` | Go | `go build`, `go test` |
| `pyproject.toml` | Python | `pytest` (if available) |

## Patterns

Operational patterns are stored in the `patterns/` directory.

```json
{
  "Aura": {
    "Patterns": {
      "Directory": "patterns"
    }
  }
}
```

| Setting | Default | Description |
|---------|---------|-------------|
| `Directory` | `patterns` | Path to pattern files |

Patterns support language overlays in subdirectories (e.g., `patterns/csharp/`).

## Logging

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  }
}
```

### Log Levels

| Level | Description |
|-------|-------------|

| `Trace` | Most detailed (debugging only) |
| `Debug` | Detailed debug info |
| `Information` | General operational info |
| `Warning` | Unexpected but handled events |
| `Error` | Errors and exceptions |
| `Critical` | Fatal errors |

### Common Overrides

For debugging LLM calls:

```json
{
  "Logging": {
    "LogLevel": {
      "Aura.Foundation.Llm": "Debug"
    }
  }
}
```

For debugging database:

```json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.EntityFrameworkCore": "Debug"
    }
  }
}
```

## Environment Variables

Any setting can be overridden via environment variables:

```powershell
# Pattern: Section__SubSection__Key
$env:Aura__Llm__DefaultProvider = "OpenAI"
$env:Aura__Llm__Providers__OpenAI__ApiKey = "sk-..."
```

## Full Example

```json
{
  "ConnectionStrings": {
    "auradb": "Host=localhost;Port=5432;Database=auradb;Username=postgres"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
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
    },
    "Agents": {
      "Directories": ["agents"],
      "EnableHotReload": true
    },
    "Prompts": {
      "Directories": ["prompts"]
    },
    "Modules": {
      "Developer": {
        "BranchPrefix": "workflow",
        "WorktreeDirectory": ".worktrees"
      }
    }
  }
}
```
