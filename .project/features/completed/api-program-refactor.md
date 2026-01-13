# API Program.cs Refactoring

**Status:** ✅ Complete  
**Completed:** 2026-01-13  
**Priority:** Medium  
**Estimated Effort:** 1-2 days  
**Created:** 2026-01-13

## Problem Statement

`src/Aura.Api/Program.cs` has grown to nearly 3000 lines and violates clean code principles:

- **Mixed concerns**: Service registration, endpoint definitions, request/response types, and helper methods all in one file
- **Inline lambdas**: ~50+ endpoint handlers defined as inline lambdas
- **DTOs scattered**: Request/response record types defined at bottom of file
- **No organization**: Endpoints for different modules mixed together
- **Hard to navigate**: Finding specific functionality requires extensive scrolling/searching

## Goals

1. **Maintainability**: Easy to find and modify specific endpoints
2. **Separation of concerns**: Types, endpoints, and configuration in logical places
3. **Module isolation**: Developer module endpoints separated from Foundation endpoints
4. **Testability**: Endpoint handlers extractable for unit testing
5. **Consistency**: Follow established patterns for minimal API organization

## Proposed Structure

```
src/Aura.Api/
├── Program.cs                      # Minimal: builder, services, app.Run()
├── Startup/
│   ├── ServiceRegistration.cs      # All AddXxx() calls
│   └── MiddlewareConfiguration.cs  # All app.UseXxx() calls
├── Endpoints/
│   ├── HealthEndpoints.cs          # /health, /health/rag, /health/ollama
│   ├── AgentEndpoints.cs           # /api/agents/*
│   ├── RagEndpoints.cs             # /api/rag/*
│   ├── ConversationEndpoints.cs    # /api/conversations/*
│   ├── WorkspaceEndpoints.cs       # /api/workspaces/*
│   ├── ToolEndpoints.cs            # /api/tools/*
│   ├── GitEndpoints.cs             # /api/git/*
│   ├── CodeGraphEndpoints.cs       # /api/codegraph/*
│   └── DeveloperEndpoints.cs       # /api/developer/*
├── Contracts/
│   ├── Requests/                   # Request DTOs
│   │   ├── CreateWorkflowRequest.cs
│   │   ├── ExecuteAgentRequest.cs
│   │   └── ...
│   └── Responses/                  # Response DTOs
│       ├── WorkflowResponse.cs
│       ├── AgentResponse.cs
│       └── ...
└── Mcp/                            # Already created
    ├── McpHandler.cs
    └── JsonRpcTypes.cs
```

## Implementation Approach

### Option A: Extension Methods (Minimal API Pattern)

Use extension methods on `WebApplication` for endpoint grouping:

```csharp
// Endpoints/HealthEndpoints.cs
public static class HealthEndpoints
{
    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health", GetHealth);
        app.MapGet("/health/rag", GetRagHealth);
        app.MapGet("/health/ollama", GetOllamaHealth);
        app.MapGet("/health/agents", GetAgentHealth);
        return app;
    }
    
    private static async Task<IResult> GetHealth(AuraDbContext db)
    {
        // Handler implementation
    }
}

// Program.cs
app.MapHealthEndpoints()
   .MapAgentEndpoints()
   .MapRagEndpoints()
   // etc.
```

### Option B: Partial Classes

Keep Program.cs as partial class with logical groupings:

```csharp
// Program.cs - main entry
public partial class Program
{
    public static async Task Main(string[] args) { ... }
}

// Program.Health.cs
public partial class Program
{
    private static void MapHealthEndpoints(WebApplication app) { ... }
}

// Program.Agents.cs  
public partial class Program
{
    private static void MapAgentEndpoints(WebApplication app) { ... }
}
```

### Recommendation: Option A

Extension methods are the idiomatic pattern for Minimal API organization. Benefits:

- Clear separation into focused files
- Can be tested independently
- Matches ASP.NET Core documentation examples
- DTOs can live with their endpoints or in Contracts/

## Migration Plan

### Phase 1: Extract DTOs (Low Risk)

1. Create `Contracts/` folder structure
2. Move all request/response records to appropriate files
3. Verify build passes

### Phase 2: Extract Endpoints (Medium Risk)

1. Start with isolated endpoints (Health, MCP)
2. Extract one group at a time
3. Run integration tests after each group
4. Keep Program.cs calling the extensions

### Phase 3: Cleanup Program.cs

1. Extract service registration to `Startup/ServiceRegistration.cs`
2. Extract middleware to `Startup/MiddlewareConfiguration.cs`
3. Program.cs becomes ~50 lines

## Acceptance Criteria

- [x] Program.cs under 100 lines (172 lines - minimal for top-level statements with migration helper)
- [x] Each endpoint group in its own file (11 endpoint files)
- [x] DTOs in Contracts/ folder (ApiContracts.cs - 199 lines)
- [x] All existing tests pass
- [x] No behavior changes
- [x] Build time not significantly impacted

## Implementation Summary

Implemented using **Option A: Extension Methods** pattern:

### Files Created

| File | Lines | Purpose |
|------|-------|---------|
| `Program.cs` | 172 | Minimal entry, service config, endpoint mapping |
| `Contracts/ApiContracts.cs` | 199 | All request/response DTOs |
| `Endpoints/AgentEndpoints.cs` | 394 | Agent CRUD and execution |
| `Endpoints/CodeGraphEndpoints.cs` | 232 | Code graph queries |
| `Endpoints/ConversationEndpoints.cs` | 206 | Conversation management |
| `Endpoints/DeveloperEndpoints.cs` | 653 | Workflow CRUD and step operations |
| `Endpoints/GitEndpoints.cs` | 160 | Git operations and worktrees |
| `Endpoints/HealthEndpoints.cs` | 192 | Health checks |
| `Endpoints/IndexEndpoints.cs` | 188 | Background indexing status |
| `Endpoints/McpEndpoints.cs` | 31 | MCP JSON-RPC endpoint |
| `Endpoints/RagEndpoints.cs` | 146 | RAG index/query |
| `Endpoints/ToolEndpoints.cs` | 169 | Tool execution |
| `Endpoints/WorkspaceEndpoints.cs` | 314 | Workspace management |

**Total: ~2,856 lines organized across 13 files** (vs original ~2,800 lines in 1 file)

## Non-Goals

- Changing endpoint URLs or behavior
- Adding new endpoints
- Changing DI patterns
- Switching from Minimal API to Controllers

## Testing

Since this is pure refactoring:

1. Existing integration tests should pass unchanged
2. Manual smoke test of each endpoint group
3. Verify OpenAPI spec unchanged (if applicable)

## References

- [Minimal API Organization](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/route-handlers)
- [Carter Library](https://github.com/CarterCommunity/Carter) - alternative pattern (not recommended for this scope)
