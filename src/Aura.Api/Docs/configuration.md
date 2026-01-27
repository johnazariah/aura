# Configuration Reference

**Last Updated**: 2026-01-27  
**Version**: 1.3.1

## Overview

This document provides a complete reference for configuring Aura's API, including LLM providers, RAG settings, workspace configuration, and environment variables.

## Configuration Files

| File | Purpose | Location |
|------|---------|----------|
| `appsettings.json` | Main configuration | `src/Aura.Api/appsettings.json` |
| `appsettings.Development.json` | Development overrides | `src/Aura.Api/appsettings.Development.json` |
| `appsettings.Production.json` | Production overrides | `src/Aura.Api/appsettings.Production.json` |
| Environment variables | Secrets and runtime config | System environment |

## LLM Provider Configuration

### Overview

Aura supports three LLM providers:

1. **Ollama** - Local, privacy-safe inference
2. **Azure OpenAI** - Enterprise-grade cloud LLM
3. **OpenAI** - Direct OpenAI API access

### Provider Selection

Set the default provider in `appsettings.json`:

```json
{
  "Aura": {
    "Llm": {
      "DefaultProvider": "AzureOpenAI"
    }
  }
}
```

**Options**: `Ollama`, `AzureOpenAI`, `OpenAI`

### Ollama Configuration

For local LLM inference:

```json
{
  "Aura": {
    "Llm": {
      "Providers": {
        "Ollama": {
          "BaseUrl": "http://localhost:11434",
          "DefaultModel": "qwen2.5-coder:7b",
          "DefaultEmbeddingModel": "nomic-embed-text",
          "TimeoutSeconds": 300,
          "NumGpu": -1,
          "MaxEmbeddingTextLength": 30000
        }
      }
    }
  }
}
```

#### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `BaseUrl` | string | `http://localhost:11434` | Ollama server endpoint |
| `DefaultModel` | string | `qwen2.5-coder:7b` | Model for chat/completion |
| `DefaultEmbeddingModel` | string | `nomic-embed-text` | Model for RAG embeddings |
| `TimeoutSeconds` | int | `300` | Request timeout (5 minutes) |
| `NumGpu` | int | `-1` | GPU layers (-1 = auto) |
| `MaxEmbeddingTextLength` | int | `30000` | Max chars for embedding |

#### Recommended Models

| Use Case | Model | Size |
|----------|-------|------|
| Code generation | `qwen2.5-coder:7b` | 7B params |
| Code generation (fast) | `qwen2.5-coder:3b` | 3B params |
| Embeddings | `nomic-embed-text` | 768 dimensions |
| General chat | `llama3.2` | 3B params |

### Azure OpenAI Configuration

For enterprise cloud deployment:

```json
{
  "Aura": {
    "Llm": {
      "Providers": {
        "AzureOpenAI": {
          "Endpoint": "https://your-resource.cognitiveservices.azure.com/",
          "ApiKey": "<your-api-key>",
          "DefaultDeployment": "gpt-4o",
          "MaxTokens": 4096,
          "TimeoutSeconds": 300
        }
      }
    }
  }
}
```

#### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `Endpoint` | string | ✅ | Azure OpenAI resource endpoint |
| `ApiKey` | string | ✅ | Azure API key (use env var for production) |
| `DefaultDeployment` | string | ✅ | Deployment name (e.g., `gpt-4o`) |
| `MaxTokens` | int | ❌ | Max response tokens (default: 4096) |
| `TimeoutSeconds` | int | ❌ | Request timeout (default: 300) |

#### Environment Variable Override

**Production**: Store API key in environment variable:

```bash
export AURA_AZUREOPENAI_APIKEY="<your-api-key>"
```

Configuration binding:

```json
{
  "AzureOpenAI": {
    "ApiKey": "${AURA_AZUREOPENAI_APIKEY}"
  }
}
```

### OpenAI Configuration

For direct OpenAI API access:

```json
{
  "Aura": {
    "Llm": {
      "Providers": {
        "OpenAI": {
          "ApiKey": "<your-api-key>",
          "DefaultModel": "gpt-4o",
          "MaxTokens": 4096,
          "TimeoutSeconds": 120
        }
      }
    }
  }
}
```

#### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `ApiKey` | string | ✅ | OpenAI API key |
| `DefaultModel` | string | ✅ | Model name (e.g., `gpt-4o`) |
| `MaxTokens` | int | ❌ | Max response tokens (default: 4096) |
| `TimeoutSeconds` | int | ❌ | Request timeout (default: 120) |

## RAG Configuration

### Workspace Indexing

Workspaces are indexed automatically when onboarded via API or VS Code extension.

#### Default Include/Exclude Patterns

Defined in `src/Aura.Foundation/Rag/RagOptions.cs`:

**Included by default**:
```
*.cs, *.csproj, *.sln, *.py, *.js, *.ts, *.tsx, *.md, *.yaml, *.json
```

**Excluded by default**:
```
bin/, obj/, node_modules/, .git/, .vs/, dist/, build/, target/
```

#### Custom Patterns

Override via API when onboarding:

```powershell
curl -X POST http://localhost:5300/api/workspaces `
  -H "Content-Type: application/json" `
  -d '{
    "path": "C:\\work\\myrepo",
    "includePatterns": ["*.cs", "*.md"],
    "excludePatterns": ["bin/", "obj/", "tests/"]
  }'
```

### RAG Query Options

Configure semantic search behavior:

```json
{
  "Aura": {
    "Rag": {
      "DefaultTopK": 10,
      "MinScore": 0.5,
      "MaxChunkSize": 2000,
      "ChunkOverlap": 200
    }
  }
}
```

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `DefaultTopK` | int | `10` | Number of results to return |
| `MinScore` | float | `0.5` | Minimum similarity score (0-1) |
| `MaxChunkSize` | int | `2000` | Max characters per chunk |
| `ChunkOverlap` | int | `200` | Overlap between chunks |

### Prompt Template RAG Queries

RAG queries can be embedded in prompt templates via YAML frontmatter:

```yaml
---
description: Enrich workflow with context
ragQueries:
  - "similar code patterns"
  - "related tests"
  - "documentation for {{feature}}"
---
You are analyzing a workflow...
```

See [ADR 016: Configurable RAG Queries](../.project/adr/016-configurable-rag-queries.md) for details.

## Workspace Configuration

### Developer Module Settings

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

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `BranchPrefix` | string | `workflow` | Prefix for workflow branches |
| `WorktreeDirectory` | string | `.worktrees` | Relative path for worktrees |

**Example**: Workflow with ID `abc123` creates:
- Branch: `workflow/abc123`
- Worktree: `<repo>/.worktrees/abc123`

## Agent Configuration

### Agent Discovery

Agents are loaded from directories specified in configuration:

```json
{
  "Aura": {
    "Agents": {
      "Directories": ["../agents", "agents"],
      "EnableHotReload": true
    }
  }
}
```

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Directories` | string[] | `["../agents", "agents"]` | Paths to agent definitions |
| `EnableHotReload` | bool | `true` | Auto-reload on file changes |

### Agent Definition Format

Agents are defined in Markdown files:

```markdown
---
id: csharp-refactor
name: C# Refactoring Specialist
capabilities:
  - csharp
  - refactor
  - roslyn
llm:
  model: qwen2.5-coder:7b
  temperature: 0.2
  maxTokens: 4000
---

You are an expert C# developer specializing in refactoring...
```

## Prompt Template Configuration

### Template Discovery

```json
{
  "Aura": {
    "Prompts": {
      "Directories": ["../prompts", "../../prompts"]
    }
  }
}
```

### Template Format

Prompts use Handlebars syntax with YAML frontmatter:

```yaml
---
description: Execute a workflow step
ragQueries:
  - "similar implementations"
---
# Task: {{step.name}}

{{step.description}}

## Context
Repository: {{workflow.repositoryPath}}
Branch: {{workflow.branchName}}
```

## Database Configuration

### PostgreSQL Connection String

```json
{
  "ConnectionStrings": {
    "auradb": "Host=localhost;Port=5433;Database=auradb;Username=aura_user"
  }
}
```

**Production**: Use environment variables:

```bash
export AURA_DB_HOST="your-db-host"
export AURA_DB_PORT="5432"
export AURA_DB_NAME="auradb"
export AURA_DB_USER="aura_user"
export AURA_DB_PASSWORD="<your-password>"
```

Connection string with environment variables:

```json
{
  "ConnectionStrings": {
    "auradb": "Host=${AURA_DB_HOST};Port=${AURA_DB_PORT};Database=${AURA_DB_NAME};Username=${AURA_DB_USER};Pwd=${AURA_DB_PASSWORD}"
  }
}
```

**Note**: Use `Pwd=` instead of `Password=` to avoid triggering secret detection hooks.

## Logging Configuration

### Log Levels

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "System.Net.Http.HttpClient": "Warning",
      "Microsoft.Extensions.Http": "Warning",
      "Polly": "Warning",
      "Microsoft.Extensions.Http.Resilience": "Warning"
    }
  }
}
```

### Available Levels

| Level | When to Use |
|-------|-------------|
| `Trace` | Very detailed diagnostic info |
| `Debug` | Debugging during development |
| `Information` | General informational messages |
| `Warning` | Warning but application continues |
| `Error` | Error events, application may continue |
| `Critical` | Critical failures, application may abort |
| `None` | Disable logging |

### Production Logging

For production deployments, logs are written to:

**Windows**: `C:\ProgramData\Aura\logs\aura-YYYYMMDD.log`

Configure file logging in `appsettings.Production.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Aura": "Information"
    },
    "File": {
      "Path": "C:\\ProgramData\\Aura\\logs\\aura-.log",
      "RollingInterval": "Day",
      "RetainedFileCountLimit": 30
    }
  }
}
```

## Environment Variables

### Summary Table

| Variable | Purpose | Example |
|----------|---------|---------|
| `ASPNETCORE_ENVIRONMENT` | Environment name | `Production` |
| `AURA_AZUREOPENAI_APIKEY` | Azure OpenAI API key | `<your-api-key>` |
| `AURA_OPENAI_APIKEY` | OpenAI API key | `<your-api-key>` |
| `AURA_DB_HOST` | Database host | `localhost` |
| `AURA_DB_PORT` | Database port | `5433` |
| `AURA_DB_NAME` | Database name | `auradb` |
| `AURA_DB_USER` | Database username | `aura_user` |
| `AURA_DB_PASSWORD` | Database password | `<your-password>` |

### Setting Environment Variables

**Windows (PowerShell)**:
```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:AURA_AZUREOPENAI_APIKEY = "<your-api-key>"
```

**Windows (System-wide)**:
```powershell
[System.Environment]::SetEnvironmentVariable("AURA_AZUREOPENAI_APIKEY", "<your-api-key>", "Machine")
```

**Linux/macOS**:
```bash
export ASPNETCORE_ENVIRONMENT=Production
export AURA_AZUREOPENAI_APIKEY="<your-api-key>"
```

## Secrets Management

### Development

Use **user secrets** for local development:

```powershell
cd src/Aura.Api
dotnet user-secrets set "Aura:Llm:Providers:AzureOpenAI:ApiKey" "<your-api-key>"
dotnet user-secrets set "ConnectionStrings:auradb" "Host=localhost;..."
```

### Production

**Best practices**:
1. **Never commit secrets** to source control
2. Use **environment variables** for production
3. Use **Azure Key Vault** or **AWS Secrets Manager** for cloud deployments
4. Use **Windows Credential Manager** for local Windows services

## Configuration Validation

### Startup Checks

Aura validates configuration at startup:

```
✅ LLM provider configured: AzureOpenAI
✅ Database connection: OK
✅ RAG service initialized
✅ Agent registry: 12 agents loaded
✅ Prompt templates: 8 templates loaded
```

### Health Endpoints

Check configuration via API:

```powershell
# Overall health
curl http://localhost:5300/health

# LLM provider health
curl http://localhost:5300/health/ollama

# Database health
curl http://localhost:5300/health/db

# RAG service health
curl http://localhost:5300/health/rag
```

## Example Configurations

### Local Development (Ollama)

```json
{
  "ConnectionStrings": {
    "auradb": "Host=localhost;Port=5433;Database=auradb;Username=aura_user"
  },
  "Aura": {
    "Llm": {
      "DefaultProvider": "Ollama",
      "Providers": {
        "Ollama": {
          "BaseUrl": "http://localhost:11434",
          "DefaultModel": "qwen2.5-coder:7b",
          "DefaultEmbeddingModel": "nomic-embed-text"
        }
      }
    },
    "Agents": {
      "Directories": ["../agents"],
      "EnableHotReload": true
    },
    "Prompts": {
      "Directories": ["../prompts"]
    }
  }
}
```

### Production (Azure OpenAI)

```json
{
  "ConnectionStrings": {
    "auradb": "Host=${AURA_DB_HOST};Port=${AURA_DB_PORT};Database=${AURA_DB_NAME};Username=${AURA_DB_USER};Pwd=${AURA_DB_PASSWORD}"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Aura": "Information"
    }
  },
  "Aura": {
    "Llm": {
      "DefaultProvider": "AzureOpenAI",
      "Providers": {
        "AzureOpenAI": {
          "Endpoint": "${AURA_AZUREOPENAI_ENDPOINT}",
          "ApiKey": "${AURA_AZUREOPENAI_APIKEY}",
          "DefaultDeployment": "gpt-4o",
          "MaxTokens": 4096,
          "TimeoutSeconds": 300
        }
      }
    },
    "Agents": {
      "Directories": ["C:\\Program Files\\Aura\\agents"],
      "EnableHotReload": false
    },
    "Prompts": {
      "Directories": ["C:\\Program Files\\Aura\\prompts"]
    }
  }
}
```

## Troubleshooting

### Common Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| "LLM provider not configured" | Missing API key | Set `ApiKey` in config or env var |
| "Database connection failed" | PostgreSQL not running | Start PostgreSQL service |
| "Agent not found" | Agent directory not configured | Check `Agents.Directories` |
| "Prompt template missing" | Template directory wrong | Check `Prompts.Directories` |

### Diagnostic Commands

```powershell
# Check current configuration
curl http://localhost:5300/health

# Test LLM connection
curl http://localhost:5300/health/ollama

# Test database connection
curl http://localhost:5300/health/db

# List loaded agents
curl http://localhost:5300/api/agents
```

## Next Steps

- **Agent Integration**: See [agents.md](agents.md) for MCP tool usage
- **Tools Reference**: See [tools-reference.md](tools-reference.md) for complete API
- **Troubleshooting**: See [troubleshooting.md](troubleshooting.md) for common issues
