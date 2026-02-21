# StoryProgressCommentService

## Overview

`StoryProgressCommentService` posts real-time progress comments to the GitHub issue linked to a story during its execution. This keeps stakeholders informed of where Aura is in the implementation lifecycle without them having to poll the Aura UI.

The service is registered as `IStoryProgressCommentService` and is injected into `StoryService`, which calls it at the following lifecycle points:

| Lifecycle point | Comment posted |
|---|---|
| Analysis complete | Issue analysis is done; planning is next |
| Planning complete | Plan created with step count; execution is starting |
| Wave complete | A parallel wave of steps finished successfully |
| PR ready | Implementation done; PR link included |

If GitHub integration is not configured (`IGitHubService.IsConfigured == false`), every method logs a warning and returns without throwing. This ensures that missing GitHub credentials never fail a story.

---

## Interface

```csharp
public interface IStoryProgressCommentService
{
    Task PostAnalysisCompleteCommentAsync(
        string owner, string repo, int issueNumber, CancellationToken ct);

    Task PostPlanningCompleteCommentAsync(
        string owner, string repo, int issueNumber, int stepCount, CancellationToken ct);

    Task PostWaveCompleteCommentAsync(
        string owner, string repo, int issueNumber, int waveNumber, int totalWaves, CancellationToken ct);

    Task PostPRReadyCommentAsync(
        string owner, string repo, int issueNumber, string prLink, CancellationToken ct);
}
```

---

## Methods

### `PostAnalysisCompleteCommentAsync`

Posts a comment indicating that Aura has finished analysing the issue and is about to begin planning.

| Parameter | Type | Description |
|---|---|---|
| `owner` | `string` | GitHub repository owner (user or organisation) |
| `repo` | `string` | GitHub repository name |
| `issueNumber` | `int` | Issue number to comment on |
| `ct` | `CancellationToken` | Cancellation token |

**Comment posted:**
```
🔍 **Analysis complete.** Aura has finished analysing the issue and is now planning implementation steps.
```

---

### `PostPlanningCompleteCommentAsync`

Posts a comment indicating that Aura has produced an execution plan and is starting to run it.

| Parameter | Type | Description |
|---|---|---|
| `owner` | `string` | GitHub repository owner |
| `repo` | `string` | GitHub repository name |
| `issueNumber` | `int` | Issue number to comment on |
| `stepCount` | `int` | Total number of steps in the plan |
| `ct` | `CancellationToken` | Cancellation token |

**Comment posted (example for 4 steps):**
```
📋 **Planning complete.** Aura has created a plan with **4 steps** and is now executing it.
```

---

### `PostWaveCompleteCommentAsync`

Posts a comment when a parallel wave of steps completes successfully. Only posted when the wave has zero failures.

| Parameter | Type | Description |
|---|---|---|
| `owner` | `string` | GitHub repository owner |
| `repo` | `string` | GitHub repository name |
| `issueNumber` | `int` | Issue number to comment on |
| `waveNumber` | `int` | 1-based index of the completed wave |
| `totalWaves` | `int` | Total number of waves in the plan |
| `ct` | `CancellationToken` | Cancellation token |

**Comment posted (example for wave 2 of 4):**
```
⚡ **Wave 2/4 complete.** Aura has finished executing wave 2 of 4.
```

---

### `PostPRReadyCommentAsync`

Posts a comment when Aura has successfully created a pull request, including a link to it.

| Parameter | Type | Description |
|---|---|---|
| `owner` | `string` | GitHub repository owner |
| `repo` | `string` | GitHub repository name |
| `issueNumber` | `int` | Issue number to comment on |
| `prLink` | `string` | Full URL of the created pull request |
| `ct` | `CancellationToken` | Cancellation token |

**Comment posted:**
```
✅ **Pull request ready for review.** Aura has finished implementing this story.

👉 [View Pull Request](https://github.com/owner/repo/pull/42)
```

---

## Integration with StoryService

`StoryService` receives `IStoryProgressCommentService` via constructor injection and calls it at the points shown below.

### Wave complete (in `ExecuteAsync`)

```csharp
// After all steps in a wave finish with no failures:
if (failedCount == 0
    && story.IssueOwner is not null
    && story.IssueRepo is not null
    && story.IssueNumber is not null)
{
    await _progressCommentService.PostWaveCompleteCommentAsync(
        story.IssueOwner, story.IssueRepo, story.IssueNumber.Value,
        currentWave, waveCount, ct);
}
```

### PR ready (in `CompleteAsync`)

```csharp
if (prResult.Success && prResult.Value is not null)
{
    workflow.PullRequestUrl = prResult.Value.Url;

    if (workflow.IssueOwner is not null
        && workflow.IssueRepo is not null
        && workflow.IssueNumber is not null)
    {
        await _progressCommentService.PostPRReadyCommentAsync(
            workflow.IssueOwner, workflow.IssueRepo, workflow.IssueNumber.Value,
            prResult.Value.Url, ct);
    }
}
```

The guard checks (`IssueOwner is not null`, etc.) ensure comments are only attempted for stories that were created from a GitHub issue URL.

---

## Error Handling

`StoryProgressCommentService` calls `EnsureConfigured()` at the top of every method:

```csharp
private bool EnsureConfigured()
{
    if (_gitHub.IsConfigured)
    {
        return true;
    }

    _logger.LogWarning("GitHub integration is not configured; skipping progress comment.");
    return false;
}
```

If `IGitHubService.IsConfigured` is `false` (e.g. no PAT configured), the method returns immediately and story execution continues normally. No exception is thrown.

---

## Registration

The service is registered by `DeveloperModule` as a scoped dependency:

```csharp
services.AddScoped<IStoryProgressCommentService, StoryProgressCommentService>();
```

---

## See Also

- [`IGitHubService`](../../src/Aura.Module.Developer/GitHub/GitHubService.cs) – underlying GitHub API client used to post comments via `PostCommentAsync`
- [`StoryService`](../../src/Aura.Module.Developer/Services/StoryService.cs) – orchestrates story execution and calls this service at lifecycle points
- [`IStoryProgressCommentService`](../../src/Aura.Module.Developer/Services/IStoryProgressCommentService.cs) – interface definition
