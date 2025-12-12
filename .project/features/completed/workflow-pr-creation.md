# Workflow PR Creation Feature

**Status:** ✅ Complete  
**Completed:** 2025-12-12  
**Last Updated:** 2025-12-12

## Overview

When a workflow completes, provide a streamlined way to:
1. Commit any generated artifacts
2. Push the workflow branch to origin
3. Create a Pull Request back to the base branch

## Implementation Summary

### Backend (Aura.Api)

- **POST `/api/developer/workflows/{id}/complete`** - Marks workflow complete, commits changes, pushes, and creates draft PR
- **POST `/api/developer/workflows/{id}/finalize`** - Explicit finalize with options for commit message, PR title, draft mode
- `WorkflowService.CompleteAsync()` - Full PR creation flow
- `GitService.CreatePullRequestAsync()` - Uses GitHub CLI (`gh pr create`)
- `Workflow.PullRequestUrl` property stored in database

### Frontend (VS Code Extension)

- Finalize dialog with form inputs:
  - Commit Message (pre-filled with workflow title)
  - Create PR checkbox
  - PR Title
  - Draft PR checkbox
- "🚀 Finalize & Create PR" button for completed workflows without PR
- "🔗 View Pull Request" button with link for already-finalized workflows
- Loading states during commit/push/PR operations
- Success message with option to open PR in browser

## API Endpoints

### POST `/api/developer/workflows/{id}/finalize`

Combined endpoint that commits, pushes, and creates PR.

**Request:**
```json
{
  "commitMessage": "feat: workflow changes",
  "createPullRequest": true,
  "prTitle": "Workflow Title",
  "prBody": "## Summary\n\n...",
  "baseBranch": "main",
  "draft": true
}
```

**Response:**
```json
{
  "workflowId": "...",
  "commitSha": "abc123",
  "pushed": true,
  "prNumber": 42,
  "prUrl": "https://github.com/owner/repo/pull/42",
  "message": "Workflow finalized. PR created: ..."
}
```

## Files Modified

### Backend
- `src/Aura.Api/Program.cs` - `/complete` and `/finalize` endpoints
- `src/Aura.Module.Developer/Services/WorkflowService.cs` - `CompleteAsync()` with PR creation
- `src/Aura.Module.Developer/Data/Entities/Workflow.cs` - `PullRequestUrl` property
- `src/Aura.Module.Developer/Data/DeveloperDbContext.cs` - Column mapping
- `src/Aura.Foundation/Git/GitService.cs` - `CreatePullRequestAsync()`, `DeleteBranchAsync()`
- `scripts/recreate-developer-schema.sql` - `pull_request_url` column

### Frontend
- `extension/src/providers/workflowPanelProvider.ts` - Finalize dialog, PR link display
- `extension/src/services/auraApiService.ts` - `finalizeWorkflow()`, `pullRequestUrl` on Workflow

## Error Handling

- No changes to commit → Skip commit, proceed to PR
- Branch already pushed → Skip push, proceed to PR  
- No GitHub CLI → Returns error with instructions
- Auth failure → Returns error with `gh auth login` guidance

## Future Enhancements

- PR templates from `.github/PULL_REQUEST_TEMPLATE.md`
- Auto-link to related issues
- Support GitLab/Azure DevOps
- Octokit.NET integration for better error handling
