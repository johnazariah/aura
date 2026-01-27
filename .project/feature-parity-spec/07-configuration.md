# Configuration Specification

**Status:** Reference Documentation
**Created:** 2026-01-28

This document specifies all configuration options for the Aura system.

## 1. Configuration Sources

Configuration is loaded from (in order of precedence):
1. Environment variables
2. `appsettings.{Environment}.json`
3. `appsettings.json`
4. Command-line arguments

---

## 2. Connection Strings

### 2.1 Database

```json
{
  "ConnectionStrings": {
    "auradb": "Host=127.0.0.1;Port=5433;Database=auradb;Username=postgres;Password=aura"
  }
}
```

Environment variable: `ConnectionStrings__auradb`

---

## 3. LLM Configuration

### 3.1 General LLM Options

```json
{
  "Llm": {
    "Provider": "ollama",
    "Model": "qwen2.5-coder:14b",
    "Temperature": 0.7,
    "MaxTokens": 4096
  }
}
```

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Provider` | string | `ollama` | Default provider: `ollama`, `azure`, `openai` |
| `Model` | string | null | Default model (null = provider default) |
| `Temperature` | double | 0.7 | Generation temperature |
| `MaxTokens` | int | null | Maximum output tokens |

### 3.2 Ollama Configuration

```json
{
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "DefaultModel": "llama3.2",
    "EmbeddingModel": "nomic-embed-text"
  }
}
```

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `BaseUrl` | string | `http://localhost:11434` | Ollama API endpoint |
| `DefaultModel` | string | `llama3.2` | Default chat model |
| `EmbeddingModel` | string | `nomic-embed-text` | Model for embeddings |

### 3.3 Azure OpenAI Configuration

```json
{
  "AzureOpenAi": {
    "Endpoint": "https://YOUR_RESOURCE.openai.azure.com",
    "ApiKey": "YOUR_API_KEY",
    "DeploymentName": "gpt-4o",
    "EmbeddingDeploymentName": "text-embedding-ada-002"
  }
}
```

| Option | Type | Description |
|--------|------|-------------|
| `Endpoint` | string | Azure OpenAI endpoint URL |
| `ApiKey` | string | API key |
| `DeploymentName` | string | Chat model deployment |
| `EmbeddingDeploymentName` | string | Embedding model deployment |

### 3.4 OpenAI Configuration

```json
{
  "OpenAi": {
    "ApiKey": "sk-...",
    "DefaultModel": "gpt-4o",
    "EmbeddingModel": "text-embedding-ada-002"
  }
}
```

---

## 4. RAG Configuration

### 4.1 RAG Options

```json
{
  "Rag": {
    "ChunkSize": 500,
    "ChunkOverlap": 50,
    "DefaultLimit": 10,
    "MinScore": 0.5,
    "EmbeddingDimension": 768
  }
}
```

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `ChunkSize` | int | 500 | Max tokens per chunk |
| `ChunkOverlap` | int | 50 | Overlap between chunks |
| `DefaultLimit` | int | 10 | Default result limit |
| `MinScore` | double | 0.5 | Minimum similarity score |
| `EmbeddingDimension` | int | 768 | Vector dimension (Ollama: 768, OpenAI: 1536) |

### 4.2 Execution Options

```json
{
  "RagExecution": {
    "Enabled": true,
    "MaxContextTokens": 4000,
    "IncludeSourcePath": true
  }
}
```

### 4.3 Watcher Options

```json
{
  "RagWatcher": {
    "Enabled": false,
    "DebounceMs": 1000
  }
}
```

### 4.4 Background Indexer Options

```json
{
  "BackgroundIndexer": {
    "MaxConcurrentJobs": 2,
    "BatchSize": 50,
    "ProgressReportIntervalMs": 5000
  }
}
```

---

## 5. Agent Configuration

### 5.1 Agent Options

```json
{
  "Aura": {
    "Agents": {
      "Directories": ["agents", "agents/custom"],
      "EnableHotReload": true
    }
  }
}
```

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Directories` | string[] | `["agents"]` | Directories to scan for agents |
| `EnableHotReload` | bool | `true` | Watch for file changes |

---

## 6. Developer Module Configuration

### 6.1 Developer Options

```json
{
  "Aura": {
    "Modules": {
      "Developer": {
        "AgentsPath": "./agents/developer",
        "WorktreesDirectory": "../worktrees",
        "MaxParallelSteps": 4
      }
    }
  }
}
```

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `AgentsPath` | string | `./agents/developer` | Developer-specific agents |
| `WorktreesDirectory` | string | `../worktrees` | Where to create worktrees |
| `MaxParallelSteps` | int | 4 | Max parallel step execution |

### 6.2 GitHub Options

```json
{
  "GitHub": {
    "Token": "ghp_...",
    "BaseUrl": "https://api.github.com"
  }
}
```

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Token` | string | null | GitHub personal access token |
| `BaseUrl` | string | `https://api.github.com` | GitHub API base URL |

Environment variable: `GITHUB_TOKEN`

---

## 7. Guardian Configuration

```json
{
  "Guardians": {
    "Enabled": true,
    "Directory": "guardians",
    "ScheduleEnabled": true
  }
}
```

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Enabled` | bool | `true` | Enable guardian system |
| `Directory` | string | `guardians` | Guardian YAML directory |
| `ScheduleEnabled` | bool | `true` | Run scheduled guardians |

---

## 8. Prompt Configuration

```json
{
  "Prompts": {
    "Directory": "prompts",
    "EnableHotReload": true
  }
}
```

---

## 9. Logging Configuration

### 9.1 Serilog

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System.Net.Http.HttpClient": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "File",
        "Args": {
          "path": "logs/aura-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7
        }
      }
    ]
  }
}
```

---

## 10. Aspire Configuration

### 10.1 AppHost (`AppHost.csproj`)

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL with pgvector
var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .WithEnvironment("POSTGRES_INITDB_ARGS", "--data-checksums")
    .AddDatabase("auradb");

// Ollama (when running locally)
var ollama = builder.AddOllama("ollama")
    .WithDataVolume()
    .WithGPU();

// API service
var api = builder.AddProject<Projects.Aura_Api>("api")
    .WithReference(postgres)
    .WithReference(ollama)
    .WaitFor(postgres);

builder.Build().Run();
```

---

## 11. Windows Service Configuration

### 11.1 Service Name

```csharp
builder.Host.UseWindowsService(options =>
{
    options.ServiceName = "AuraService";
});
```

### 11.2 Service Installation

```powershell
# Install as service
sc.exe create AuraService binPath="C:\Program Files\Aura\Aura.Api.exe"
sc.exe config AuraService start=auto
sc.exe start AuraService
```

---

## 12. Environment Variables

### 12.1 Standard Variables

| Variable | Description |
|----------|-------------|
| `ASPNETCORE_ENVIRONMENT` | Environment: `Development`, `Production` |
| `AURA_LOG_DIR` | Override log directory |
| `GITHUB_TOKEN` | GitHub API token |

### 12.2 Connection Overrides

| Variable | Description |
|----------|-------------|
| `ConnectionStrings__auradb` | Database connection string |
| `Ollama__BaseUrl` | Ollama endpoint |
| `AzureOpenAi__Endpoint` | Azure OpenAI endpoint |
| `AzureOpenAi__ApiKey` | Azure OpenAI key |

---

## 13. Default Ports

| Service | Port | Description |
|---------|------|-------------|
| Aura API | 5300 | Main HTTP API |
| PostgreSQL | 5433 | Database (non-standard to avoid conflicts) |
| Ollama | 11434 | LLM inference |
| Aspire Dashboard | 15888 | Aspire orchestration UI |

---

## 14. Platform-Specific Paths

### 14.1 Windows

| Path | Location |
|------|----------|
| Install | `C:\Program Files\Aura` |
| Logs | `C:\ProgramData\Aura\logs` |
| Data | `C:\ProgramData\Aura\data` |
| Config | `C:\ProgramData\Aura\appsettings.json` |

### 14.2 macOS/Linux

| Path | Location |
|------|----------|
| Data | `~/.local/share/Aura` |
| Logs | `~/.local/share/Aura/logs` |
| Config | `~/.config/aura/appsettings.json` |

---

## 15. Development vs Production

### 15.1 Development (`appsettings.Development.json`)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  },
  "Ollama": {
    "BaseUrl": "http://localhost:11434"
  },
  "ConnectionStrings": {
    "auradb": "Host=localhost;Port=5433;Database=auradb;Username=postgres;Password=dev"
  }
}
```

### 15.2 Production (`appsettings.json`)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "ConnectionStrings": {
    "auradb": "Host=127.0.0.1;Port=5433;Database=auradb;Username=postgres;Password=${AURA_DB_PASSWORD}"
  }
}
```

---

## 16. VS Code Extension Settings

### 16.1 Extension Settings

```json
{
  "aura.apiUrl": "http://localhost:5300",
  "aura.refreshInterval": 10000,
  "aura.autoOnboard": false
}
```

### 16.2 MCP Configuration

In VS Code `settings.json`:

```json
{
  "mcp": {
    "servers": {
      "aura": {
        "url": "http://localhost:5300/mcp"
      }
    }
  }
}
```
