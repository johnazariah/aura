---
description: Create a new feature from scratch — spec, branch, worktree, and initial scaffold.
---

# Start Feature

You are starting a new feature for the Aura project. Follow these steps to create the spec, branch, and worktree.

## Step 1: Create Feature Spec

Create a feature spec file in `.project/features/in-progress/` using the template at `.project/templates/feature-spec.md`.

```powershell
# Naming convention: kebab-case, descriptive
# Example: .project/features/in-progress/mcp-tool-caching.md
```

The spec must include:
- **Title** and one-sentence summary
- **Status:** 🔄 In Progress
- **Problem statement** — what's wrong or missing
- **Proposed solution** — how to fix it
- **Acceptance criteria** — measurable conditions for completion
- **Affected files** — which source files will change

## Step 2: Update Features README

Add the new feature to `.project/features/README.md` in the "In Progress" section:

```markdown
| [Feature Name](in-progress/feature-name.md) | Brief description |
```

## Step 3: Create Git Branch

```powershell
# From the main repo (c:\work\aura)
git checkout -b feature/{feature-name-slug}
```

Use kebab-case matching the spec filename.

## Step 4: Create Worktree (Optional)

If working in isolation from main:

```powershell
# Create worktree for isolated development
git worktree add "C:\work\aura-worktrees\{feature-name}" feature/{feature-name-slug}
Set-Location "C:\work\aura-worktrees\{feature-name}"
```

## Step 5: Create Story in Aura (Optional)

If using Aura's story management:

```powershell
curl -X POST "http://localhost:5300/api/developer/stories" `
  -H "Content-Type: application/json" `
  -d '{
    "title": "{Feature Title}",
    "description": "{Feature description with acceptance criteria}",
    "repositoryPath": "c:/work/aura"
  }'
```

## Step 6: Report to User

```
## Feature Started ✅

| Item | Value |
|------|-------|
| Spec | `.project/features/in-progress/{name}.md` |
| Branch | `feature/{name}` |
| Worktree | `C:\work\aura-worktrees\{name}` (if created) |

### Next steps:
- [ ] Review and refine the feature spec
- [ ] Begin implementation
- [ ] When done, use `aura.complete-feature.prompt.md` to finalize
```

## Checklist

- [ ] Feature spec created with acceptance criteria
- [ ] Features README updated
- [ ] Git branch created with conventional name
- [ ] Worktree created (if isolated development needed)
- [ ] Feature spec reviewed with user before implementation begins
