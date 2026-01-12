# Aura Architecture Quick Reference

This document helps future sessions quickly understand the codebase structure.

## Solution Structure

```
src/
├── Aura.Foundation/          # Core abstractions (agents, RAG, prompts, data)
├── Aura.Module.Developer/    # Developer workflow module (WorkflowService, CodeGraphIndexer)
├── Aura.Api/                 # REST API (Program.cs has ALL endpoints inline)
├── Aura.AppHost/             # Aspire orchestration
└── Aura.ServiceDefaults/     # Shared Aspire configuration

extension/                    # VS Code extension (TypeScript)
├── src/services/auraApiService.ts    # API client
├── src/providers/workflowTreeProvider.ts  # Tree view
└── src/commands/                     # Command handlers

prompts/                      # Prompt templates (.prompt files with Handlebars)
agents/                       # Agent definitions (.md files)
```

## Key Files

| What | Where |
|------|-------|
| **All API endpoints** | `src/Aura.Api/Program.cs` (monolithic, search for `app.Map`) |
| **Workflow logic** | `src/Aura.Module.Developer/Services/WorkflowService.cs` |
| **RAG service** | `src/Aura.Foundation/Rag/RagService.cs` |
| **Code graph service** | `src/Aura.Foundation/Rag/ICodeGraphService.cs` |
| **Semantic indexer** | `src/Aura.Foundation/Rag/ISemanticIndexer.cs` |
| **Agent execution** | `src/Aura.Foundation/Agents/ConfigurableAgent.cs` |
| **Prompt loading** | `src/Aura.Foundation/Prompts/PromptRegistry.cs` |
| **LLM providers** | `src/Aura.Foundation/Llm/` |
| **Extension API client** | `extension/src/services/auraApiService.ts` |

## API Endpoints Quick Reference

### Workflows (`/api/developer/workflows`)
- `POST /api/developer/workflows` - Create workflow (needs `repositoryPath`)
- `GET /api/developer/workflows` - List workflows (optional `?repositoryPath=` filter)
- `GET /api/developer/workflows/{id}` - Get workflow details
- `DELETE /api/developer/workflows/{id}` - Delete workflow
- `POST /api/developer/workflows/{id}/analyze` - Enrich/analyze workflow
- `POST /api/developer/workflows/{id}/plan` - Generate steps
- `POST /api/developer/workflows/{id}/steps/{stepId}/execute` - Execute step

### Workspace Indexing (`/api/workspaces`)
- `POST /api/workspaces` - Onboard workspace (registers + starts RAG + code graph indexing)
- `GET /api/workspaces` - List all workspaces
- `GET /api/workspaces/{id}` - Get workspace details with stats
- `POST /api/workspaces/{id}/reindex` - Reindex existing workspace
- `DELETE /api/workspaces/{id}` - Remove workspace and its indexed data
- `GET /api/workspaces/lookup?path=...` - Look up workspace by path

### Code Graph Queries
- `GET /api/graph/find/{name}` - Find nodes by name
- `GET /api/graph/implementations/{interfaceName}` - Find implementations
- `GET /api/graph/callers/{methodName}` - Find method callers
- `GET /api/graph/members/{typeName}` - Get type members
- `GET /api/graph/namespace/{namespaceName}` - Find types in namespace

### RAG Search
- `POST /api/rag/search` - Vector search (body: `{query, topK, sourcePathPrefix}`)
- `POST /api/rag/hybrid` - Hybrid search (vector + graph)

## Configuration

| Setting | File | Notes |
|---------|------|-------|
| LLM Providers | `src/Aura.Api/appsettings.json` → `LlmProviders` | `default` key sets global provider |
| Prompts directory | `src/Aura.Api/appsettings.json` → `Aura:Prompts:Directories` | Default: `["prompts"]` |
| RAG patterns | `src/Aura.Foundation/Rag/RagOptions.cs` → `DefaultIncludePatterns` | File types to index |

## Common Patterns

### Prompt Template Structure
```yaml
---
description: What this prompt does
ragQueries:
  - "query one for RAG context"
  - "query two for RAG context"
---
Your prompt with {{handlebars}} placeholders
```

### Agent Definition Structure
```markdown
---
name: agent-name
capabilities: [capability1, capability2]
provider: azureopenai  # or ollama
model: gpt-4.1-mini
---
# System Prompt
You are an expert...
```

### RAG Context Flow
1. Prompt template defines `ragQueries` in frontmatter
2. `WorkflowService` reads queries via `_promptRegistry.GetRagQueries(promptName)`
3. Queries sent to `RagService.QueryAsync()` or `GetRagContextForStepAsync()`
4. Results passed to agent via `AgentContext.RagContext`
5. `ConfigurableAgent.AppendRagContext()` adds to system prompt

## Path Handling

- **Always normalize paths** for comparison (lowercase, forward slashes)
- Use `EF.Functions.ILike` for case-insensitive DB queries
- `repositoryPath` = source repo, `workspacePath` = git worktree

## Debugging Tips

```powershell
# Check workflow exists
curl -s "http://localhost:5300/api/developer/workflows" | ConvertFrom-Json

# Check workspace status (use URL-encoded path)
curl -s "http://localhost:5300/api/workspaces/lookup?path=C%3A%5Cwork%5CMyRepo"

# Check graph index
curl -s "http://localhost:5300/api/graph/find/ClassName?workspacePath=c%3A/work/myrepo"

# Search RAG manually
Invoke-RestMethod -Method POST -Uri "http://localhost:5300/api/rag/search" -ContentType "application/json" -Body '{"query":"my search","topK":5}'
```

## ADRs Reference

Key architectural decisions in `.project/adr/`:
- `016-configurable-rag-queries.md` - RAG queries in prompt frontmatter
- `017-case-insensitive-paths.md` - Path normalization
- `018-prompt-template-architecture.md` - Prompt vs agent separation
- `008-local-rag-foundation.md` - RAG design
- `015-graph-rag-for-code.md` - Roslyn code graph
