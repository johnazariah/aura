# Workspace Onboarding

**Status:** ✅ Complete  
**Completed:** 2026-01-13  
**Priority:** High

## Overview

Explicit onboarding flow when a user opens a workspace in VS Code, providing clear consent and explanation before indexing begins.

## Implementation Summary

### Extension

- **Welcome View** (`welcomeViewProvider.ts`): Shows for non-onboarded workspaces with:
  - Welcome header with sparkle icon
  - Privacy explanation (all data stays local)
  - Feature list (code indexed, embeddings stored, graph built)
  - "Enable Aura for this Workspace" button
  
- **Context Management** (`extension.ts`):
  - `checkOnboardingStatus()` checks API on activation
  - Sets `aura.workspaceOnboarded` / `aura.workspaceNotOnboarded` contexts
  - Controls view visibility via `when` clauses in package.json

- **Onboard Command** (`aura.onboardWorkspace`):
  - Calls API to create workspace and start indexing
  - Updates context to show main views
  - Displays progress in status bar

### API Endpoints

| Endpoint | Purpose |
|----------|---------|
| `GET /api/workspaces/{idOrPath}` | Get workspace status by ID or path |
| `POST /api/workspaces` | Create workspace and optionally start indexing |
| `POST /api/workspaces/{id}/reindex` | Trigger re-indexing of existing workspace |
| `DELETE /api/workspaces/{id}` | Remove workspace and all indexed data |

### Database

- `workspaces` table stores:
  - `id` (deterministic from path)
  - `canonical_path`
  - `name`
  - `status` (Pending, Indexing, Ready, Error, Stale)
  - `git_remote_url`, `default_branch`
  - `created_at`, `last_accessed_at`

### Git Safe.Directory

GitService automatically handles "dubious ownership" errors by:
1. Detecting the error pattern
2. Adding path to `git config --global safe.directory`
3. Retrying the operation

## User Flow

```text
┌─ New Workspace ─────────────────────┐
│                                     │
│  🌟 Welcome to Aura                 │
│                                     │
│  Aura indexes your code locally     │
│  for AI-assisted development.       │
│                                     │
│  🔒 All data stays on your machine  │
│                                     │
│  [🚀 Enable Aura for this Workspace]│
│                                     │
└─────────────────────────────────────┘
        │
        ▼ Click "Enable"
┌─ Onboarding ────────────────────────┐
│  $(sync~spin) Indexing 45%          │
└─────────────────────────────────────┘
        │
        ▼ Complete
┌─ Normal UI ─────────────────────────┐
│  System Status                      │
│  Workflows                          │
│  Agents                             │
└─────────────────────────────────────┘
```

## Key Files

| File | Purpose |
|------|---------|
| [extension/src/providers/welcomeViewProvider.ts](../../../extension/src/providers/welcomeViewProvider.ts) | Welcome view tree provider |
| [extension/src/extension.ts](../../../extension/src/extension.ts) | `checkOnboardingStatus()`, `onboardWorkspace()` |
| [extension/src/services/auraApiService.ts](../../../extension/src/services/auraApiService.ts) | `getWorkspaceStatus()`, `onboardWorkspace()` |
| [src/Aura.Api/Program.cs](../../../src/Aura.Api/Program.cs) | Workspace endpoints |
| [src/Aura.Foundation/Git/GitService.cs](../../../src/Aura.Foundation/Git/GitService.cs) | Safe.directory handling |

## Success Criteria

- [x] New workspace shows welcome view, not empty RAG panel
- [x] "Enable Aura" button triggers onboarding and indexing
- [x] Git safe.directory handled automatically without user intervention
- [x] Returning to onboarded workspace shows stats immediately
- [x] User can remove workspace from Aura (delete index)

## Testing

```powershell
# Check workspace status
curl http://localhost:5300/api/workspaces/C%3A%5Cwork%5Caura

# Onboard new workspace
curl -X POST http://localhost:5300/api/workspaces -H "Content-Type: application/json" -d '{"path":"C:\\work\\my-project"}'

# Delete workspace
curl -X DELETE http://localhost:5300/api/workspaces/{id}
```

## Related

- [ADR: Local-First Architecture](../../adr/001-local-first.md)
- [End-User Documentation](./end-user-documentation.md)
- [Index Health Dashboard](./index-health-dashboard.md)
