# RFC 7807 Problem Details for HTTP APIs

**Status:** ✅ Complete
**Priority:** Medium
**Type:** API Compliance / Developer Experience
**Completed:** 2026-01-24

## Progress

- [x] **Phase 1: Infrastructure** - `ProblemDetails`, `ProblemTypes`, `Problem` factory
- [x] **Phase 2: Developer Endpoints** - All 25+ methods updated
- [x] **Phase 3: Other Endpoints** - AgentEndpoints, ConversationEndpoints, CodeGraphEndpoints updated
- [x] **Phase 4: Testing** - All 17 integration tests pass

## Problem Statement

The Aura API currently returns inconsistent error responses:

```json
// Current: Simple error object
{ "error": "Workflow 123 not found" }

// Current: Sometimes just a string
"Not Found"

// Current: Validation errors
{ "errors": { "Title": ["The Title field is required."] } }
```

This makes client error handling fragile and inconsistent. RFC 7807 defines a standard format for HTTP API problem details that provides:

- Consistent structure across all endpoints
- Machine-readable error types
- Human-readable descriptions
- Extension fields for additional context

## RFC 7807 Overview

### Standard Members

| Field | Type | Description |
|-------|------|-------------|
| `type` | URI | A URI reference that identifies the problem type |
| `title` | string | Short, human-readable summary |
| `status` | integer | HTTP status code |
| `detail` | string | Human-readable explanation specific to this occurrence |
| `instance` | URI | URI reference identifying this specific problem occurrence |

### Example

```json
HTTP/1.1 404 Not Found
Content-Type: application/problem+json

{
  "type": "https://aura.dev/problems/story-not-found",
  "title": "Story Not Found",
  "status": 404,
  "detail": "Story with ID '3fa85f64-5717-4562-b3fc-2c963f66afa6' does not exist.",
  "instance": "/api/developer/stories/3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

## Design

### 1. Problem Types Registry

Define a registry of problem types with URIs:

```csharp
// src/Aura.Api/Problems/ProblemTypes.cs
public static class ProblemTypes
{
    private const string BaseUri = "https://aura.dev/problems/";

    // Resource errors
    public const string NotFound = BaseUri + "not-found";
    public const string StoryNotFound = BaseUri + "story-not-found";
    public const string StepNotFound = BaseUri + "step-not-found";
    public const string WorkspaceNotFound = BaseUri + "workspace-not-found";
    public const string AgentNotFound = BaseUri + "agent-not-found";

    // Validation errors
    public const string ValidationFailed = BaseUri + "validation-failed";
    public const string InvalidState = BaseUri + "invalid-state";
    public const string InvalidRequest = BaseUri + "invalid-request";

    // Business logic errors
    public const string StoryNotReady = BaseUri + "story-not-ready";
    public const string StepAlreadyExecuted = BaseUri + "step-already-executed";
    public const string WorktreeCreationFailed = BaseUri + "worktree-creation-failed";
    public const string IndexingInProgress = BaseUri + "indexing-in-progress";

    // External service errors
    public const string LlmProviderError = BaseUri + "llm-provider-error";
    public const string GitOperationFailed = BaseUri + "git-operation-failed";

    // Rate limiting / quotas
    public const string RateLimited = BaseUri + "rate-limited";
    public const string TokenBudgetExceeded = BaseUri + "token-budget-exceeded";
}
```

### 2. Problem Details Record

```csharp
// src/Aura.Api/Problems/ProblemDetails.cs
/// <summary>
/// RFC 7807 Problem Details for HTTP APIs.
/// </summary>
public record ProblemDetails
{
    /// <summary>
    /// A URI reference that identifies the problem type.
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>
    /// A short, human-readable summary of the problem type.
    /// </summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>
    /// The HTTP status code.
    /// </summary>
    [JsonPropertyName("status")]
    public required int Status { get; init; }

    /// <summary>
    /// A human-readable explanation specific to this occurrence.
    /// </summary>
    [JsonPropertyName("detail")]
    public string? Detail { get; init; }

    /// <summary>
    /// A URI reference that identifies the specific occurrence.
    /// </summary>
    [JsonPropertyName("instance")]
    public string? Instance { get; init; }

    /// <summary>
    /// Optional trace ID for debugging.
    /// </summary>
    [JsonPropertyName("traceId")]
    public string? TraceId { get; init; }
}

/// <summary>
/// Problem details with validation errors.
/// </summary>
public record ValidationProblemDetails : ProblemDetails
{
    /// <summary>
    /// Validation errors by field name.
    /// </summary>
    [JsonPropertyName("errors")]
    public IDictionary<string, string[]>? Errors { get; init; }
}
```

### 3. Problem Factory

```csharp
// src/Aura.Api/Problems/ProblemFactory.cs
public static class Problem
{
    public static IResult NotFound(string resourceType, object id, HttpContext context) =>
        Results.Json(
            new ProblemDetails
            {
                Type = ProblemTypes.NotFound,
                Title = $"{resourceType} Not Found",
                Status = 404,
                Detail = $"{resourceType} with ID '{id}' does not exist.",
                Instance = context.Request.Path,
                TraceId = Activity.Current?.Id ?? context.TraceIdentifier,
            },
            contentType: "application/problem+json",
            statusCode: 404);

    public static IResult StoryNotFound(Guid id, HttpContext context) =>
        Results.Json(
            new ProblemDetails
            {
                Type = ProblemTypes.StoryNotFound,
                Title = "Story Not Found",
                Status = 404,
                Detail = $"Story with ID '{id}' does not exist.",
                Instance = context.Request.Path,
                TraceId = Activity.Current?.Id ?? context.TraceIdentifier,
            },
            contentType: "application/problem+json",
            statusCode: 404);

    public static IResult ValidationFailed(
        IDictionary<string, string[]> errors,
        HttpContext context) =>
        Results.Json(
            new ValidationProblemDetails
            {
                Type = ProblemTypes.ValidationFailed,
                Title = "Validation Failed",
                Status = 400,
                Detail = "One or more validation errors occurred.",
                Instance = context.Request.Path,
                TraceId = Activity.Current?.Id ?? context.TraceIdentifier,
                Errors = errors,
            },
            contentType: "application/problem+json",
            statusCode: 400);

    public static IResult InvalidState(
        string detail,
        HttpContext context) =>
        Results.Json(
            new ProblemDetails
            {
                Type = ProblemTypes.InvalidState,
                Title = "Invalid State",
                Status = 409,
                Detail = detail,
                Instance = context.Request.Path,
                TraceId = Activity.Current?.Id ?? context.TraceIdentifier,
            },
            contentType: "application/problem+json",
            statusCode: 409);

    public static IResult BadRequest(
        string detail,
        string? type = null,
        HttpContext? context = null) =>
        Results.Json(
            new ProblemDetails
            {
                Type = type ?? ProblemTypes.InvalidRequest,
                Title = "Bad Request",
                Status = 400,
                Detail = detail,
                Instance = context?.Request.Path,
                TraceId = Activity.Current?.Id ?? context?.TraceIdentifier,
            },
            contentType: "application/problem+json",
            statusCode: 400);

    public static IResult ServiceError(
        string detail,
        string? type = null,
        HttpContext? context = null) =>
        Results.Json(
            new ProblemDetails
            {
                Type = type ?? "https://aura.dev/problems/internal-error",
                Title = "Internal Server Error",
                Status = 500,
                Detail = detail,
                Instance = context?.Request.Path,
                TraceId = Activity.Current?.Id ?? context?.TraceIdentifier,
            },
            contentType: "application/problem+json",
            statusCode: 500);
}
```

### 4. Usage in Endpoints

Before:
```csharp
if (story is null)
{
    return Results.NotFound(new { error = "Story not found" });
}
```

After:
```csharp
if (story is null)
{
    return Problem.StoryNotFound(id, context);
}
```

### 5. Exception Handler Middleware

```csharp
// src/Aura.Api/Middleware/ProblemDetailsMiddleware.cs
public class ProblemDetailsMiddleware(RequestDelegate next, ILogger<ProblemDetailsMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (NotFoundException ex)
        {
            await WriteProblemAsync(context, new ProblemDetails
            {
                Type = ProblemTypes.NotFound,
                Title = "Not Found",
                Status = 404,
                Detail = ex.Message,
                Instance = context.Request.Path,
                TraceId = Activity.Current?.Id ?? context.TraceIdentifier,
            });
        }
        catch (InvalidOperationException ex)
        {
            await WriteProblemAsync(context, new ProblemDetails
            {
                Type = ProblemTypes.InvalidState,
                Title = "Invalid Operation",
                Status = 409,
                Detail = ex.Message,
                Instance = context.Request.Path,
                TraceId = Activity.Current?.Id ?? context.TraceIdentifier,
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            await WriteProblemAsync(context, new ProblemDetails
            {
                Type = "https://aura.dev/problems/internal-error",
                Title = "Internal Server Error",
                Status = 500,
                Detail = "An unexpected error occurred.",
                Instance = context.Request.Path,
                TraceId = Activity.Current?.Id ?? context.TraceIdentifier,
            });
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, ProblemDetails problem)
    {
        context.Response.StatusCode = problem.Status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    }
}
```

### 6. Documentation Endpoint

Serve problem type documentation:

```csharp
app.MapGet("/problems/{type}", (string type) =>
{
    var docs = ProblemDocumentation.Get(type);
    if (docs is null)
        return Results.NotFound();
    return Results.Ok(docs);
}).WithTags("Documentation");
```

## Migration Strategy

### Phase 1: Infrastructure (2h)
1. Create `ProblemDetails` and `ValidationProblemDetails` records
2. Create `ProblemTypes` constants
3. Create `Problem` factory class
4. Add `ProblemDetailsMiddleware`

### Phase 2: Developer Endpoints (2h)
1. Update all `DeveloperEndpoints.cs` methods
2. Replace `Results.NotFound(new { error = ... })` with `Problem.StoryNotFound(...)`
3. Replace `Results.BadRequest(new { error = ... })` with `Problem.BadRequest(...)`

### Phase 3: Other Endpoints (1h)
1. Update `WorkspaceEndpoints.cs`
2. Update `AgentEndpoints.cs`
3. Update `RagEndpoints.cs`

### Phase 4: Testing (1h)
1. Add tests for `ProblemFactory`
2. Update integration tests to expect `application/problem+json`
3. Verify error responses match RFC 7807

## Files to Create/Modify

| File | Change |
|------|--------|
| `src/Aura.Api/Problems/ProblemDetails.cs` | New - RFC 7807 record |
| `src/Aura.Api/Problems/ProblemTypes.cs` | New - Problem type URIs |
| `src/Aura.Api/Problems/Problem.cs` | New - Factory methods |
| `src/Aura.Api/Middleware/ProblemDetailsMiddleware.cs` | New - Exception handler |
| `src/Aura.Api/Program.cs` | Register middleware |
| `src/Aura.Api/Endpoints/DeveloperEndpoints.cs` | Use Problem factory |
| `src/Aura.Api/Endpoints/WorkspaceEndpoints.cs` | Use Problem factory |
| `src/Aura.Api/Endpoints/AgentEndpoints.cs` | Use Problem factory |

## Client-Side Updates

### VS Code Extension

Update `AuraApiService.ts` to handle problem+json:

```typescript
interface ProblemDetails {
  type: string;
  title: string;
  status: number;
  detail?: string;
  instance?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}

private async handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const contentType = response.headers.get('content-type');
    if (contentType?.includes('application/problem+json')) {
      const problem: ProblemDetails = await response.json();
      throw new AuraApiError(problem.title, problem.status, problem);
    }
    throw new AuraApiError(response.statusText, response.status);
  }
  return response.json();
}
```

## Example Responses

### Story Not Found
```json
HTTP/1.1 404 Not Found
Content-Type: application/problem+json

{
  "type": "https://aura.dev/problems/story-not-found",
  "title": "Story Not Found",
  "status": 404,
  "detail": "Story with ID '3fa85f64-5717-4562-b3fc-2c963f66afa6' does not exist.",
  "instance": "/api/developer/stories/3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "traceId": "00-1234567890abcdef-1234567890ab-01"
}
```

### Validation Failed
```json
HTTP/1.1 400 Bad Request
Content-Type: application/problem+json

{
  "type": "https://aura.dev/problems/validation-failed",
  "title": "Validation Failed",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "instance": "/api/developer/stories",
  "traceId": "00-1234567890abcdef-1234567890ab-01",
  "errors": {
    "title": ["The title field is required."],
    "repositoryPath": ["The path does not exist."]
  }
}
```

### Invalid State
```json
HTTP/1.1 409 Conflict
Content-Type: application/problem+json

{
  "type": "https://aura.dev/problems/story-not-ready",
  "title": "Story Not Ready",
  "status": 409,
  "detail": "Story must be in 'Planned' status before executing steps. Current status: 'Draft'.",
  "instance": "/api/developer/stories/3fa85f64-5717-4562-b3fc-2c963f66afa6/steps/execute",
  "traceId": "00-1234567890abcdef-1234567890ab-01"
}
```

### LLM Provider Error
```json
HTTP/1.1 502 Bad Gateway
Content-Type: application/problem+json

{
  "type": "https://aura.dev/problems/llm-provider-error",
  "title": "LLM Provider Error",
  "status": 502,
  "detail": "The OpenAI API returned an error: Rate limit exceeded. Please retry after 60 seconds.",
  "instance": "/api/developer/stories/3fa85f64-5717-4562-b3fc-2c963f66afa6/analyze",
  "traceId": "00-1234567890abcdef-1234567890ab-01"
}
```

## Acceptance Criteria

- [ ] All error responses use `application/problem+json` content type
- [ ] All error responses include `type`, `title`, `status`, `detail`
- [ ] All error responses include `instance` (request path)
- [ ] All error responses include `traceId` for debugging
- [ ] Validation errors include `errors` field with field-level messages
- [ ] Problem types are documented URIs
- [ ] Exception handler middleware catches unhandled exceptions
- [ ] Extension handles problem+json responses correctly
- [ ] Integration tests verify response format

## Benefits

1. **Consistency**: All errors follow the same structure
2. **Discoverability**: `type` URIs can link to documentation
3. **Debugging**: `traceId` and `instance` help trace issues
4. **Tooling**: OpenAPI/Swagger can document problem types
5. **Standards Compliance**: Follows RFC 7807 spec exactly
6. **Extension Support**: Client can show meaningful error messages

## References

- [RFC 7807 - Problem Details for HTTP APIs](https://www.rfc-editor.org/rfc/rfc7807)
- [ASP.NET Core Problem Details](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling#problem-details)
- [Zalando RESTful API Guidelines](https://opensource.zalando.com/restful-api-guidelines/#176)
