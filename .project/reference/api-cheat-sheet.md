# Aura API Cheat Sheet

**Base URL**: `http://localhost:5300`

## Health & Status

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/health` | Overall health check |
| GET | `/health/agents` | Agent registry health |
| GET | `/health/db` | Database connectivity |
| GET | `/health/ollama` | Ollama LLM provider status |
| GET | `/health/rag` | RAG service status |

## Workflows (Developer Module)

### Workflow Lifecycle

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/developer/workflows` | Create new workflow |
| GET | `/api/developer/workflows` | List all workflows |
| GET | `/api/developer/workflows/{id}` | Get workflow details with steps |
| POST | `/api/developer/workflows/{id}/analyze` | Analyze/enrich workflow (step 1) |
| POST | `/api/developer/workflows/{id}/plan` | Generate execution plan (step 2) |
| POST | `/api/developer/workflows/{id}/complete` | Mark workflow complete |
| POST | `/api/developer/workflows/{id}/cancel` | Cancel workflow |
| DELETE | `/api/developer/workflows/{id}` | Delete workflow |
| POST | `/api/developer/workflows/{id}/chat` | Chat with workflow context |

### Step Management

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/developer/workflows/{wfId}/steps/{stepId}/execute` | Execute a step |
| POST | `/api/developer/workflows/{wfId}/steps/{stepId}/approve` | Approve step output |
| POST | `/api/developer/workflows/{wfId}/steps/{stepId}/reject` | Reject step (request rework) |
| POST | `/api/developer/workflows/{wfId}/steps/{stepId}/skip` | Skip a step |
| POST | `/api/developer/workflows/{wfId}/steps/{stepId}/chat` | Chat about specific step |
| POST | `/api/developer/workflows/{wfId}/steps/{stepId}/reassign` | Reassign to different agent |
| PUT | `/api/developer/workflows/{wfId}/steps/{stepId}/description` | Update step description |
| POST | `/api/developer/workflows/{id}/steps` | Add new step |
| DELETE | `/api/developer/workflows/{wfId}/steps/{stepId}` | Remove step |

## Workflow Quick Start

```powershell
# 1. Create workflow
$body = '{"title": "My Task", "description": "Details", "repositoryPath": "c:\\work\\myrepo"}'
curl -X POST "http://localhost:5300/api/developer/workflows" -H "Content-Type: application/json" -d $body

# 2. Analyze (enriches with context)
curl -X POST "http://localhost:5300/api/developer/workflows/{id}/analyze"

# 3. Plan (generates steps)
curl -X POST "http://localhost:5300/api/developer/workflows/{id}/plan"

# 4. Execute steps one by one
curl -X POST "http://localhost:5300/api/developer/workflows/{wfId}/steps/{stepId}/execute"

# 5. Approve/reject step output
curl -X POST "http://localhost:5300/api/developer/workflows/{wfId}/steps/{stepId}/approve"
```

## Agents

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/agents` | List all registered agents |
| GET | `/api/agents/{agentId}` | Get agent details |
| GET | `/api/agents/best?capability=X&language=Y` | Find best agent for task |
| POST | `/api/agents/{agentId}/execute` | Execute agent directly |
| POST | `/api/agents/{agentId}/execute/rag` | Execute with RAG context |

## Tools (ReAct)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/tools` | List available tools |
| POST | `/api/tools/{toolId}/execute` | Execute a single tool |
| POST | `/api/tools/react` | Run ReAct loop with tools |

## RAG (Retrieval-Augmented Generation)

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/rag/index` | Index a single file |
| POST | `/api/rag/index/directory` | Index entire directory |
| POST | `/api/rag/query` | Query indexed content |
| GET | `/api/rag/stats` | Get RAG statistics |
| GET | `/api/rag/stats/directory?path=X` | Stats for specific directory |
| DELETE | `/api/rag` | Clear all RAG data |
| DELETE | `/api/rag/{contentId}` | Delete specific content |

## Background Indexing

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/index/background` | Start background indexing job |
| GET | `/api/index/jobs/{jobId}` | Check job status |
| GET | `/api/index/status` | Overall indexing status |

## Code Graph (Roslyn)

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/graph/index` | Index solution for code graph |
| GET | `/api/graph/find/{name}` | Find symbol by name |
| GET | `/api/graph/members/{typeName}` | Get type members |
| GET | `/api/graph/callers/{methodName}` | Find method callers |
| GET | `/api/graph/implementations/{interface}` | Find implementations |
| GET | `/api/graph/namespace/{ns}` | List namespace contents |
| DELETE | `/api/graph/{workspacePath}` | Clear graph for workspace |

## Semantic Index

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/semantic/index` | Index file semantically |

## Git Operations

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/git/status` | Get git status |
| POST | `/api/git/branch` | Create branch |
| POST | `/api/git/commit` | Commit changes |
| GET | `/api/git/worktrees` | List worktrees |
| POST | `/api/git/worktrees` | Create worktree |
| DELETE | `/api/git/worktrees` | Delete worktree |

## Conversations

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/conversations` | Create conversation |
| GET | `/api/conversations` | List conversations |
| GET | `/api/conversations/{id}` | Get conversation |
| POST | `/api/conversations/{id}/messages` | Add message |

## Executions

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/executions` | List execution history |

---

## Common Request Bodies

### Create Workflow
```json
{
  "title": "Task title",
  "description": "Optional description",
  "repositoryPath": "c:\\work\\myrepo"
}
```

### Execute Step
```json
{
  "agentId": "optional-agent-override"
}
```

### Reject Step
```json
{
  "feedback": "What needs to be fixed"
}
```

### RAG Query
```json
{
  "query": "search text",
  "workspacePath": "optional path filter",
  "topK": 5
}
```
