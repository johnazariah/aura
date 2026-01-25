# Azure DevOps & Jira Integration

**Status:** 📋 Backlog  
**Priority:** High  
**Source:** Gap Analysis vs Birdlet/Agent Orchestrator  
**Estimated Effort:** 2-3 weeks

## Overview

Add Azure DevOps and Jira as work item sources alongside the existing GitHub Issues integration. This enables enterprise teams using these platforms to create Aura workflows directly from their work items.

## Strategic Context

Agent Orchestrator has Azure DevOps and Jira integration in its roadmap. Many enterprise teams use these platforms rather than GitHub Issues. Adding support would:
- Enable adoption in enterprises using Azure DevOps
- Support teams on Jira (very common in larger organizations)
- Create a unified interface regardless of work item source
- Position Aura as enterprise-ready

## Use Cases

1. **Azure DevOps Work Items** — "Create a workflow from ADO work item #12345"
2. **Jira Issues** — "Start working on PROJ-123 from Jira"
3. **Cross-Platform** — Teams using GitHub for code but Jira for tracking
4. **Enterprise SSO** — Azure AD authentication for ADO integration

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    Work Item Abstraction                         │
│                    IWorkItemProvider                             │
└─────────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        │                     │                     │
        ▼                     ▼                     ▼
┌───────────────┐    ┌───────────────┐    ┌───────────────┐
│ GitHub        │    │ Azure DevOps  │    │ Jira          │
│ Provider      │    │ Provider      │    │ Provider      │
│ (existing)    │    │ (new)         │    │ (new)         │
│               │    │               │    │               │
│ - Issues API  │    │ - Work Items  │    │ - Issues API  │
│ - GraphQL     │    │ - REST API    │    │ - REST API    │
│ - Webhooks    │    │ - OAuth       │    │ - OAuth/Token │
└───────────────┘    └───────────────┘    └───────────────┘
```

## Implementation

### Phase 1: Work Item Abstraction (Week 1)

**Abstract Interface:**

```csharp
namespace Aura.Foundation.WorkItems;

public interface IWorkItemProvider
{
    string ProviderId { get; }           // "github", "azure-devops", "jira"
    string DisplayName { get; }          // "GitHub Issues", "Azure DevOps", "Jira"
    
    Task<bool> TestConnectionAsync(CancellationToken ct = default);
    Task<WorkItem> GetWorkItemAsync(string workItemId, CancellationToken ct = default);
    Task<IReadOnlyList<WorkItem>> SearchAsync(WorkItemSearchRequest request, CancellationToken ct = default);
    Task UpdateStatusAsync(string workItemId, WorkItemStatus status, CancellationToken ct = default);
    Task AddCommentAsync(string workItemId, string comment, CancellationToken ct = default);
    Task<string> CreateWorkItemAsync(CreateWorkItemRequest request, CancellationToken ct = default);
}

public record WorkItem(
    string Id,
    string ProviderId,
    string Title,
    string Description,
    WorkItemStatus Status,
    string? AssignedTo,
    string[] Labels,
    string Url,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    Dictionary<string, object> CustomFields
);

public enum WorkItemStatus
{
    New,
    Active,       // In Progress
    Resolved,     // Done but not closed
    Closed,
    Blocked
}

public record WorkItemSearchRequest(
    string? Query = null,
    WorkItemStatus[]? Statuses = null,
    string? AssignedTo = null,
    string? Project = null,
    int Limit = 50
);
```

**Refactor Existing GitHub Integration:**

```csharp
// Existing GitHub code refactored to implement IWorkItemProvider
public class GitHubWorkItemProvider : IWorkItemProvider
{
    public string ProviderId => "github";
    public string DisplayName => "GitHub Issues";
    
    private readonly IGitHubClient _client;
    private readonly ILogger<GitHubWorkItemProvider> _logger;
    
    public async Task<WorkItem> GetWorkItemAsync(string workItemId, CancellationToken ct)
    {
        // Parse "owner/repo#123" format
        var (owner, repo, number) = ParseGitHubIssueId(workItemId);
        
        var issue = await _client.Issue.Get(owner, repo, number);
        
        return new WorkItem(
            Id: workItemId,
            ProviderId: "github",
            Title: issue.Title,
            Description: issue.Body ?? "",
            Status: MapGitHubState(issue.State),
            AssignedTo: issue.Assignee?.Login,
            Labels: issue.Labels.Select(l => l.Name).ToArray(),
            Url: issue.HtmlUrl,
            CreatedAt: issue.CreatedAt.UtcDateTime,
            UpdatedAt: issue.UpdatedAt?.UtcDateTime ?? issue.CreatedAt.UtcDateTime,
            CustomFields: new()
        );
    }
}
```

### Phase 2: Azure DevOps Provider (Week 1-2)

**Azure DevOps Client:**

```csharp
public class AzureDevOpsWorkItemProvider : IWorkItemProvider
{
    public string ProviderId => "azure-devops";
    public string DisplayName => "Azure DevOps";
    
    private readonly HttpClient _http;
    private readonly AzureDevOpsOptions _options;
    private readonly ILogger<AzureDevOpsWorkItemProvider> _logger;
    
    public AzureDevOpsWorkItemProvider(
        HttpClient http,
        IOptions<AzureDevOpsOptions> options,
        ILogger<AzureDevOpsWorkItemProvider> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
        
        // Set up authentication
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{_options.PersonalAccessToken}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        _http.BaseAddress = new Uri($"https://dev.azure.com/{_options.Organization}/");
    }
    
    public async Task<bool> TestConnectionAsync(CancellationToken ct)
    {
        try
        {
            var response = await _http.GetAsync($"_apis/projects?api-version=7.0", ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure DevOps connection test failed");
            return false;
        }
    }
    
    public async Task<WorkItem> GetWorkItemAsync(string workItemId, CancellationToken ct)
    {
        // Work item ID is just the number for ADO
        var response = await _http.GetAsync(
            $"{_options.Project}/_apis/wit/workitems/{workItemId}?api-version=7.0&$expand=all",
            ct);
        response.EnsureSuccessStatusCode();
        
        var ado = await response.Content.ReadFromJsonAsync<AdoWorkItem>(ct);
        
        return new WorkItem(
            Id: workItemId,
            ProviderId: "azure-devops",
            Title: ado.Fields["System.Title"]?.ToString() ?? "",
            Description: ado.Fields["System.Description"]?.ToString() ?? "",
            Status: MapAdoState(ado.Fields["System.State"]?.ToString()),
            AssignedTo: ado.Fields.GetValueOrDefault("System.AssignedTo")?.ToString(),
            Labels: ExtractTags(ado.Fields.GetValueOrDefault("System.Tags")?.ToString()),
            Url: ado.Links["html"]["href"]?.ToString() ?? "",
            CreatedAt: DateTime.Parse(ado.Fields["System.CreatedDate"]?.ToString() ?? ""),
            UpdatedAt: DateTime.Parse(ado.Fields["System.ChangedDate"]?.ToString() ?? ""),
            CustomFields: ExtractCustomFields(ado.Fields)
        );
    }
    
    public async Task<IReadOnlyList<WorkItem>> SearchAsync(WorkItemSearchRequest request, CancellationToken ct)
    {
        // Use WIQL (Work Item Query Language)
        var wiql = BuildWiqlQuery(request);
        
        var response = await _http.PostAsJsonAsync(
            $"{_options.Project}/_apis/wit/wiql?api-version=7.0",
            new { query = wiql },
            ct);
        response.EnsureSuccessStatusCode();
        
        var queryResult = await response.Content.ReadFromJsonAsync<WiqlQueryResult>(ct);
        
        // Fetch full work items (batch)
        var ids = queryResult.WorkItems.Take(request.Limit).Select(w => w.Id);
        return await GetWorkItemsByIdsAsync(ids, ct);
    }
    
    private string BuildWiqlQuery(WorkItemSearchRequest request)
    {
        var conditions = new List<string> { "[System.TeamProject] = @project" };
        
        if (request.Statuses?.Any() == true)
        {
            var states = string.Join("', '", request.Statuses.Select(MapStatusToAdoState));
            conditions.Add($"[System.State] IN ('{states}')");
        }
        
        if (!string.IsNullOrEmpty(request.AssignedTo))
        {
            conditions.Add($"[System.AssignedTo] = '{request.AssignedTo}'");
        }
        
        if (!string.IsNullOrEmpty(request.Query))
        {
            conditions.Add($"[System.Title] CONTAINS '{request.Query}'");
        }
        
        return $"SELECT [System.Id] FROM WorkItems WHERE {string.Join(" AND ", conditions)} ORDER BY [System.ChangedDate] DESC";
    }
    
    public async Task UpdateStatusAsync(string workItemId, WorkItemStatus status, CancellationToken ct)
    {
        var patch = new[]
        {
            new { op = "replace", path = "/fields/System.State", value = MapStatusToAdoState(status) }
        };
        
        var response = await _http.PatchAsJsonAsync(
            $"{_options.Project}/_apis/wit/workitems/{workItemId}?api-version=7.0",
            patch,
            ct);
        response.EnsureSuccessStatusCode();
    }
    
    public async Task AddCommentAsync(string workItemId, string comment, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync(
            $"{_options.Project}/_apis/wit/workitems/{workItemId}/comments?api-version=7.0-preview.3",
            new { text = comment },
            ct);
        response.EnsureSuccessStatusCode();
    }
    
    private static WorkItemStatus MapAdoState(string? state) => state?.ToLower() switch
    {
        "new" => WorkItemStatus.New,
        "active" or "doing" or "in progress" => WorkItemStatus.Active,
        "resolved" or "done" => WorkItemStatus.Resolved,
        "closed" or "removed" => WorkItemStatus.Closed,
        "blocked" => WorkItemStatus.Blocked,
        _ => WorkItemStatus.New
    };
    
    private static string MapStatusToAdoState(WorkItemStatus status) => status switch
    {
        WorkItemStatus.New => "New",
        WorkItemStatus.Active => "Active",
        WorkItemStatus.Resolved => "Resolved",
        WorkItemStatus.Closed => "Closed",
        WorkItemStatus.Blocked => "Blocked",
        _ => "New"
    };
}

public record AzureDevOpsOptions
{
    public string Organization { get; init; } = "";
    public string Project { get; init; } = "";
    public string PersonalAccessToken { get; init; } = "";
}
```

### Phase 3: Jira Provider (Week 2)

**Jira Client:**

```csharp
public class JiraWorkItemProvider : IWorkItemProvider
{
    public string ProviderId => "jira";
    public string DisplayName => "Jira";
    
    private readonly HttpClient _http;
    private readonly JiraOptions _options;
    
    public JiraWorkItemProvider(HttpClient http, IOptions<JiraOptions> options)
    {
        _http = http;
        _options = options.Value;
        
        // Basic auth or API token
        var credentials = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{_options.Email}:{_options.ApiToken}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        _http.BaseAddress = new Uri(_options.BaseUrl);
    }
    
    public async Task<WorkItem> GetWorkItemAsync(string workItemId, CancellationToken ct)
    {
        // Jira key format: PROJ-123
        var response = await _http.GetAsync($"/rest/api/3/issue/{workItemId}", ct);
        response.EnsureSuccessStatusCode();
        
        var jira = await response.Content.ReadFromJsonAsync<JiraIssue>(ct);
        
        return new WorkItem(
            Id: workItemId,
            ProviderId: "jira",
            Title: jira.Fields.Summary,
            Description: ConvertAtlassianDocToMarkdown(jira.Fields.Description),
            Status: MapJiraStatus(jira.Fields.Status?.Name),
            AssignedTo: jira.Fields.Assignee?.DisplayName,
            Labels: jira.Fields.Labels ?? [],
            Url: $"{_options.BaseUrl}/browse/{workItemId}",
            CreatedAt: jira.Fields.Created,
            UpdatedAt: jira.Fields.Updated,
            CustomFields: ExtractCustomFields(jira.Fields)
        );
    }
    
    public async Task<IReadOnlyList<WorkItem>> SearchAsync(WorkItemSearchRequest request, CancellationToken ct)
    {
        var jql = BuildJqlQuery(request);
        
        var response = await _http.GetAsync(
            $"/rest/api/3/search?jql={Uri.EscapeDataString(jql)}&maxResults={request.Limit}",
            ct);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<JiraSearchResult>(ct);
        
        return result.Issues.Select(MapJiraIssueToWorkItem).ToList();
    }
    
    private string BuildJqlQuery(WorkItemSearchRequest request)
    {
        var conditions = new List<string>();
        
        if (!string.IsNullOrEmpty(request.Project))
        {
            conditions.Add($"project = {request.Project}");
        }
        
        if (request.Statuses?.Any() == true)
        {
            var statuses = string.Join(", ", request.Statuses.Select(s => $"\"{MapStatusToJiraStatus(s)}\""));
            conditions.Add($"status IN ({statuses})");
        }
        
        if (!string.IsNullOrEmpty(request.AssignedTo))
        {
            conditions.Add($"assignee = \"{request.AssignedTo}\"");
        }
        
        if (!string.IsNullOrEmpty(request.Query))
        {
            conditions.Add($"text ~ \"{request.Query}\"");
        }
        
        var jql = conditions.Count > 0 ? string.Join(" AND ", conditions) : "ORDER BY updated DESC";
        return jql + " ORDER BY updated DESC";
    }
    
    public async Task UpdateStatusAsync(string workItemId, WorkItemStatus status, CancellationToken ct)
    {
        // Jira requires transition IDs, need to get available transitions first
        var transitionsResponse = await _http.GetAsync($"/rest/api/3/issue/{workItemId}/transitions", ct);
        var transitions = await transitionsResponse.Content.ReadFromJsonAsync<JiraTransitionsResult>(ct);
        
        var targetStatus = MapStatusToJiraStatus(status);
        var transition = transitions.Transitions.FirstOrDefault(t => 
            t.To.Name.Equals(targetStatus, StringComparison.OrdinalIgnoreCase));
        
        if (transition != null)
        {
            await _http.PostAsJsonAsync(
                $"/rest/api/3/issue/{workItemId}/transitions",
                new { transition = new { id = transition.Id } },
                ct);
        }
    }
    
    public async Task AddCommentAsync(string workItemId, string comment, CancellationToken ct)
    {
        await _http.PostAsJsonAsync(
            $"/rest/api/3/issue/{workItemId}/comment",
            new { body = ConvertMarkdownToAtlassianDoc(comment) },
            ct);
    }
    
    private static WorkItemStatus MapJiraStatus(string? status) => status?.ToLower() switch
    {
        "to do" or "open" or "backlog" => WorkItemStatus.New,
        "in progress" or "in development" => WorkItemStatus.Active,
        "done" or "resolved" => WorkItemStatus.Resolved,
        "closed" => WorkItemStatus.Closed,
        "blocked" => WorkItemStatus.Blocked,
        _ => WorkItemStatus.New
    };
}

public record JiraOptions
{
    public string BaseUrl { get; init; } = "";    // https://your-org.atlassian.net
    public string Email { get; init; } = "";
    public string ApiToken { get; init; } = "";
    public string DefaultProject { get; init; } = "";
}
```

### Phase 4: Integration & UI (Week 3)

**Unified Work Item Service:**

```csharp
public class WorkItemService : IWorkItemService
{
    private readonly IEnumerable<IWorkItemProvider> _providers;
    
    public async Task<WorkItem> GetWorkItemAsync(string providerId, string workItemId, CancellationToken ct)
    {
        var provider = _providers.FirstOrDefault(p => p.ProviderId == providerId)
            ?? throw new ArgumentException($"Unknown provider: {providerId}");
        
        return await provider.GetWorkItemAsync(workItemId, ct);
    }
    
    public async Task<IReadOnlyList<(string ProviderId, bool Connected)>> GetConnectionStatusAsync(CancellationToken ct)
    {
        var results = await Task.WhenAll(_providers.Select(async p => 
            (p.ProviderId, await p.TestConnectionAsync(ct))));
        return results;
    }
}
```

**API Endpoints:**

```csharp
// Work items API group
var workItems = app.MapGroup("/api/work-items").WithTags("Work Items");

workItems.MapGet("/providers", async (IWorkItemService service, CancellationToken ct) =>
{
    var status = await service.GetConnectionStatusAsync(ct);
    return Results.Ok(status);
})
.WithName("GetWorkItemProviders");

workItems.MapGet("/{provider}/{id}", async (
    string provider,
    string id,
    IWorkItemService service,
    CancellationToken ct) =>
{
    var item = await service.GetWorkItemAsync(provider, id, ct);
    return Results.Ok(item);
})
.WithName("GetWorkItem");

workItems.MapGet("/{provider}/search", async (
    string provider,
    [AsParameters] WorkItemSearchRequest request,
    IWorkItemService service,
    CancellationToken ct) =>
{
    var items = await service.SearchAsync(provider, request, ct);
    return Results.Ok(items);
})
.WithName("SearchWorkItems");

// Workflow creation from work item
app.MapPost("/api/developer/workflows/from-work-item", async (
    CreateWorkflowFromWorkItemRequest request,
    IWorkItemService workItemService,
    IWorkflowService workflowService,
    CancellationToken ct) =>
{
    var workItem = await workItemService.GetWorkItemAsync(request.Provider, request.WorkItemId, ct);
    
    var workflow = await workflowService.CreateAsync(new CreateWorkflowRequest(
        Title: workItem.Title,
        Description: workItem.Description,
        Source: $"{request.Provider}:{request.WorkItemId}",
        SourceUrl: workItem.Url,
        WorkspaceId: request.WorkspaceId
    ), ct);
    
    return Results.Created($"/api/developer/workflows/{workflow.Id}", workflow);
})
.WithName("CreateWorkflowFromWorkItem");
```

**VS Code Extension Updates:**

```typescript
// extension/src/providers/workItemTreeProvider.ts
export class WorkItemTreeProvider implements vscode.TreeDataProvider<WorkItemNode> {
    private _onDidChangeTreeData = new vscode.EventEmitter<WorkItemNode | undefined>();
    readonly onDidChangeTreeData = this._onDidChangeTreeData.event;
    
    async getChildren(element?: WorkItemNode): Promise<WorkItemNode[]> {
        if (!element) {
            // Root: show providers
            const status = await this.auraClient.get('/api/work-items/providers');
            return status
                .filter(p => p.Connected)
                .map(p => new WorkItemNode(p.ProviderId, 'provider'));
        }
        
        if (element.type === 'provider') {
            // Provider level: show work items
            const items = await this.auraClient.get(
                `/api/work-items/${element.id}/search?statuses=New,Active&limit=20`
            );
            return items.map(i => new WorkItemNode(i.Id, 'work-item', i));
        }
        
        return [];
    }
}

// Commands
vscode.commands.registerCommand('aura.createWorkflowFromWorkItem', async (node: WorkItemNode) => {
    if (node.type !== 'work-item') return;
    
    const workspaceId = await getActiveWorkspaceId();
    await auraClient.post('/api/developer/workflows/from-work-item', {
        provider: node.provider,
        workItemId: node.id,
        workspaceId
    });
    
    vscode.window.showInformationMessage(`Created workflow from ${node.workItem.Title}`);
    vscode.commands.executeCommand('aura.refreshWorkflows');
});
```

**Package.json Updates:**

```json
{
    "contributes": {
        "views": {
            "aura-workflows": [
                {
                    "id": "auraWorkItems",
                    "name": "Work Items",
                    "when": "aura.hasWorkItemProviders"
                }
            ]
        },
        "commands": [
            {
                "command": "aura.createWorkflowFromWorkItem",
                "title": "Create Workflow",
                "icon": "$(add)"
            }
        ],
        "menus": {
            "view/item/context": [
                {
                    "command": "aura.createWorkflowFromWorkItem",
                    "when": "view == auraWorkItems && viewItem == work-item"
                }
            ]
        }
    }
}
```

## Configuration

```json
{
    "WorkItems": {
        "Providers": {
            "GitHub": {
                "Enabled": true
                // Uses existing GITHUB_TOKEN env var
            },
            "AzureDevOps": {
                "Enabled": false,
                "Organization": "your-org",
                "Project": "your-project",
                "PersonalAccessToken": ""  // Or use env: AZURE_DEVOPS_PAT
            },
            "Jira": {
                "Enabled": false,
                "BaseUrl": "https://your-org.atlassian.net",
                "Email": "",
                "ApiToken": "",           // Or use env: JIRA_API_TOKEN
                "DefaultProject": "PROJ"
            }
        }
    }
}
```

**Environment Variables:**

```bash
# Azure DevOps
AZURE_DEVOPS_ORG=your-org
AZURE_DEVOPS_PROJECT=your-project
AZURE_DEVOPS_PAT=your-personal-access-token

# Jira
JIRA_BASE_URL=https://your-org.atlassian.net
JIRA_EMAIL=your-email@example.com
JIRA_API_TOKEN=your-api-token
```

## Success Criteria

- [ ] Azure DevOps work items can be fetched by ID
- [ ] Jira issues can be fetched by key (PROJ-123)
- [ ] Work item search works for all providers
- [ ] Status updates sync back to source system
- [ ] Comments can be added from Aura
- [ ] VS Code tree view shows work items from all connected providers
- [ ] Workflows can be created from any work item source
- [ ] Connection status shows in system status panel

## Testing

```csharp
[Fact]
public async Task AzureDevOpsProvider_GetWorkItem_ReturnsCorrectFields()
{
    var provider = CreateTestProvider();
    var item = await provider.GetWorkItemAsync("12345", CancellationToken.None);
    
    item.Id.Should().Be("12345");
    item.ProviderId.Should().Be("azure-devops");
    item.Title.Should().NotBeEmpty();
}

[Fact]
public async Task JiraProvider_Search_ReturnsResults()
{
    var provider = CreateTestProvider();
    var results = await provider.SearchAsync(new WorkItemSearchRequest(
        Project: "TEST",
        Statuses: [WorkItemStatus.Active]
    ), CancellationToken.None);
    
    results.Should().NotBeEmpty();
    results.Should().OnlyContain(i => i.Status == WorkItemStatus.Active);
}

[Fact]
public async Task WorkItemService_HandlesMultipleProviders()
{
    var service = new WorkItemService([_githubProvider, _adoProvider, _jiraProvider]);
    
    var github = await service.GetWorkItemAsync("github", "owner/repo#1", default);
    var ado = await service.GetWorkItemAsync("azure-devops", "123", default);
    var jira = await service.GetWorkItemAsync("jira", "PROJ-456", default);
    
    github.ProviderId.Should().Be("github");
    ado.ProviderId.Should().Be("azure-devops");
    jira.ProviderId.Should().Be("jira");
}
```

## Future Enhancements

1. **Webhooks** — Real-time updates when work items change
2. **Bidirectional Sync** — Create issues in ADO/Jira from Aura
3. **Azure AD SSO** — Enterprise single sign-on for ADO
4. **Custom Field Mapping** — Map custom fields to workflow metadata
5. **Bulk Operations** — Create multiple workflows from a query
6. **Sprint/Iteration Support** — Filter by sprint in ADO
