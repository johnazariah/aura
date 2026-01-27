# Worktree Index Detection

**Status:** ✅ Complete
**Completed:** 2026-01-28
**Created:** 2025-01-23

## Overview

When opening VS Code in a git worktree, the extension correctly detects the parent repository's index and shows the Stories view instead of the "Welcome to Aura" onboarding view.

## Problem Statement

When opening VS Code in a git worktree, the extension incorrectly showed the "Welcome to Aura" view and prompted the user to "Enable Aura for this Workspace", even when the parent repository was already indexed.

The RAG status bar correctly showed "X files (via parent)" indicating it detected the worktree relationship, but the onboarding logic didn't use the same detection.

## Root Cause

Two separate issues:

1. **`indexWorkspace()` didn't use canonical path**: When user clicked "Onboard to Aura" in a worktree, the function used the worktree path directly instead of the parent's canonical path.

2. **Timing issue on activation**: `checkOnboardingStatus()` ran at extension activation. If the API wasn't ready yet, the call failed and defaulted to `isOnboarded: false`.

## Solution

### Fix 1: Use canonical path in `indexWorkspace()`

**File:** `extension/src/extension.ts`

```typescript
const workspacePath = workspaceFolders[0].uri.fsPath;
const repoInfo = await gitService.getRepositoryInfo(workspacePath);
const canonicalPath = repoInfo?.canonicalPath ?? workspacePath;
const isWorktree = repoInfo?.isWorktree ?? false;

const status = await auraApiService.getWorkspaceStatus(canonicalPath);
```

All subsequent API calls use `canonicalPath` and show appropriate messages for worktrees.

### Fix 2: Update context when health check succeeds

**File:** `extension/src/services/healthCheckService.ts`

When the RAG health check detects the workspace is indexed (directly or via parent), it updates the VS Code context:

```typescript
if (canonicalPath && (repoDocuments > 0 || repoChunks > 0 || graphNodes > 0)) {
    await vscode.commands.executeCommand('setContext', 'aura.workspaceOnboarded', true);
    await vscode.commands.executeCommand('setContext', 'aura.workspaceNotOnboarded', false);
}
```

## Implementation

- [x] `indexWorkspace()` updated to use canonical path
- [x] Added worktree-specific messaging ("Indexing parent repository...")
- [x] Health check updates onboarding context when indexed
- [x] `gitService.getRepositoryInfo()` returns canonical path for worktrees

## Files Changed

- `extension/src/extension.ts` - `indexWorkspace()` function
- `extension/src/services/healthCheckService.ts` - `checkRag()` method
- `extension/src/services/gitService.ts` - `getRepositoryInfo()` with worktree detection

## Testing

1. Create a story via API (creates worktree)
2. Open worktree in new VS Code window
3. Verify Stories view shows (not Welcome view)
4. Verify status bar shows "X files (via parent)"
