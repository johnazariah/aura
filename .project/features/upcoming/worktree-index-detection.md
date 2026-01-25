# Worktree Index Detection

**Status:** 🔧 In Progress (code complete, pending rebuild)
**Priority:** High (blocks workflow testing)
**Created:** 2025-01-23

## Problem Statement

When opening VS Code in a git worktree, the extension incorrectly shows the "Welcome to Aura" view and prompts the user to "Enable Aura for this Workspace", even when the parent repository is already indexed.

The RAG status bar correctly shows "X files (via parent)" indicating it detects the worktree relationship, but the onboarding logic doesn't use the same detection.

## Root Cause

Two separate issues:

1. **`indexWorkspace()` doesn't use canonical path**: When user clicks "Onboard to Aura" in a worktree, the function uses the worktree path directly instead of the parent's canonical path, causing it to try to index the worktree as a separate workspace.

2. **Timing issue on activation**: `checkOnboardingStatus()` runs at extension activation. If the API isn't ready yet, the call fails and defaults to `isOnboarded: false`, showing the welcome view even when the parent is indexed.

## Solution

### Fix 1: Use canonical path in `indexWorkspace()`

**File:** `extension/src/extension.ts`

Before:
```typescript
const workspacePath = workspaceFolders[0].uri.fsPath;
const status = await auraApiService.getWorkspaceStatus(workspacePath);
```

After:
```typescript
const workspacePath = workspaceFolders[0].uri.fsPath;
const repoInfo = await gitService.getRepositoryInfo(workspacePath);
const canonicalPath = repoInfo?.canonicalPath ?? workspacePath;
const isWorktree = repoInfo?.isWorktree ?? false;

const status = await auraApiService.getWorkspaceStatus(canonicalPath);
```

Also update all subsequent API calls to use `canonicalPath` and show appropriate messages for worktrees.

### Fix 2: Update context when health check succeeds

**File:** `extension/src/services/healthCheckService.ts`

When the RAG health check detects the workspace is indexed (directly or via parent), update the VS Code context:

```typescript
if (canonicalPath && (repoDocuments > 0 || repoChunks > 0 || graphNodes > 0)) {
    // ... existing code ...
    
    // Update onboarding context - workspace is indexed
    await vscode.commands.executeCommand('setContext', 'aura.workspaceOnboarded', true);
    await vscode.commands.executeCommand('setContext', 'aura.workspaceNotOnboarded', false);
}
```

## Implementation Status

- [x] `indexWorkspace()` updated to use canonical path
- [x] Added worktree-specific messaging ("Indexing parent repository...")
- [x] Health check updates onboarding context when indexed
- [ ] Extension rebuild required
- [ ] Manual testing in worktree

## Testing

1. Create a story via API (creates worktree)
2. Open worktree in new VS Code window
3. Verify Stories view shows (not Welcome view)
4. Verify status bar shows "X files (via parent)"
5. If Welcome view appears, wait for health check and verify it switches

## Files Changed

- `extension/src/extension.ts` - `indexWorkspace()` function
- `extension/src/services/healthCheckService.ts` - `checkRag()` method
