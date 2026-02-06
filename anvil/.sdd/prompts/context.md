# Refresh Project Context

Read key project files to understand current state. Use this when starting a new session or returning to a project.

## Instructions

### Step 1: Read Vision and Backlog

```powershell
# Check for vision
if (Test-Path "VISION.md") {
    # Read it
}

# Check for backlog
if (Test-Path ".project/backlog") {
    Get-ChildItem ".project/backlog/*.md"
}
```

### Step 2: Check Work in Progress

Scan `.project/` for incomplete work:

| Folder | State | Meaning |
|--------|-------|---------|
| `research/` without `plans/` | Research done | Needs planning |
| `plans/` without `changes/` | Planned | Needs implementation |
| `changes/` without `reviews/` | Implemented | Needs review |

### Step 3: Check Git Status

```powershell
git status --short
git log --oneline -5
git branch --show-current
```

### Step 4: Check for TODOs

```powershell
git grep -n "TODO\|FIXME\|HACK\|XXX" -- "*.cs" "*.ts" "*.py" "*.go" "*.rs" | Select-Object -First 10
```

### Step 5: Report Context

```
## 📍 Project Context

**Project:** {name from package.json, Cargo.toml, etc.}
**Branch:** {current branch}
**Last commit:** {short hash} {message}

### Vision
{One-line from VISION.md or "No vision defined"}

### Backlog
| Priority | Item | Status |
|----------|------|--------|
| High | story-execution | Ready for research |
| Medium | issue-integration | In progress (planning) |

### Work in Progress
- **{item}** is in **{phase}** phase
  - {what's done, what's next}

### Uncommitted Changes
{list or "Clean working tree"}

### Open TODOs: {count}
{first few or "None found"}

---

What would you like to do?
- `@next-backlog-item` - Pick next work item
- `@backlog-builder` - Add to backlog
- Continue WIP: `@{phase} .project/{folder}/{file}.md`
```

## Purpose

This prompt provides situational awareness without taking any action. It's the "where am I?" command for a project.
