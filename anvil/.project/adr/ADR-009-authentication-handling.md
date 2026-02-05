---
title: "ADR-009: Authentication Handling"
status: "Accepted"
date: "2026-01-30"
authors: "Anvil Team"
tags: ["security", "authentication", "github"]
supersedes: ""
superseded_by: ""
---

# ADR-009: Authentication Handling

## Status

Accepted

## Context

Anvil interacts with multiple systems that may require authentication:

| System | Auth Required | Phase |
|--------|---------------|-------|
| **Aura API** (`localhost:5300`) | No | Phase 1 |
| **VS Code Extension** | No (local) | Phase 1 |
| **GitHub API** (Issues, PRs) | Yes | Phase 2 |

The Aura API is a local server accessed by the local VS Code Extension—no authentication needed.

GitHub integration (Phase 2) will require authentication to:
- Read issues from repositories
- Validate PRs created by Aura
- Post status updates or comments

## Decision

We adopt a **tiered authentication** approach:

### Phase 1: No Authentication Required

Aura API and VS Code Extension testing require no authentication:

```csharp
public class AuraClient : IAuraClient
{
    private readonly HttpClient _httpClient;
    private readonly AnvilOptions _options;

    public AuraClient(HttpClient httpClient, IOptions<AnvilOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _httpClient.BaseAddress = new Uri(_options.AuraBaseUrl);
        // No auth headers needed for local Aura
    }

    public async Task<Result<StoryResponse, AuraError>> ExecuteStoryAsync(
        string storyContent, 
        CancellationToken ct)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/developer/workflows", 
            new { content = storyContent }, ct);
        // ...
    }
}
```

### Phase 2: GitHub Authentication

For GitHub API access, we'll use a **Personal Access Token (PAT)** for a dedicated test account.

#### Token Storage

```csharp
public class GitHubOptions
{
    public string? PersonalAccessToken { get; set; }
    public string? TestRepository { get; set; }  // "owner/repo"
}
```

#### Configuration Sources (by precedence)

```bash
# Environment variable (preferred for CI)
export ANVIL__GitHub__PersonalAccessToken="ghp_xxxxxxxxxxxx"
export ANVIL__GitHub__TestRepository="aura-test/anvil-fixtures"

# Or appsettings.json (local dev - DO NOT COMMIT TOKENS)
{
  "GitHub": {
    "PersonalAccessToken": "ghp_xxxxxxxxxxxx",
    "TestRepository": "aura-test/anvil-fixtures"
  }
}
```

#### GitHub Client

```csharp
public class GitHubStorySource : IStorySource
{
    private readonly GitHubClient _client;
    private readonly GitHubOptions _options;

    public GitHubStorySource(IOptions<GitHubOptions> options)
    {
        _options = options.Value;
        _client = new GitHubClient(new ProductHeaderValue("Anvil"))
        {
            Credentials = new Credentials(_options.PersonalAccessToken)
        };
    }

    public async Task<Result<Story, StoryError>> LoadStoryAsync(
        string issueUrl, 
        CancellationToken ct)
    {
        // Parse owner/repo/issue from URL
        var issue = await _client.Issue.Get(owner, repo, issueNumber);
        // Extract story from issue body
        return ParseStoryFromIssue(issue);
    }
}
```

### Test Account Setup

For integration testing, we'll create:

| Resource | Purpose |
|----------|---------|
| **GitHub Account** | `aura-test` (or similar) - dedicated test account |
| **Test Repository** | `aura-test/anvil-fixtures` - contains test issues |
| **PAT Scopes** | `repo` (read issues, PRs, create comments) |

#### Required PAT Scopes

```
repo
  ├── repo:status      - Access commit status
  ├── repo_deployment  - Not needed
  ├── public_repo      - Access public repos
  └── repo:invite      - Not needed

read:org               - Not needed unless testing org repos
```

### Validation at Startup

```csharp
public class GitHubAuthValidator : IHostedService
{
    private readonly GitHubOptions _options;
    private readonly ILogger<GitHubAuthValidator> _logger;

    public async Task StartAsync(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_options.PersonalAccessToken))
        {
            _logger.LogWarning("GitHub PAT not configured. GitHub integration disabled.");
            return;
        }

        try
        {
            var client = new GitHubClient(new ProductHeaderValue("Anvil"))
            {
                Credentials = new Credentials(_options.PersonalAccessToken)
            };
            var user = await client.User.Current();
            _logger.LogInformation("GitHub authenticated as {User}", user.Login);
        }
        catch (AuthorizationException)
        {
            _logger.LogError("GitHub PAT is invalid. GitHub integration disabled.");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

### Security Considerations

| Concern | Mitigation |
|---------|------------|
| **Token exposure** | Never commit tokens; use env vars in CI |
| **Minimal scopes** | Request only needed permissions |
| **Test isolation** | Use dedicated test account, not personal |
| **Token rotation** | Document how to rotate PAT |
| **Secrets in logs** | Never log token values |

```csharp
// ❌ Bad: Logging secrets
_logger.LogInformation("Using token: {Token}", _options.PersonalAccessToken);

// ✅ Good: Log presence, not value
_logger.LogInformation("GitHub PAT configured: {IsConfigured}", 
    !string.IsNullOrEmpty(_options.PersonalAccessToken));
```

## Consequences

**Positive**
- **POS-001**: Phase 1 works immediately with no auth setup
- **POS-002**: GitHub auth is optional (graceful degradation)
- **POS-003**: Standard PAT approach, well-documented
- **POS-004**: Test account isolates from production data
- **POS-005**: Environment variables work in CI/CD

**Negative**
- **NEG-001**: PAT needs periodic rotation
- **NEG-002**: Test account requires maintenance
- **NEG-003**: Rate limits apply to GitHub API

## Alternatives Considered

### Alternative 1: GitHub App Authentication
- **Description**: Create a GitHub App instead of using PAT
- **Rejection Reason**: More complex setup; overkill for test tooling

### Alternative 2: OAuth Flow
- **Description**: Interactive OAuth login
- **Rejection Reason**: Not suitable for CLI/CI automation

### Alternative 3: No GitHub Integration
- **Description**: Only support file-based stories
- **Rejection Reason**: Limits end-to-end testing capability

## Implementation Notes

- **IMP-001**: Phase 1 requires no auth configuration
- **IMP-002**: GitHub integration is opt-in (works without PAT)
- **IMP-003**: Use `Octokit.NET` for GitHub API
- **IMP-004**: Create test account: `aura-test` with repo `anvil-fixtures`
- **IMP-005**: Store PAT in CI secrets, never in code
- **IMP-006**: Document PAT creation in README

## References

- [GitHub Personal Access Tokens](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/creating-a-personal-access-token)
- [Octokit.NET Authentication](https://octokitnet.readthedocs.io/en/latest/getting-started/#authentication)
- [ADR-006: Environment Configuration](ADR-006-environment-configuration.md)
