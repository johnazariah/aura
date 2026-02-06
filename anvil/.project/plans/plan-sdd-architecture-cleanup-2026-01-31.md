# Plan: SDD Architecture Cleanup

**Backlog Item:** Architecture refactoring to cleanly separate SDD framework from project-specific content
**Created:** 2026-01-31
**Status:** Ready for Implementation

## Problem Statement

The current `.sdd/` directory conflates two concerns:
1. **SDD Framework** - The reusable methodology, prompts, and templates
2. **Project Instance** - Anvil-specific ADRs, architecture, coding guidelines

This makes `.sdd/` non-portable. Copying it to a new project brings Anvil-specific decisions.

Additionally, `.copilot-tracking/` is an awkward name that doesn't convey its purpose.

## Solution

Split into two directories with clear responsibilities:

| Directory | Purpose | Portable? |
|-----------|---------|-----------|
| `.sdd/` | SDD framework (methodology, prompts, templates) | ✅ Yes |
| `.project/` | Project-specific content (vision, ADRs, architecture, work tracking) | ❌ No |

## Success Criteria

- [ ] `.sdd/` contains ONLY framework content (philosophy, prompts, templates)
- [ ] `.project/` contains ALL project-specific content
- [ ] `.copilot-tracking/` is removed (contents moved to `.project/`)
- [ ] All internal references updated to new paths
- [ ] Agents reference correct locations
- [ ] Documentation explains the separation clearly

---

## Implementation Steps

### Step 1: Create `.project/` Directory Structure

Create the new directory structure:

```
.project/
├── VISION.md                      # Moved from .copilot-tracking/
├── STATUS.md                      # New: quick project status
├── adr/                           # Moved from .sdd/ADR/
├── architecture/                  # Moved from .sdd/architecture/
├── coding-guidelines/             # Moved from .sdd/coding-guidelines/
├── backlog/                       # Moved from .copilot-tracking/backlog/
├── research/                      # Moved from .copilot-tracking/research/
├── plans/                         # Moved from .copilot-tracking/plans/
├── changes/                       # Moved from .copilot-tracking/changes/
├── reviews/                       # Moved from .copilot-tracking/reviews/
├── handoffs/                      # Moved from .copilot-tracking/handoffs/
└── completed/                     # Moved from .copilot-tracking/completed/
    ├── backlog/
    ├── research/
    ├── plans/
    ├── changes/
    └── reviews/
```

**Verification:** Directory structure exists.

---

### Step 2: Move Project-Specific Content from `.sdd/` to `.project/`

#### 2.1: Move ADRs

```powershell
# Move .sdd/ADR/* → .project/adr/
```

Files to move:
- `ADR-001-testing.md`
- `ADR-002-dependency-injection.md`
- `ADR-003-logging.md`
- `ADR-004-cross-cutting-concerns.md`
- `ADR-005-database-strategy.md`
- `ADR-006-environment-configuration.md`
- `ADR-007-vscode-extension-testing.md`
- `ADR-008-story-source-strategy.md`
- `ADR-009-authentication-handling.md`
- `ADR-010-workspace-isolation.md`
- `ADR-011-concurrency-strategy.md`
- `ADR-012-retry-flakiness-strategy.md`
- `ADR-013-report-formats.md`
- `ADR-014-git-operations.md`
- `ADR-015-build-test-invocation.md`
- `ADR-016-cli-defaults.md`
- `ADR-017-test-fixture-templates.md`

**Verification:** All ADR files exist in `.project/adr/`, none remain in `.sdd/ADR/`.

#### 2.2: Move Architecture Docs

```powershell
# Move .sdd/architecture/* → .project/architecture/
```

Files to move:
- `project.md` → rename to `overview.md`
- `principles.md`

**Verification:** Files exist in `.project/architecture/`.

#### 2.3: Move Coding Guidelines

```powershell
# Move .sdd/coding-guidelines/* → .project/coding-guidelines/
```

Files to move:
- `csharp.md`

**Verification:** File exists in `.project/coding-guidelines/`.

#### 2.4: Remove Empty Directories from `.sdd/`

After moving, remove:
- `.sdd/ADR/` (directory)
- `.sdd/architecture/` (directory)
- `.sdd/coding-guidelines/` (directory)

---

### Step 3: Move Content from `.copilot-tracking/` to `.project/`

#### 3.1: Move VISION.md

```powershell
# Move .copilot-tracking/VISION.md → .project/VISION.md
```

#### 3.2: Move Work Tracking Folders

```powershell
# Move entire folders
.copilot-tracking/backlog/     → .project/backlog/
.copilot-tracking/research/    → .project/research/
.copilot-tracking/plans/       → .project/plans/
.copilot-tracking/changes/     → .project/changes/
.copilot-tracking/reviews/     → .project/reviews/
.copilot-tracking/handoffs/    → .project/handoffs/
.copilot-tracking/completed/   → .project/completed/
```

#### 3.3: Remove `.copilot-tracking/` Directory

After all content is moved, remove the directory.

**Verification:** `.copilot-tracking/` no longer exists.

---

### Step 4: Update Internal References

#### 4.1: Update `.sdd/README.md`

Replace all occurrences:
- `.copilot-tracking/` → `.project/`
- `.sdd/ADR/` → `.project/adr/`
- `.sdd/architecture/` → `.project/architecture/`
- `.sdd/coding-guidelines/` → `.project/coding-guidelines/`

Add new section explaining the separation:

```markdown
## Directory Structure

SDD uses two directories:

| Directory | Contains | Copy to New Projects? |
|-----------|----------|----------------------|
| `.sdd/` | Framework (methodology, prompts, templates) | ✅ Yes |
| `.project/` | Your project (vision, ADRs, architecture, work) | ❌ No |

### `.sdd/` - The Framework

```
.sdd/
├── README.md           # This file
├── LICENSE             # MIT license
├── philosophy.md       # SDD methodology
├── prompts/            # Workflow prompts
│   ├── setup.md
│   ├── commit.md
│   ├── complete.md
│   ├── context.md
│   └── merge.md
└── templates/          # Scaffolding templates
    └── AGENTS.md
```

### `.project/` - Your Project

```
.project/
├── VISION.md           # Product vision
├── STATUS.md           # Current state
├── adr/                # Architecture Decision Records
├── architecture/       # System architecture docs
├── coding-guidelines/  # Language-specific standards
├── backlog/            # Work items (what to build)
├── research/           # Research artifacts
├── plans/              # Implementation plans
├── changes/            # Change logs
├── reviews/            # Review results
├── handoffs/           # Session handoffs
└── completed/          # Archived completed work
```
```

#### 4.2: Update `.sdd/philosophy.md`

Replace all path references:
- `.copilot-tracking/research/` → `.project/research/`
- `.copilot-tracking/plans/` → `.project/plans/`
- `.copilot-tracking/changes/` → `.project/changes/`
- `.copilot-tracking/reviews/` → `.project/reviews/`

#### 4.3: Update `.sdd/prompts/context.md`

Replace:
- `.copilot-tracking/backlog` → `.project/backlog`
- `.copilot-tracking/` → `.project/`

#### 4.4: Update `.sdd/prompts/complete.md`

Replace all `.copilot-tracking/` → `.project/`

#### 4.5: Update `.sdd/templates/AGENTS.md`

Replace:
- `.sdd/ADR/` → `.project/adr/`
- `.sdd/architecture/` → `.project/architecture/`
- `.sdd/coding-guidelines/` → `.project/coding-guidelines/`
- All ADR links to use new paths

---

### Step 5: Update Agent Definitions

#### 5.1: Update `.github/agents/research.agent.md`

Replace:
- `.copilot-tracking/backlog/` → `.project/backlog/`
- `.copilot-tracking/research/` → `.project/research/`

#### 5.2: Update `.github/agents/plan.agent.md`

Replace:
- `.copilot-tracking/research/` → `.project/research/`
- `.copilot-tracking/plans/` → `.project/plans/`

#### 5.3: Update `.github/agents/implement.agent.md`

Replace:
- `.copilot-tracking/plans/` → `.project/plans/`
- `.copilot-tracking/changes/` → `.project/changes/`

#### 5.4: Update `.github/agents/verify.agent.md`

Replace:
- `.copilot-tracking/changes/` → `.project/changes/`
- `.copilot-tracking/reviews/` → `.project/reviews/`

#### 5.5: Update `.github/agents/next-backlog-item.agent.md`

Replace:
- `.copilot-tracking/backlog/` → `.project/backlog/`

#### 5.6: Update `.github/agents/backlog-builder.agent.md`

Replace:
- `.copilot-tracking/backlog/` → `.project/backlog/`
- `VISION.md` reference if it mentions `.copilot-tracking/`

#### 5.7: Update `.github/agents/docs.agent.md`

Replace any `.copilot-tracking/` → `.project/`

#### 5.8: Update `.github/agents/release.agent.md`

Replace any `.copilot-tracking/` → `.project/`

---

### Step 6: Update `.project/architecture/overview.md` (formerly `project.md`)

Update the SDD Workflow section with new paths:

```markdown
### Artifact Locations

| Phase | Output Location | Naming Convention |
|-------|-----------------|-------------------|
| Research | `.project/research/` | `research-{item}-{YYYY-MM-DD}.md` |
| Plan | `.project/plans/` | `plan-{item}-{YYYY-MM-DD}.md` |
| Implement | `src/`, `tests/`, `.project/changes/` | `changes-{item}-{YYYY-MM-DD}.md` |
| Validate | `.project/reviews/` | `review-{item}-{YYYY-MM-DD}.md` |

**Note:** Files in `.project/**` are exempt from repository linting rules.
```

Also update ADR links from `.sdd/ADR/` → `.project/adr/`.

---

### Step 7: Create `.project/STATUS.md`

Create a new status file:

```markdown
# Anvil Status

> Quick reference for current project state.

## Current Phase

**Phase:** Active Development

## Recently Completed

| Date | Item | Summary |
|------|------|---------|
| 2026-01-31 | Story Execution Core | Basic story loading and validation |

## In Progress

| Item | Phase | Next Step |
|------|-------|-----------|
| SDD Architecture Cleanup | Implementing | Complete file moves |

## Backlog Priorities

See [backlog/](backlog/) for full list.

1. Indexing Effectiveness
2. Issue-to-PR Pipeline
3. MCP Tool Effectiveness
```

---

### Step 8: Update `.gitignore` if Needed

Check if `.gitignore` references `.copilot-tracking/`. If so, update to `.project/`.

---

### Step 9: Create `.project/README.md`

Create an index file:

```markdown
# Anvil Project Documentation

This directory contains all project-specific documentation and work tracking.

## Quick Links

| Document | Purpose |
|----------|---------|
| [VISION.md](VISION.md) | Product vision and success criteria |
| [STATUS.md](STATUS.md) | Current project state |
| [architecture/](architecture/) | System architecture |
| [adr/](adr/) | Architecture Decision Records |
| [coding-guidelines/](coding-guidelines/) | Language-specific standards |

## Work Tracking

| Folder | Contains |
|--------|----------|
| [backlog/](backlog/) | Work items to be done |
| [research/](research/) | Research artifacts |
| [plans/](plans/) | Implementation plans |
| [changes/](changes/) | Change documentation |
| [reviews/](reviews/) | Review results |
| [handoffs/](handoffs/) | Session handoff notes |
| [completed/](completed/) | Archived completed work |

## For AI Assistants

Read these in order:
1. `VISION.md` - What we're building
2. `STATUS.md` - Where we are
3. `architecture/overview.md` - How it works
4. Relevant ADRs for your task
```

---

### Step 10: Final Verification

Run the following checks:

1. **No orphaned references:**
   ```powershell
   git grep -r "\.copilot-tracking" -- "*.md" "*.yaml" "*.json"
   ```
   Should return no results.

2. **No references to old .sdd paths:**
   ```powershell
   git grep -r "\.sdd/ADR\|\.sdd/architecture\|\.sdd/coding-guidelines" -- "*.md"
   ```
   Should return no results.

3. **Directory structure correct:**
   ```powershell
   Test-Path ".project/VISION.md"
   Test-Path ".project/adr/ADR-001-testing.md"
   Test-Path ".project/architecture/overview.md"
   Test-Path ".sdd/philosophy.md"
   -not (Test-Path ".copilot-tracking")
   -not (Test-Path ".sdd/ADR")
   ```
   All should be True.

---

## Files Changed Summary

### Created
- `.project/README.md`
- `.project/STATUS.md`

### Moved (old → new)
- `.sdd/ADR/*` → `.project/adr/*`
- `.sdd/architecture/project.md` → `.project/architecture/overview.md`
- `.sdd/architecture/principles.md` → `.project/architecture/principles.md`
- `.sdd/coding-guidelines/csharp.md` → `.project/coding-guidelines/csharp.md`
- `.copilot-tracking/VISION.md` → `.project/VISION.md`
- `.copilot-tracking/backlog/*` → `.project/backlog/*`
- `.copilot-tracking/research/*` → `.project/research/*`
- `.copilot-tracking/plans/*` → `.project/plans/*`
- `.copilot-tracking/changes/*` → `.project/changes/*`
- `.copilot-tracking/reviews/*` → `.project/reviews/*`
- `.copilot-tracking/handoffs/*` → `.project/handoffs/*`
- `.copilot-tracking/completed/*` → `.project/completed/*`

### Modified
- `.sdd/README.md` - Updated paths
- `.sdd/philosophy.md` - Updated paths
- `.sdd/prompts/context.md` - Updated paths
- `.sdd/prompts/complete.md` - Updated paths
- `.sdd/templates/AGENTS.md` - Updated paths
- `.github/agents/*.agent.md` - All 8 agents updated
- `.project/architecture/overview.md` - Updated paths

### Deleted
- `.sdd/ADR/` (directory, after move)
- `.sdd/architecture/` (directory, after move)
- `.sdd/coding-guidelines/` (directory, after move)
- `.copilot-tracking/` (entire directory, after move)

---

## Notes for Implementation

1. **Use `git mv`** for all file moves to preserve history
2. **Commit in logical chunks:**
   - Commit 1: Create `.project/` structure and move files
   - Commit 2: Update all references
   - Commit 3: Remove old directories
3. **Test agents** after changes to ensure they find correct paths
