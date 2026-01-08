# Workspace Onboarding

**Status:** 🔄 In Progress  
**Priority:** High  
**Created:** 2026-01-08

## Overview

Provide an explicit onboarding flow when a user opens a workspace in VS Code, rather than showing RAG/indexing UI for workspaces that haven't been set up.

## Problem Statement

Currently:

- RAG Index panel shows for all workspaces, even if not indexed
- Progress shows "0% complete" or errors for non-onboarded repos
- Service may need to configure git `safe.directory` without user awareness
- No explanation of what Aura does or what data is stored

## Goals

1. **Explicit consent** - User actively chooses to enable Aura for each workspace
2. **Clear explanation** - User understands what indexing does and where data is stored
3. **Clean UI** - No confusing states for workspaces that aren't set up
4. **Graceful setup** - Handle git safe.directory and other setup transparently
5. **Per-workspace settings** - Foundation for future customization (exclude patterns, etc.)

## User Flow

### First Time Opening Workspace

1. User opens workspace in VS Code
2. Extension checks if workspace is onboarded via `/api/workspace/{path}/status`
3. If not onboarded:
   - Hide RAG Index tree view (or show minimal "Not configured" state)
   - Show "Aura Setup" welcome view with:
     - Brief explanation of Aura's capabilities
     - What will be indexed (code, markdown, etc.)
     - Where data is stored (local PostgreSQL)
     - Privacy assurance (all local, nothing sent to cloud)
   - "Enable Aura for this Workspace" button
4. On button click:
   - Call `/api/workspace/{path}/onboard`
   - API handles git safe.directory if needed
   - Triggers indexing
   - Extension switches to normal RAG Index view with progress

### Returning to Onboarded Workspace

1. Extension checks status → workspace is onboarded
2. Show normal RAG Index panel with stats/health
3. Background refresh if index is stale (optional, configurable)

## API Design

### GET /api/workspace/{path}/status

Returns onboarding status for a workspace.

```json
{
  "path": "c:\\work\\aura",
  "isOnboarded": true,
  "onboardedAt": "2026-01-08T12:00:00Z",
  "lastIndexedAt": "2026-01-08T14:30:00Z",
  "indexHealth": "fresh",
  "stats": {
    "files": 470,
    "chunks": 8009,
    "graphNodes": 2585
  }
}
```

### POST /api/workspace/{path}/onboard

Onboards a workspace: configures git, triggers indexing, stores metadata.

Request:

```json
{
  "path": "c:\\work\\aura",
  "options": {
    "includePatterns": ["*.cs", "*.md", "*.ts"],
    "excludePatterns": ["**/node_modules/**", "**/bin/**"]
  }
}
```

Response:

```json
{
  "success": true,
  "jobId": "abc-123",
  "message": "Indexing started",
  "setupActions": [
    "Added c:/work/aura to git safe.directory"
  ]
}
```

### DELETE /api/workspace/{path}

Removes workspace from Aura (deletes index data, metadata).

## Data Model

Extend `index_metadata` table or create new `workspaces` table:

```sql
CREATE TABLE workspaces (
  id UUID PRIMARY KEY,
  path TEXT NOT NULL UNIQUE,
  onboarded_at TIMESTAMPTZ NOT NULL,
  last_indexed_at TIMESTAMPTZ,
  settings JSONB,  -- include/exclude patterns, refresh policy
  created_at TIMESTAMPTZ DEFAULT NOW()
);
```

## Extension Changes

### New Welcome View

In `package.json`, add a welcome view for the Aura container:

```json
"views": {
  "aura": [
    {
      "id": "aura.welcome",
      "name": "Welcome",
      "when": "aura.workspaceNotOnboarded"
    },
    {
      "id": "aura.ragIndex", 
      "name": "RAG Index",
      "when": "aura.workspaceOnboarded"
    }
  ]
}
```

### Context Management

- Set `aura.workspaceOnboarded` context based on API response
- Refresh on workspace change
- Handle multi-root workspaces (check each folder)

### Welcome View Content

Use VS Code's `WebviewView` or tree view with welcome content:

```text
┌─────────────────────────────────────┐
│  🌟 Welcome to Aura                 │
│                                     │
│  Aura indexes your code locally to  │
│  provide AI-assisted development.   │
│                                     │
│  What happens when you enable Aura: │
│  • Code and docs are indexed        │
│  • Embeddings stored in local DB    │
│  • Code graph built for navigation  │
│  • All data stays on your machine   │
│                                     │
│  [Enable Aura for this Workspace]   │
│                                     │
│  Learn more: docs/getting-started   │
└─────────────────────────────────────┘
```

## Setup Actions (Transparent to User)

When onboarding, the API should handle:

1. **Git safe.directory** - Auto-configure if running as different user
2. **Initial index** - Queue full indexing job
3. **Workspace metadata** - Store in database
4. **Return setup actions** - So extension can show what was done (optional toast)

## Settings (Future)

Per-workspace settings stored in `workspaces.settings`:

```json
{
  "includePatterns": ["*.cs", "*.md"],
  "excludePatterns": ["**/bin/**", "**/obj/**"],
  "autoRefresh": true,
  "refreshOnSave": false
}
```

## Edge Cases

1. **Multi-root workspace** - Each folder can be onboarded independently
2. **Workspace moved/renamed** - Match by normalized path, handle gracefully
3. **Service not running** - Show connection error, not onboarding UI
4. **Already indexing** - Show progress, don't re-trigger

## Success Criteria

- [ ] New workspace shows welcome view, not empty RAG panel
- [ ] "Enable Aura" button triggers onboarding and indexing
- [ ] Git safe.directory handled automatically without user intervention
- [ ] Returning to onboarded workspace shows stats immediately
- [ ] User can remove workspace from Aura (delete index)

## Implementation Order

1. API: `/api/workspace/{path}/status` endpoint
2. API: `/api/workspace/{path}/onboard` endpoint  
3. Extension: Check onboarding status on activation
4. Extension: Welcome view with enable button
5. Extension: Context switching between views
6. API: Automatic git safe.directory handling (already done)
7. Extension: Polish UX, add settings page

## Related

- [ADR: Local-First Architecture](../../adr/001-local-first.md)
- [Coding Standards](../../standards/coding-standards.md)
