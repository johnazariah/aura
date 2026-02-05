# Complete Backlog Item Ceremony

Move a completed backlog item through the SDD tracking structure and update all documentation.

## Input

Provide the backlog item name that was completed:
```
/complete story-execution-core
```

## Instructions

### Step 1: Locate All Artifacts

Find the related files in `.project/`:

```
.project/
├── backlog/
│   └── {item-name}.md          ← Original backlog item
├── research/
│   └── research-{item-name}-{date}.md
├── plans/
│   └── plan-{item-name}-{date}.md
├── changes/
│   └── changes-{item-name}-{date}.md
└── reviews/
    └── review-{item-name}-{date}.md   ← Should show ✅ Approved
```

### Step 2: Verify Review Status

Read the review document and confirm:
- Verdict is `✅ Approved`
- All requirements were met

If not approved, stop and inform user:
```
⚠️ Cannot complete: Review shows "Needs Work"

Address the issues in the review first, then run @verify again.
```

### Step 3: Update Backlog Item

Add completion status to the original backlog item:

```markdown
# Backlog: {Item Name}

**Status:** ✅ Complete
**Completed:** {YYYY-MM-DD}

...rest of file...
```

### Step 4: Archive Artifacts

Move all related artifacts to a `completed/` subfolder:

```powershell
# Create completed folders if needed
New-Item -ItemType Directory -Force -Path ".project/completed/backlog"
New-Item -ItemType Directory -Force -Path ".project/completed/research"
New-Item -ItemType Directory -Force -Path ".project/completed/plans"
New-Item -ItemType Directory -Force -Path ".project/completed/changes"
New-Item -ItemType Directory -Force -Path ".project/completed/reviews"

# Move artifacts
Move-Item ".project/backlog/{item}.md" ".project/completed/backlog/"
Move-Item ".project/research/research-{item}-*.md" ".project/completed/research/"
Move-Item ".project/plans/plan-{item}-*.md" ".project/completed/plans/"
Move-Item ".project/changes/changes-{item}-*.md" ".project/completed/changes/"
Move-Item ".project/reviews/review-{item}-*.md" ".project/completed/reviews/"
```

### Step 5: Update Vision (if applicable)

If VISION.md tracks capabilities, mark the completed capability:

```markdown
## Core Capabilities

- [x] ~~Story Execution~~ ✅ Complete (2026-01-31)
- [ ] Issue-to-PR Pipeline
- [ ] MCP Tool Effectiveness Testing
```

### Step 6: Commit the Completion

```powershell
git add .project/
git commit -m "docs(sdd): complete {item-name}

- Marked backlog item as complete
- Archived all SDD artifacts
- Updated capability tracking"
```

### Step 7: Report Summary

```
## 🎉 Backlog Item Complete

**Item:** {item-name}
**Completed:** {YYYY-MM-DD}

### Artifacts Archived:
- ✅ backlog/{item}.md → completed/backlog/
- ✅ research/research-{item}-{date}.md → completed/research/
- ✅ plans/plan-{item}-{date}.md → completed/plans/
- ✅ changes/changes-{item}-{date}.md → completed/changes/
- ✅ reviews/review-{item}-{date}.md → completed/reviews/

### Next Steps:
- Run `@next-backlog-item` to pick the next work item
- Or `@backlog-builder` to add new items
```

## Constraints

- Only complete items with approved reviews
- Always preserve the full artifact trail
- Update all tracking documents
- Use conventional commits
