# Workflow PR Creation Feature

**Status:** 🚧 In Progress  
**Created:** 2025-12-12

## Overview

When a workflow completes, provide a streamlined way to:
1. Commit any generated artifacts
2. Push the workflow branch to origin
3. Create a Pull Request back to the base branch

## User Flow

### UI Changes (Workflow Panel)

When workflow status is `Completed`:

1. Show "Complete Workflow" section with options:
   - **Commit Message** input (pre-filled with workflow title)
   - **Create PR** checkbox (default: checked)
   - **PR Title** input (pre-filled with workflow title)
   - **PR Description** textarea (pre-filled with workflow summary)
   - **Draft PR** checkbox (default: checked for safety)

2. Button: "🚀 Commit & Create PR"

3. On success, show:
   - Link to the PR
   - Option to open in browser

### API Endpoints

#### POST `/api/developer/workflows/{id}/commit`

Commits all changes in the workflow workspace.

**Request:**
```json
{
  "message": "docs: add BrightSword codebase analysis",
  "includeUntracked": true
}
```

**Response:**
```json
{
  "commitSha": "abc123",
  "filesChanged": 3,
  "insertions": 150,
  "deletions": 0
}
```

#### POST `/api/developer/workflows/{id}/push`

Pushes the workflow branch to origin.

**Request:**
```json
{
  "setUpstream": true
}
```

**Response:**
```json
{
  "pushed": true,
  "remote": "origin",
  "branch": "workflow/explore-brightsword-codebase-a261d8c2e0a64963aa36991df"
}
```

#### POST `/api/developer/workflows/{id}/create-pr`

Creates a Pull Request on GitHub.

**Request:**
```json
{
  "title": "Explore BrightSword Codebase",
  "body": "## Summary\n\nAnalysis of the BrightSword repository...",
  "baseBranch": "main",
  "draft": true
}
```

**Response:**
```json
{
  "prNumber": 42,
  "prUrl": "https://github.com/owner/repo/pull/42",
  "state": "open",
  "draft": true
}
```

#### POST `/api/developer/workflows/{id}/finalize`

Combined endpoint that does commit + push + create-pr in one call.

**Request:**
```json
{
  "commitMessage": "docs: add BrightSword codebase analysis",
  "prTitle": "Explore BrightSword Codebase", 
  "prBody": "## Summary\n\n...",
  "draft": true
}
```

**Response:**
```json
{
  "commitSha": "abc123",
  "prNumber": 42,
  "prUrl": "https://github.com/owner/repo/pull/42"
}
```

## Implementation Details

### Git Operations (LibGit2Sharp)

Use existing `GitService` to:
- Stage all changes: `git add -A`
- Commit with message
- Push to origin with upstream tracking

### GitHub API Integration

Options:
1. **GitHub CLI (`gh`)** - Shell out to `gh pr create`
   - Pros: Already authenticated, handles tokens
   - Cons: External dependency
   
2. **Octokit.NET** - Native C# GitHub API client
   - Pros: No external dependency, proper error handling
   - Cons: Need to handle auth tokens

**Recommendation:** Use GitHub CLI for MVP, migrate to Octokit later if needed.

### Configuration

Add to `appsettings.json`:
```json
{
  "GitHub": {
    "UseGitHubCli": true,
    "DefaultBaseBranch": "main",
    "CreateDraftPRs": true
  }
}
```

## Error Handling

- No changes to commit → Skip commit, proceed to PR
- Branch already pushed → Skip push, proceed to PR
- PR already exists → Return existing PR URL
- No GitHub CLI → Return error with instructions
- Auth failure → Return error with `gh auth login` instructions

## UI States

1. **Ready to finalize** - Show form with inputs
2. **Committing** - "Committing changes..."
3. **Pushing** - "Pushing to origin..."
4. **Creating PR** - "Creating pull request..."
5. **Success** - Show PR link with "Open in Browser" button
6. **Error** - Show error message with retry option

## Files to Modify

### Backend
- `src/Aura.Api/Program.cs` - Add endpoints
- `src/Aura.Module.Developer/Services/WorkflowService.cs` - Add methods
- `src/Aura.Module.Developer/Services/GitService.cs` - Add commit/push methods

### Frontend
- `extension/src/providers/workflowPanelProvider.ts` - Add finalize UI
- `extension/src/services/auraApiService.ts` - Add API methods

## MVP Scope

For initial implementation:
1. ✅ Single "Finalize" button (not separate commit/push/PR)
2. ✅ Use GitHub CLI for PR creation
3. ✅ Pre-fill commit message and PR title from workflow title
4. ✅ Always create as draft PR
5. ❌ Skip: Editable PR description (use default)
6. ❌ Skip: Non-draft PR option

## Future Enhancements

- PR templates from `.github/PULL_REQUEST_TEMPLATE.md`
- Auto-link to related issues
- Add workflow step summaries to PR description
- Support GitLab/Azure DevOps
- Octokit integration for better error handling
