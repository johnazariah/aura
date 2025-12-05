# Aura Architecture Quick Reference

> **For Copilot/AI Assistants**: Read this first to avoid searching for endpoints and file locations.

## API Endpoints (all in `src/Aura.Api/Program.cs`)

### Workflow Endpoints (`/api/developer/workflows`)

| Method | Endpoint | Purpose |
|--------|----------|---------|
| POST | `/api/developer/workflows` | Create workflow (requires `repositoryPath`, auto-creates worktree) |
| GET | `/api/developer/workflows` | List workflows (filter by `status`, `repositoryPath`) |
| GET | `/api/developer/workflows/{id}` | Get workflow details with steps |
| DELETE | `/api/developer/workflows/{id}` | Delete workflow and cleanup worktree |
| POST | `/api/developer/workflows/{id}/analyze` | Enrich/analyze workflow (uses RAG) |
| POST | `/api/developer/workflows/{id}/plan` | Generate execution steps |
| POST | `/api/developer/workflows/{id}/complete` | Mark workflow as complete |
| POST | `/api/developer/workflows/{id}/cancel` | Cancel workflow |
| POST | `/api/developer/workflows/{id}/chat` | Chat to modify workflow plan |
| POST | `/api/developer/workflows/{id}/steps/{stepId}/execute` | Execute a step |
| POST | `/api/developer/workflows/{id}/steps/{stepId}/approve` | Approve step output |
| POST | `/api/developer/workflows/{id}/steps/{stepId}/reject` | Reject step output (with feedback) |
| POST | `/api/developer/workflows/{id}/steps/{stepId}/skip` | Skip a step (with optional reason) |
| POST | `/api/developer/workflows/{id}/steps/{stepId}/chat` | Chat with agent about a step |
| POST | `/api/developer/workflows/{id}/steps/{stepId}/reassign` | Reassign step to different agent |
| PUT | `/api/developer/workflows/{id}/steps/{stepId}/description` | Update step description |

### RAG Endpoints (`/api/rag`)

| Method | Endpoint | Purpose |
|--------|----------|---------|
| POST | `/api/rag/index/directory` | Index directory (text embeddings ONLY) |
| POST | `/api/rag/search` | Search RAG index |
| GET | `/api/rag/stats/directory?path=...` | Get indexing stats for a directory |

### Semantic/Graph Endpoints (Code Analysis)

| Method | Endpoint | Purpose |
|--------|----------|---------|
| POST | `/api/semantic/index` | **PREFERRED**: Index with code graph + embeddings |
| POST | `/api/graph/index` | Index solution into code graph (Roslyn) |
| GET | `/api/graph/find/{name}` | Find nodes by name |
| GET | `/api/graph/implementations/{interface}` | Find implementations |
| GET | `/api/graph/callers/{method}` | Find method callers |
| GET | `/api/graph/members/{type}` | Get type members |
| DELETE | `/api/graph/{workspacePath}` | Clear graph for workspace |

### Git Worktree Endpoints

| Method | Endpoint | Purpose |
|--------|----------|---------|
| POST | `/api/git/worktree` | Create worktree |
| GET | `/api/git/worktree?repositoryPath=...` | List worktrees |
| DELETE | `/api/git/worktree` | Remove worktree |

## Key File Locations

### Source Code

| Path | Purpose |
|------|---------|
| `src/Aura.Api/Program.cs` | All API endpoints (single file, ~2000 lines) |
| `src/Aura.Foundation/` | Core abstractions (agents, RAG, prompts, data) |
| `src/Aura.Module.Developer/` | Developer module (workflows, git, code analysis) |
| `src/Aura.Module.Developer/Services/WorkflowService.cs` | Workflow logic |
| `src/Aura.Foundation/Agents/ConfigurableAgent.cs` | Agent execution, RAG context injection |
| `src/Aura.Foundation/Prompts/PromptRegistry.cs` | Prompt loading and rendering |
| `src/Aura.Foundation/Rag/RagService.cs` | RAG indexing and querying |
| `src/Aura.Foundation/Rag/RagOptions.cs` | Default include/exclude patterns |

### Configuration

| Path | Purpose |
|------|---------|
| `src/Aura.Api/appsettings.json` | API configuration (LLM providers, RAG settings) |
| `agents/*.md` | Agent definitions (system prompts, capabilities) |
| `prompts/*.prompt` | Prompt templates (Handlebars format with YAML frontmatter) |

### Prompt Templates

| File | Used For |
|------|----------|
| `prompts/workflow-enrich.prompt` | Analyze phase (enrichment) |
| `prompts/workflow-plan.prompt` | Plan phase (step generation) |
| `prompts/step-execute.prompt` | Generic step execution |
| `prompts/step-execute-documentation.prompt` | Documentation steps |
| `prompts/step-review.prompt` | Code review steps |

### ADRs (Architecture Decision Records)

Location: `.project/adr/`

Key ADRs:
- `016-configurable-rag-queries.md` - RAG queries in prompt frontmatter
- `017-case-insensitive-paths.md` - Path normalization for Windows
- `018-prompt-template-architecture.md` - Prompt vs agent vs RAG separation

## Common Debugging Commands

```powershell
# Check API health
curl -s http://localhost:5300/health

# List workflows
curl -s http://localhost:5300/api/developer/workflows | ConvertFrom-Json | ConvertTo-Json -Depth 3

# Check RAG index stats
curl -s "http://localhost:5300/api/rag/stats/directory?path=C%3A%5Cwork%5CYourRepo"

# Check if code graph is indexed
curl -s "http://localhost:5300/api/graph/find/YourClassName?workspacePath=c%3A%2Fwork%2Fyourrepo"

# Trigger semantic indexing (graph + embeddings)
curl -s -X POST "http://localhost:5300/api/semantic/index" -H "Content-Type: application/json" -d '{"directoryPath":"C:\\work\\YourRepo","recursive":true}'
```

## Architecture Patterns

### Prompt Flow

```
Agent Definition (.md)     →  System Prompt ("who you are")
  ↓
RAG Context (auto-injected) →  Appended to system prompt by ConfigurableAgent
  ↓
Prompt Template (.prompt)   →  User Message ("what to do")
  ↓
LLM Provider               →  Azure OpenAI / Ollama (configured in appsettings.json)
```

### RAG Query Configuration

RAG queries can be defined in prompt template frontmatter:

```yaml
---
description: Your prompt description
ragQueries:
  - "query one keywords"
  - "query two keywords"
---
```

The code reads these via `_promptRegistry.GetRagQueries(promptName)`.

### Path Handling

Windows paths are case-insensitive. The system normalizes paths:
- `RagService.cs`: Uses `ToLowerInvariant()` and `ILike` for matching
- `WorkflowService.cs`: Uses `ToLowerInvariant()` for filtering
- `CodeGraphService.cs`: Uses `ToLowerInvariant()` and `ILike` for workspace path matching

```csharp
// Standard pattern:
private static string NormalizePath(string path) =>
    path.Replace('\\', '/').ToLowerInvariant();

var normalized = NormalizePath(workspacePath);
query.Where(n => EF.Functions.ILike(n.WorkspacePath!, normalized));
```

### Indexing Types

1. **Text RAG** (`/api/rag/index/directory`): Just embeddings, no code understanding
2. **Semantic Index** (`/api/semantic/index`): Code graph (Roslyn) + selective embeddings
3. **Graph Index** (`/api/graph/index`): Code graph only from .sln file

**Always prefer `/api/semantic/index`** for code repositories.

## LLM Provider Configuration

In `appsettings.json`:

```json
{
  "LlmProviders": {
    "default": {
      "Provider": "azureopenai",
      "Endpoint": "https://your-endpoint.cognitiveservices.azure.com/",
      "ApiKey": "your-key",
      "DeploymentName": "gpt-4.1-mini"
    }
  }
}
```

## Extension (VS Code)

| Path | Purpose |
|------|---------|
| `extension/src/services/auraApiService.ts` | API client |
| `extension/src/providers/workflowTreeProvider.ts` | Workflow tree view |

Key: Extension filters workflows by `repositoryPath` matching VS Code workspace.
