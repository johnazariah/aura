# Aura Architecture Quick Reference

This document helps future sessions quickly understand the codebase structure.

> **Architecture**: Hybrid — local code intelligence (Roslyn, TreeSitter, pgvector RAG) with cloud LLM inference (Azure OpenAI default, Ollama optional). See [ADR-024](../.project/adr/024-hybrid-architecture.md).

## Solution Structure

```
src/
├── Aura.Foundation/          # Core: agents, LLM, RAG, prompts, data
├── Aura.Module.Developer/    # Developer vertical: stories, git, Roslyn, code gen
├── Aura.Module.Researcher/   # Researcher vertical: library, papers, PDF ingestion
├── Aura.Api/                 # REST API + MCP server (Windows Service, port 5300)
├── Aura.AppHost/             # Aspire orchestration
└── Aura.ServiceDefaults/     # Shared Aspire configuration

extension/                    # VS Code extension (TypeScript)
├── src/services/auraApiService.ts    # API client
├── src/providers/workflowTreeProvider.ts  # Tree view
└── src/commands/                     # Command handlers

prompts/                      # Prompt templates (.prompt files with Handlebars)
agents/                       # Agent definitions (.md files)
patterns/                     # Step-by-step operational patterns
```

## Key Files

| What | Where |
|------|-------|
| **Endpoint registration** | `src/Aura.Api/Program.cs` (calls `Map*Endpoints()` extension methods) |
| **Endpoint implementations** | `src/Aura.Api/Endpoints/*.cs` (one file per module) |
| **MCP handler** | `src/Aura.Api/Mcp/McpHandler.cs` + 11 partial files |
| **Story logic** | `src/Aura.Module.Developer/Services/StoryService.cs` |
| **RAG service** | `src/Aura.Foundation/Rag/RagService.cs` |
| **Code graph service** | `src/Aura.Foundation/Rag/ICodeGraphService.cs` |
| **Semantic indexer** | `src/Aura.Foundation/Rag/ISemanticIndexer.cs` |
| **Agent execution** | `src/Aura.Foundation/Agents/ConfigurableAgent.cs` |
| **Prompt loading** | `src/Aura.Foundation/Prompts/PromptRegistry.cs` |
| **LLM providers** | `src/Aura.Foundation/Llm/` |
| **Library service** | `src/Aura.Module.Researcher/Services/LibraryService.cs` |
| **Extension API client** | `extension/src/services/auraApiService.ts` |

## MCP Tools (13 tools exposed to GitHub Copilot)

| Tool | Partial File | Purpose |
|------|-------------|---------|
| `aura_architect` | McpHandler.cs | Architecture analysis |
| `aura_docs` | McpHandler.cs | Documentation tools |
| `aura_edit` | McpHandler.Edit.cs | File editing |
| `aura_generate` | McpHandler.Generate.cs | Code generation (C#) |
| `aura_inspect` | McpHandler.Inspect.cs | Type/member inspection |
| `aura_navigate` | McpHandler.Navigate.cs | Callers, implementations, references |
| `aura_pattern` | McpHandler.Pattern.cs | Load operational patterns |
| `aura_refactor` | McpHandler.Refactor.cs | Rename, extract, move |
| `aura_search` | McpHandler.Search.cs | Semantic code search |
| `aura_tree` | McpHandler.cs | File tree |
| `aura_validate` | McpHandler.Validate.cs | Compilation & test validation |
| `aura_workflow` | McpHandler.Workflow.cs | Story management |
| `aura_workspace` | McpHandler.Workspaces.cs | Workspace registration |

## API Endpoints Quick Reference

### Stories (`/api/developer/stories`)
- `POST /api/developer/stories` - Create story
- `GET /api/developer/stories` - List stories
- `GET /api/developer/stories/by-path` - Find story by worktree path
- `GET /api/developer/stories/{id}` - Get story details
- `DELETE /api/developer/stories/{id}` - Delete story
- `POST /api/developer/stories/{id}/analyze` - Enrich/analyze story
- `POST /api/developer/stories/{id}/decompose` - Generate steps
- `POST /api/developer/stories/{id}/run` - Execute story
- `GET /api/developer/stories/{id}/stream` - SSE stream of execution
- `POST /api/developer/stories/{id}/complete` - Mark complete
- `POST /api/developer/stories/{id}/cancel` - Cancel execution
- `POST /api/developer/stories/{id}/chat` - Chat with story context

### Workspace Indexing (`/api/workspaces`)
- `POST /api/workspaces` - Onboard workspace (registers + starts RAG + code graph indexing)
- `GET /api/workspaces` - List all workspaces
- `GET /api/workspaces/{id}` - Get workspace details with stats
- `POST /api/workspaces/{id}/reindex` - Reindex existing workspace
- `DELETE /api/workspaces/{id}` - Remove workspace and its indexed data
- `GET /api/workspaces/lookup?path=...` - Look up workspace by path

### Researcher (`/api/researcher`)
- `GET /api/researcher/sources` - List library sources
- `POST /api/researcher/sources` - Create source
- `POST /api/researcher/sources/import` - Import from URL/file
- `POST /api/researcher/sources/search` - Semantic search across library
- `POST /api/researcher/papers/search` - Search academic papers
- `POST /api/researcher/sources/{id}/convert` - PDF → Markdown
- `GET /api/researcher/sources/{id}/excerpts` - Get excerpts

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
- `024-hybrid-architecture.md` - Hybrid architecture (supersedes ADR-001)
- `016-configurable-rag-queries.md` - RAG queries in prompt frontmatter
- `017-case-insensitive-paths.md` - Path normalization
- `018-prompt-template-architecture.md` - Prompt vs agent separation
- `008-local-rag-foundation.md` - RAG design
- `015-graph-rag-for-code.md` - Roslyn code graph
