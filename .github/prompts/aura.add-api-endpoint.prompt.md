---
description: Guide for adding a new REST API endpoint to Aura.
---

# Add API Endpoint

You are adding a new REST API endpoint to Aura. All endpoints are defined as static extension methods on `WebApplication` in `src/Aura.Api/Endpoints/`.

## Architecture Overview

Endpoints follow the Minimal API pattern:
- **Registration**: `src/Aura.Api/Program.cs` calls `app.Map*Endpoints()` extension methods
- **Implementation**: Each module has its own endpoints file in `src/Aura.Api/Endpoints/`
- **Existing files**: `DeveloperEndpoints.cs`, `ResearcherEndpoints.cs`, `WorkspaceEndpoints.cs`, `RagEndpoints.cs`, `HealthEndpoints.cs`, `McpEndpoints.cs`, `AgentEndpoints.cs`, `GitEndpoints.cs`, `GuardianEndpoints.cs`, `IndexEndpoints.cs`, `ToolEndpoints.cs`

## Step 1: Choose the Right Endpoints File

| If the endpoint is for... | Add to... |
|---------------------------|-----------|
| Story/workflow management | `DeveloperEndpoints.cs` |
| Library/paper management | `ResearcherEndpoints.cs` |
| Workspace registration/indexing | `WorkspaceEndpoints.cs` or `WorkspaceIndexEndpoints.cs` |
| Code graph queries | `WorkspaceGraphEndpoints.cs` |
| RAG search | `RagEndpoints.cs` |
| Git operations | `GitEndpoints.cs` |
| New module entirely | Create new `{Module}Endpoints.cs` file |

## Step 2: Add the Route Registration

In the appropriate endpoints file's `Map*Endpoints` method:

```csharp
// Follow existing conventions:
// - Use RESTful route patterns
// - Include type constraints on route parameters: {id:guid}
// - Group related endpoints together with comments
app.MapGet("/api/{module}/{resource}", GetResource);
app.MapPost("/api/{module}/{resource}", CreateResource);
app.MapPatch("/api/{module}/{resource}/{id:guid}", UpdateResource);
app.MapDelete("/api/{module}/{resource}/{id:guid}", DeleteResource);
```

## Step 3: Implement the Handler Method

```csharp
private static async Task<IResult> GetResource(
    IMyService myService,          // Injected from DI
    HttpContext context,            // For Problem responses
    Guid id,                        // From route parameter
    string? filter = null,          // From query string
    CancellationToken ct = default)
{
    try
    {
        var result = await myService.GetAsync(id, ct);
        return result is null
            ? Problem.NotFound("Resource", id, context)
            : Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Problem.Internal(ex.Message, context);
    }
}
```

### Key patterns:
- **Return `IResult`** — use `Results.Ok()`, `Results.Created()`, `Results.NoContent()`
- **Use `Problem.*` helpers** — `src/Aura.Api/Problems/Problem.cs` for standardized errors
- **Inject services as parameters** — Minimal API auto-resolves from DI
- **Include `CancellationToken`** — always as last parameter
- **Validate required fields** — return `Problem.MissingRequiredField()` with expected format

## Step 4: Create Request/Response DTOs (if needed)

Add DTOs in `src/Aura.Api/Contracts/`:

```csharp
// Use records for immutable DTOs
public sealed record CreateResourceRequest(
    string Name,
    string? Description = null);

public sealed record ResourceResponse(
    Guid Id,
    string Name,
    string Description,
    DateTime CreatedAt);
```

## Step 5: Register in Program.cs (if new endpoints file)

If you created a new endpoints file, register it in `src/Aura.Api/Program.cs`:

```csharp
// Add after existing Map*Endpoints() calls (around line 177)
app.MapMyModuleEndpoints();
```

## Step 6: Add Tests

Test endpoints via integration tests or by testing the underlying service:

```powershell
# Manual verification
curl -s http://localhost:5300/api/{module}/{resource} | ConvertFrom-Json

# POST example
curl -X POST http://localhost:5300/api/{module}/{resource} `
  -H "Content-Type: application/json" `
  -d '{"name": "test"}'
```

## Step 7: Build and Verify

```powershell
dotnet build
dotnet test
```

Then ask user to run `Update-LocalInstall.ps1` as Administrator and test with curl.

## Checklist

- [ ] Route follows RESTful conventions (`/api/{module}/{resource}`)
- [ ] Handler method is `private static async Task<IResult>`
- [ ] Error responses use `Problem.*` helpers
- [ ] Request DTOs are records in `Contracts/`
- [ ] CancellationToken included on async operations
- [ ] Required field validation with helpful error messages
- [ ] Registration in `Program.cs` (if new endpoints file)
- [ ] Build and tests pass
- [ ] Manual curl verification succeeds
