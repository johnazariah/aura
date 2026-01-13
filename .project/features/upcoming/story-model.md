# Story Model & GitHub Issue Integration

**Status:** Proposed  
**Priority:** High  
**Estimated Effort:** 2-3 days  
**Created:** 2026-01-13

## Overview

Extend the existing Workflow model to support:
1. **Mode** — structured (current) vs. conversational (new)
2. **Issue linking** — import from GitHub Issues, post updates back
3. **Parallel stories** — multiple stories in flight, each in isolated worktree

## Strategic Context

Per [ADR-021](../../adr/021-session-infrastructure-pivot.md), Aura is pivoting to "development session infrastructure." Stories become the primary unit of work, with structured workflows as one execution mode.

## Design Principles

1. **Workflow IS a Story** — no new entity, just extend existing model
2. **Rename later** — user will rename Workflow → Story via compiler refactoring
3. **Append-only sync** — no conflict detection, just post timestamped comments
4. **Import, don't mirror** — issue content imported on demand, not continuously synced

## Schema Changes

### Workflow Entity Extensions

```csharp
public sealed class Workflow  // Will be renamed to "Story" later
{
    // === EXISTING (unchanged) ===
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? RepositoryPath { get; set; }
    public WorkflowStatus Status { get; set; }
    public string? WorktreePath { get; set; }
    public string? GitBranch { get; set; }
    public string? AnalyzedContext { get; set; }
    public string? ExecutionPlan { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? PullRequestUrl { get; set; }
    public ICollection<WorkflowStep> Steps { get; set; }
    
    // === NEW: Issue Integration ===
    
    /// <summary>External issue URL (e.g., "https://github.com/org/repo/issues/123").</summary>
    public string? IssueUrl { get; set; }
    
    /// <summary>Issue provider type.</summary>
    public IssueProvider? IssueProvider { get; set; }
    
    /// <summary>Issue number (extracted from URL for API calls).</summary>
    public int? IssueNumber { get; set; }
    
    /// <summary>Repository owner (extracted from URL).</summary>
    public string? IssueOwner { get; set; }
    
    /// <summary>Repository name (extracted from URL).</summary>
    public string? IssueRepo { get; set; }
    
    // === NEW: Mode ===
    
    /// <summary>Execution mode: structured (steps) or conversational.</summary>
    public WorkflowMode Mode { get; set; } = WorkflowMode.Structured;
}

public enum IssueProvider
{
    GitHub,
    AzureDevOps,
}

public enum WorkflowMode
{
    /// <summary>Plan → Steps → Execute → Review (current behavior).</summary>
    Structured,
    
    /// <summary>Free-form conversation in worktree (GHCP Agent mode).</summary>
    Conversational,
}
```

### Database Migration

```csharp
public partial class AddStoryFeatures : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("IssueUrl", "Workflows", nullable: true);
        migrationBuilder.AddColumn<string>("IssueProvider", "Workflows", nullable: true);
        migrationBuilder.AddColumn<int>("IssueNumber", "Workflows", nullable: true);
        migrationBuilder.AddColumn<string>("IssueOwner", "Workflows", nullable: true);
        migrationBuilder.AddColumn<string>("IssueRepo", "Workflows", nullable: true);
        migrationBuilder.AddColumn<string>("Mode", "Workflows", nullable: false, defaultValue: "Structured");
        
        migrationBuilder.CreateIndex("IX_Workflows_IssueUrl", "Workflows", "IssueUrl");
    }
}
```

## GitHub Integration

### Service Interface

```csharp
public interface IGitHubService
{
    /// <summary>Fetch issue details.</summary>
    Task<GitHubIssue> GetIssueAsync(string owner, string repo, int number, CancellationToken ct = default);
    
    /// <summary>Post a comment to an issue.</summary>
    Task PostCommentAsync(string owner, string repo, int number, string body, CancellationToken ct = default);
    
    /// <summary>Close an issue.</summary>
    Task CloseIssueAsync(string owner, string repo, int number, CancellationToken ct = default);
    
    /// <summary>Parse issue URL into components.</summary>
    (string owner, string repo, int number)? ParseIssueUrl(string url);
}

public record GitHubIssue(
    int Number,
    string Title,
    string? Body,
    string State,
    IReadOnlyList<string> Labels,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string HtmlUrl
);
```

### Implementation

```csharp
public sealed class GitHubService : IGitHubService
{
    private readonly HttpClient _http;
    private readonly ILogger<GitHubService> _logger;
    
    public GitHubService(HttpClient http, ILogger<GitHubService> logger)
    {
        _http = http;
        _logger = logger;
    }
    
    public async Task<GitHubIssue> GetIssueAsync(string owner, string repo, int number, CancellationToken ct)
    {
        var response = await _http.GetAsync($"/repos/{owner}/{repo}/issues/{number}", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GitHubIssue>(ct) 
            ?? throw new InvalidOperationException("Failed to parse issue");
    }
    
    public async Task PostCommentAsync(string owner, string repo, int number, string body, CancellationToken ct)
    {
        var content = JsonContent.Create(new { body });
        var response = await _http.PostAsync($"/repos/{owner}/{repo}/issues/{number}/comments", content, ct);
        response.EnsureSuccessStatusCode();
        _logger.LogInformation("Posted comment to {Owner}/{Repo}#{Number}", owner, repo, number);
    }
    
    public (string owner, string repo, int number)? ParseIssueUrl(string url)
    {
        // https://github.com/owner/repo/issues/123
        var match = Regex.Match(url, @"github\.com/([^/]+)/([^/]+)/issues/(\d+)");
        if (!match.Success) return null;
        return (match.Groups[1].Value, match.Groups[2].Value, int.Parse(match.Groups[3].Value));
    }
}
```

### Configuration

```csharp
public class GitHubOptions
{
    public string? Token { get; set; }  // PAT with repo scope
    public string BaseUrl { get; set; } = "https://api.github.com";
}

// In Program.cs
services.Configure<GitHubOptions>(config.GetSection("GitHub"));
services.AddHttpClient<IGitHubService, GitHubService>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<GitHubOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
    client.DefaultRequestHeaders.Add("User-Agent", "Aura/1.2.0");
    if (!string.IsNullOrEmpty(options.Token))
    {
        client.DefaultRequestHeaders.Authorization = new("Bearer", options.Token);
    }
});
```

## API Endpoints

### Create Story from Issue

```
POST /api/stories/from-issue
{
    "issueUrl": "https://github.com/org/repo/issues/123",
    "mode": "conversational",           // or "structured"
    "createWorktree": true              // default: true
}

Response:
{
    "id": "guid",
    "title": "Add retry logic to PaymentService",
    "description": "Issue body markdown...",
    "issueUrl": "https://github.com/org/repo/issues/123",
    "issueNumber": 123,
    "mode": "conversational",
    "worktreePath": "/path/to/.worktrees/story-123",
    "gitBranch": "story/123-add-retry-logic"
}
```

### Refresh from Issue

```
POST /api/stories/{id}/refresh

Response:
{
    "updated": true,
    "changes": ["title", "description"]
}
```

### Post Update to Issue

```
POST /api/stories/{id}/post-update
{
    "message": "Started work in branch `story/123-add-retry-logic`"
}

→ Posts to GitHub:
🤖 **Aura Update** (2026-01-13 14:30 UTC)

Started work in branch `story/123-add-retry-logic`
```

### Close Issue

```
POST /api/stories/{id}/close-issue
{
    "comment": "Completed via PR #456"  // optional
}
```

### List Active Stories

```
GET /api/stories/active?repositoryPath=/path/to/repo

Response:
{
    "stories": [
        {
            "id": "guid",
            "title": "Add retry logic",
            "issueNumber": 123,
            "mode": "conversational",
            "status": "Executing",
            "worktreePath": "..."
        }
    ]
}
```

## Extension UI Changes

### Story List View

Replace workflow-centric view with story-centric view:

```
┌─────────────────────────────────────┐
│ 📋 Active Stories                   │
├─────────────────────────────────────┤
│ ▶ #123 Add retry logic      [Conv]  │
│   └ 🌿 story/123-add-retry          │
│                                     │
│ ▶ #124 Fix notification bug [Struct]│
│   └ 🌿 story/124-fix-notif          │
│   └ Step 2/5: Implementing...       │
│                                     │
│ ▶ #125 Update docs          [Conv]  │
│   └ 🌿 story/125-update-docs        │
│                                     │
│ + New Story from Issue...           │
│ + New Story (no issue)...           │
└─────────────────────────────────────┘
```

### Commands

| Command | Description |
|---------|-------------|
| `aura.createStoryFromIssue` | Prompt for GitHub URL, create story |
| `aura.refreshFromIssue` | Re-import issue content |
| `aura.postUpdateToIssue` | Post status comment |
| `aura.openStoryWorktree` | Open VS Code window at worktree path |
| `aura.listActiveStories` | Show story list panel |

## Workflow (Story Lifecycle)

### Conversational Mode

```
1. User: "Create from Issue #123"
2. Aura: Fetches issue, creates Story, creates worktree
3. Aura: Posts comment "Started work in branch story/123-..."
4. User: Opens worktree in VS Code, uses GHCP Agent
5. User: "Post update" → Aura comments on issue
6. User: Creates PR manually or via Aura
7. Aura: Posts comment "PR ready: #456"
8. User: Merges PR
9. User: "Close issue" → Aura closes issue
```

### Structured Mode

```
1-3. Same as above
4. Aura: Runs planning/enrichment
5. Aura: Executes steps
6. User: Reviews each step
7. Aura: Creates PR
8. Aura: Posts comment "PR ready: #456"
9. Same as above
```

## Files to Modify

| File | Change |
|------|--------|
| `src/Aura.Module.Developer/Data/Entities/Workflow.cs` | Add issue fields, Mode enum |
| `src/Aura.Module.Developer/Data/DeveloperDbContext.cs` | Update entity config |
| `src/Aura.Module.Developer/Data/Migrations/*.cs` | New migration |
| `src/Aura.Foundation/GitHub/GitHubService.cs` | New file |
| `src/Aura.Foundation/GitHub/GitHubOptions.cs` | New file |
| `src/Aura.Api/Program.cs` | Add story endpoints |
| `extension/src/extension.ts` | Add story commands |
| `extension/package.json` | Add command contributions |

## Success Criteria

- [ ] `POST /api/stories/from-issue` creates Story with worktree
- [ ] `POST /api/stories/{id}/post-update` posts comment to GitHub
- [ ] `POST /api/stories/{id}/refresh` updates from issue
- [ ] `POST /api/stories/{id}/close-issue` closes GitHub issue
- [ ] Extension can create story from issue URL
- [ ] Extension shows active stories list
- [ ] Conversational mode skips planning/steps

## Future Enhancements

1. **Azure DevOps support** — same pattern, different API
2. **Auto-post on status change** — configurable automation
3. **Issue templates** — create issues from Aura
4. **Bulk operations** — close multiple stories

## Dependencies

- GitHub PAT with `repo` scope
- Existing worktree support (already built)

## Estimated Effort

| Task | Effort |
|------|--------|
| Schema + migration | 2 hours |
| GitHubService | 4 hours |
| API endpoints | 4 hours |
| Extension commands | 4 hours |
| Testing | 4 hours |
| **Total** | **~2-3 days** |
