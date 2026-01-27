# API Layer Specification

**Status:** Reference Documentation
**Created:** 2026-01-28

The API layer (`Aura.Api`) provides HTTP endpoints for the VS Code extension, MCP server for GitHub Copilot, and SSE streaming for real-time updates.

## Application Startup

```csharp
// Program.cs key sections

// 1. Configure as Windows Service
builder.Host.UseWindowsService(options => options.ServiceName = "AuraService");

// 2. Configure Serilog
builder.Host.UseSerilog(...);

// 3. Add Aspire defaults
builder.AddServiceDefaults();

// 4. Add PostgreSQL with pgvector
builder.Services.AddDbContext<AuraDbContext>(options =>
    options.UseNpgsql(connectionString, o => o.UseVector()));

// 5. Add Foundation and Developer Module
builder.Services.AddAuraFoundation(builder.Configuration);
var developerModule = new DeveloperModule();
developerModule.ConfigureServices(builder.Services, builder.Configuration);

// 6. Add MCP handler
builder.Services.AddScoped<McpHandler>();

// 7. Map endpoints
app.MapHealthEndpoints();
app.MapMcpEndpoints();
app.MapAgentEndpoints();
app.MapDeveloperEndpoints();
// ... etc
```

---

## 1. REST Endpoints

### 1.1 Health Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/health` | Aspire health check |
| GET | `/alive` | Aspire liveness check |

### 1.2 Agent Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/agents` | List all agents |
| GET | `/api/agents/{id}` | Get agent details |
| POST | `/api/agents/{id}/execute` | Execute agent with prompt |
| POST | `/api/agents/{id}/chat` | Stream chat with agent |

### 1.3 Conversation Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/conversations` | List conversations |
| GET | `/api/conversations/{id}` | Get conversation with messages |
| POST | `/api/conversations` | Create conversation |
| POST | `/api/conversations/{id}/messages` | Add message |
| DELETE | `/api/conversations/{id}` | Delete conversation |

### 1.4 RAG Endpoints

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/rag/index` | Index content |
| POST | `/api/rag/index-directory` | Index directory (background) |
| POST | `/api/rag/query` | Semantic search |
| GET | `/api/rag/stats` | Index statistics |
| DELETE | `/api/rag/clear` | Clear index |

### 1.5 Code Graph Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/graph/find/{name}` | Find nodes by name |
| GET | `/api/graph/implementations/{interface}` | Find interface implementers |
| GET | `/api/graph/callers/{method}` | Find method callers |
| GET | `/api/graph/members/{type}` | Get type members |
| GET | `/api/graph/stats` | Graph statistics |

### 1.6 Index Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/index/health` | Index health by workspace |
| GET | `/api/index/jobs` | List background indexing jobs |
| GET | `/api/index/jobs/{jobId}` | Get job status |
| POST | `/api/index/jobs/{jobId}/cancel` | Cancel job |

### 1.7 Workspace Endpoints

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/workspaces` | Onboard workspace |
| GET | `/api/workspaces` | List workspaces |
| GET | `/api/workspaces/{id}` | Get workspace details |
| GET | `/api/workspaces/lookup?path=...` | Look up by path |
| POST | `/api/workspaces/{id}/reindex` | Reindex workspace |
| DELETE | `/api/workspaces/{id}` | Remove workspace |

### 1.8 Git Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/git/status?path=...` | Git status |
| POST | `/api/git/commit` | Commit changes |
| POST | `/api/git/push` | Push to remote |
| GET | `/api/git/worktrees?repo=...` | List worktrees |
| POST | `/api/git/worktrees` | Create worktree |
| DELETE | `/api/git/worktrees/{path}` | Remove worktree |

### 1.9 Tool Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/tools` | List registered tools |
| GET | `/api/tools/{id}` | Get tool definition |
| POST | `/api/tools/{id}/execute` | Execute tool |

### 1.10 Guardian Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/guardians` | List guardians |
| GET | `/api/guardians/{id}` | Get guardian details |
| POST | `/api/guardians/{id}/run` | Manually trigger guardian |

---

## 2. Developer Endpoints

### 2.1 Story CRUD

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/developer/stories` | Create story |
| GET | `/api/developer/stories` | List stories |
| GET | `/api/developer/stories/{id}` | Get story with steps |
| GET | `/api/developer/stories/by-path?path=...` | Find by worktree path |
| DELETE | `/api/developer/stories/{id}` | Delete story |
| PATCH | `/api/developer/stories/{id}/status` | Reset status |

### 2.2 Story Lifecycle

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/developer/stories/{id}/analyze` | Run analysis agent |
| POST | `/api/developer/stories/{id}/plan` | Generate steps |
| POST | `/api/developer/stories/{id}/decompose` | Decompose using pattern |
| POST | `/api/developer/stories/{id}/run` | Execute next step |
| GET | `/api/developer/stories/{id}/stream` | SSE execution stream |
| POST | `/api/developer/stories/{id}/execute-all` | Execute all steps |
| POST | `/api/developer/stories/{id}/complete` | Complete story |
| POST | `/api/developer/stories/{id}/cancel` | Cancel story |
| POST | `/api/developer/stories/{id}/finalize` | Finalize (commit, push, PR) |
| POST | `/api/developer/stories/{id}/chat` | Chat with story context |

### 2.3 Step Management

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/developer/stories/{id}/steps` | Add step |
| DELETE | `/api/developer/stories/{id}/steps/{stepId}` | Delete step |

### 2.4 Step Operations

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/developer/stories/{id}/steps/{stepId}/execute` | Execute step |
| POST | `/api/developer/stories/{id}/steps/{stepId}/approve` | Approve step |
| POST | `/api/developer/stories/{id}/steps/{stepId}/reject` | Reject with feedback |
| POST | `/api/developer/stories/{id}/steps/{stepId}/skip` | Skip step |
| POST | `/api/developer/stories/{id}/steps/{stepId}/reset` | Reset step |
| POST | `/api/developer/stories/{id}/steps/{stepId}/chat` | Chat about step |
| POST | `/api/developer/stories/{id}/steps/{stepId}/reassign` | Reassign to agent |
| PUT | `/api/developer/stories/{id}/steps/{stepId}/description` | Update description |

### 2.5 Issue Integration

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/developer/stories/from-issue` | Create from GitHub issue |
| POST | `/api/developer/stories/{id}/refresh-from-issue` | Refresh issue data |
| POST | `/api/developer/stories/{id}/post-update` | Post comment to issue |
| POST | `/api/developer/stories/{id}/close-issue` | Close linked issue |

---

## 3. MCP Server

### 3.1 Endpoint

```
POST /mcp
Content-Type: application/json

{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": { "name": "aura_search", "arguments": { "query": "..." } }
}
```

### 3.2 JSON-RPC Methods

| Method | Description |
|--------|-------------|
| `initialize` | Initialize MCP connection |
| `tools/list` | List available tools |
| `tools/call` | Execute a tool |

### 3.3 MCP Tools (Consolidated)

| Tool | Description |
|------|-------------|
| `aura_search` | Semantic search with content type filter |
| `aura_navigate` | Callers, implementations, derived types, usages, by_attribute, extension_methods, by_return_type, references, definition |
| `aura_inspect` | Type members, list types |
| `aura_refactor` | Rename, change_signature, extract_interface, extract_method, extract_variable, safe_delete, move_type_to_file |
| `aura_generate` | Create_type, implement_interface, constructor, property, method, tests |
| `aura_validate` | Compilation, tests |
| `aura_workflow` | List, get, get_by_path, create, enrich, update_step, complete |
| `aura_pattern` | List, get (with language overlay) |
| `aura_workspace` | Detect_worktree, invalidate_cache, status |
| `aura_edit` | Insert_lines, replace_lines, delete_lines, append, prepend |
| `aura_tree` | Hierarchical codebase tree view |
| `aura_get_node` | Get node source code by ID |
| `aura_docs` | Search bundled documentation |

### 3.4 Tool Input Schema Example

```json
{
  "type": "object",
  "properties": {
    "operation": {
      "type": "string",
      "enum": ["callers", "implementations", "derived_types", "usages", "by_attribute"],
      "description": "Navigation operation type"
    },
    "symbolName": {
      "type": "string",
      "description": "Symbol to navigate from"
    },
    "solutionPath": {
      "type": "string",
      "description": "Path to solution file"
    }
  },
  "required": ["operation"]
}
```

### 3.5 Response Format

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "content": [
      {
        "type": "text",
        "text": "Found 5 callers of `ProcessAsync`:\n..."
      }
    ]
  }
}
```

---

## 4. SSE Streaming

### 4.1 Story Execution Stream

```
GET /api/developer/stories/{id}/stream
Accept: text/event-stream
```

Events:

```
event: Started
data: {"type":"Started","storyId":"...","timestamp":"..."}

event: WaveStarted
data: {"type":"WaveStarted","wave":1,"totalWaves":3}

event: StepStarted
data: {"type":"StepStarted","stepId":"...","stepName":"..."}

event: StepOutput
data: {"type":"StepOutput","stepId":"...","output":"..."}

event: StepCompleted
data: {"type":"StepCompleted","stepId":"..."}

event: WaveCompleted
data: {"type":"WaveCompleted","wave":1}

event: Completed
data: {"type":"Completed"}
```

### 4.2 Chat Streaming

```
POST /api/agents/{id}/chat
Accept: text/event-stream
Content-Type: application/json

{"messages":[...], "workspacePath":"..."}
```

Events:

```
event: token
data: {"content":"Hello"}

event: done
data: {"totalTokens":150,"finishReason":"stop"}

event: error
data: {"message":"Rate limit exceeded","code":"rate_limit"}
```

---

## 5. Error Handling

### 5.1 RFC 7807 Problem Details

All error responses follow RFC 7807:

```json
{
  "type": "https://aura.dev/problems/story-not-found",
  "title": "Story Not Found",
  "status": 404,
  "detail": "Story with ID '...' was not found",
  "instance": "/api/developer/stories/..."
}
```

### 5.2 Problem Types

| Type | Status | Description |
|------|--------|-------------|
| `story-not-found` | 404 | Story doesn't exist |
| `step-not-found` | 404 | Step doesn't exist |
| `invalid-state` | 409 | Operation not valid in current state |
| `missing-required-field` | 400 | Required request field missing |
| `workspace-not-indexed` | 400 | Workspace needs onboarding |
| `build-failed` | 422 | Build/compilation failed |
| `internal-error` | 500 | Unexpected server error |

---

## 6. Request/Response Contracts

### 6.1 Create Story Request

```csharp
public record CreateStoryRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string? RepositoryPath { get; init; }
    public string? AutomationMode { get; init; }  // "Assisted", "SemiAuto", "Autonomous"
    public string? DispatchTarget { get; init; }  // "CopilotCli", "InternalAgents"
    public string? IssueUrl { get; init; }
}
```

### 6.2 Story Response

```csharp
{
  "id": "guid",
  "title": "...",
  "description": "...",
  "status": "Planned",
  "automationMode": "Assisted",
  "dispatchTarget": "CopilotCli",
  "gitBranch": "story/...",
  "worktreePath": "/path/to/worktree",
  "repositoryPath": "/path/to/repo",
  "issueUrl": "https://github.com/...",
  "currentWave": 0,
  "waveCount": 3,
  "steps": [
    {
      "id": "guid",
      "order": 0,
      "name": "Step 1",
      "capability": "csharp-coding",
      "status": "Pending",
      "wave": 1
    }
  ],
  "createdAt": "2026-01-28T...",
  "updatedAt": "2026-01-28T..."
}
```

### 6.3 Execute Step Response

```csharp
{
  "id": "guid",
  "name": "Step 1",
  "status": "Completed",
  "output": "Created UserService.cs with...",
  "attempts": 1,
  "startedAt": "...",
  "completedAt": "..."
}
```

---

## 7. CORS Configuration

Allow requests from VS Code extension:

```csharp
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});
```

---

## 8. Logging

### 8.1 Serilog Configuration

```csharp
configuration
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
    .WriteTo.Console()
    .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7);

// Windows: also log to Event Log
if (OperatingSystem.IsWindows())
{
    configuration.WriteTo.EventLog(source: "Aura", restrictedToMinimumLevel: LogEventLevel.Warning);
}
```

### 8.2 Log Locations

| Platform | Path |
|----------|------|
| Windows | `C:\ProgramData\Aura\logs\aura-YYYYMMDD.log` |
| macOS | `~/.local/share/Aura/logs/aura-YYYYMMDD.log` |
| Linux | `$XDG_DATA_HOME/Aura/logs/` or `~/.local/share/Aura/logs/` |
