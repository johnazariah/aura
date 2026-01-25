---
agent: agent
description: Merge a git worktree back to main with full quality validation - a local PR ceremony.
---

# Merge Worktree to Main

Merge work from a git worktree back to the main branch with full quality validation.
This is a **local PR ceremony** — treat it with the same rigor as an actual pull request.

---

## Prerequisites

- You are in a git worktree (not the main repo)
- The worktree has commits ready to merge
- Main branch is clean and up-to-date

---

## Phase 1: Identify Context

1. **Confirm worktree status**:
   ```powershell
   git worktree list
   git branch --show-current
   git status --short
   ```

2. **Identify the main repo path**:
   ```powershell
   # Worktrees are typically at: C:\work\aura-worktrees\{branch-name}
   # Main repo is typically at: C:\work\aura
   $mainRepo = "c:\work\aura"
   ```

3. **Get story/feature context** (if using Aura workflows):
   ```powershell
   # Check if there's an associated story
   curl -s "http://localhost:5300/api/developer/stories" | ConvertFrom-Json | 
     Where-Object { $_.worktreePath -eq (Get-Location).Path }
   ```

4. **List commits to be merged**:
   ```powershell
   git log main..HEAD --oneline
   ```

5. **Report to user**:
   ```
   ## Worktree Merge Analysis

   Worktree: C:\work\aura-worktrees\feature-xyz
   Branch: feature/xyz
   Main repo: C:\work\aura

   ### Commits to merge:
   - abc1234 feat: add new capability
   - def5678 test: add tests for capability
   - ghi9012 docs: update documentation

   ### Files changed:
   N files changed, X insertions(+), Y deletions(-)
   ```

---

## Phase 2: Quality Gates

**All gates must pass before merging. Stop and fix any failures.**

### Gate 1: Clean Working Tree

```powershell
$status = git status --porcelain
if ($status) {
    Write-Error "Working tree not clean. Commit or stash changes first."
    exit 1
}
```

### Gate 2: Build Succeeds

```powershell
dotnet build -c Release --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed. Fix errors before merging."
    exit 1
}
```

### Gate 3: All Tests Pass

```powershell
# Run unit tests
dotnet test tests/Aura.Foundation.Tests -c Release --verbosity minimal
dotnet test tests/Aura.Module.Developer.Tests -c Release --verbosity minimal

# Total should be 656+ tests passing
```

### Gate 4: No Secrets or Credentials

```powershell
# The pre-commit hook checks this, but verify explicitly
.\scripts\hooks\pre-commit.ps1
```

### Gate 5: Documentation Updated (if applicable)

Check if the changes require documentation updates:

- [ ] New MCP tools documented in `prompts/mcp-tools-instructions.md`
- [ ] New features reflected in README
- [ ] ADR created for architectural decisions
- [ ] Feature spec in `.project/features/` if completing a feature

### Gate 6: Commit Messages Follow Convention

```powershell
git log main..HEAD --pretty=format:"%s" | ForEach-Object {
    if ($_ -notmatch "^(feat|fix|docs|chore|refactor|test|style|perf|ci|build)(\(.+\))?:") {
        Write-Warning "Non-conventional commit: $_"
    }
}
```

---

## Phase 3: Prepare for Merge

1. **Ensure main is up-to-date**:
   ```powershell
   git -C $mainRepo fetch origin
   git -C $mainRepo status
   # Should show "Your branch is up to date with 'origin/main'"
   ```

2. **Rebase onto latest main** (recommended for clean history):
   ```powershell
   git fetch origin main
   git rebase origin/main
   
   # If conflicts occur, resolve them and continue:
   # git rebase --continue
   ```

3. **Squash commits if needed** (optional, for cleaner history):
   ```powershell
   # Interactive rebase to squash/fixup commits
   git rebase -i main
   
   # Or create a single squash commit
   git reset --soft main
   git commit -m "feat(module): complete feature X

   - Added capability A
   - Updated component B
   - Added tests for C"
   ```

---

## Phase 4: Execute Merge

1. **Switch to main repo**:
   ```powershell
   Set-Location $mainRepo
   ```

2. **Merge the worktree branch**:
   ```powershell
   $worktreeBranch = "feature/xyz"  # Replace with actual branch
   
   # Fast-forward merge (if rebased)
   git merge $worktreeBranch --ff-only
   
   # Or regular merge (creates merge commit)
   git merge $worktreeBranch --no-ff -m "Merge $worktreeBranch"
   ```

3. **Verify merge succeeded**:
   ```powershell
   git log --oneline -5
   dotnet build -c Release --verbosity minimal
   ```

---

## Phase 5: Post-Merge Validation

1. **Run full test suite in main**:
   ```powershell
   dotnet test tests/Aura.Foundation.Tests -c Release --verbosity minimal
   dotnet test tests/Aura.Module.Developer.Tests -c Release --verbosity minimal
   ```

2. **Build extension** (if extension changes):
   ```powershell
   .\scripts\Build-Extension.ps1 -SkipInstall
   ```

3. **Verify database compatibility** (if schema changes):
   ```powershell
   # Drop and recreate to verify migrations work
   dotnet ef database drop --project src/Aura.Module.Developer --startup-project src/Aura.Api --force
   dotnet ef database update --project src/Aura.Module.Developer --startup-project src/Aura.Api
   ```

---

## Phase 6: Push and Cleanup

1. **Push to origin**:
   ```powershell
   git push origin main
   ```

2. **Delete the worktree branch** (if no longer needed):
   ```powershell
   git branch -d $worktreeBranch
   ```

3. **Remove the worktree directory**:
   ```powershell
   git worktree remove $worktreePath
   # Or manually: Remove-Item -Recurse -Force $worktreePath
   ```

4. **Update story status** (if using Aura workflows):
   ```powershell
   # Mark story as complete via API
   curl -X PATCH "http://localhost:5300/api/developer/stories/$storyId" `
     -H "Content-Type: application/json" `
     -d '{"status": "Completed"}'
   ```

---

## Phase 7: Report Summary

Provide a summary to the user:

```
## Merge Complete ✅

| Phase | Status |
|-------|--------|
| Build | ✅ Passed |
| Tests | ✅ 656 passing |
| Merge | ✅ Fast-forward to main |
| Push | ✅ origin/main updated |
| Cleanup | ✅ Worktree removed |

### Commits merged:
- abc1234 feat: add new capability
- def5678 test: add tests

### Next steps:
- [ ] Consider creating a release if this is a significant feature
- [ ] Update STATUS.md if needed
- [ ] Close related GitHub issue if applicable
```

---

## Rollback Procedure

If something goes wrong after merging:

```powershell
# Find the commit before merge
git log --oneline -10

# Reset to previous state
git reset --hard <commit-before-merge>

# Force push (DANGEROUS - only if you haven't pushed yet)
git push origin main --force
```

---

## Checklist Summary

Before merging, ensure:

- [ ] Working tree is clean (no uncommitted changes)
- [ ] Build succeeds with zero warnings
- [ ] All 656+ tests pass
- [ ] No secrets in commits
- [ ] Commit messages follow conventional format
- [ ] Documentation updated for new features
- [ ] Main branch is up-to-date with origin
- [ ] Rebased onto latest main (recommended)

After merging, ensure:

- [ ] Tests still pass in main
- [ ] Extension builds (if changed)
- [ ] Database migrations work (if schema changed)
- [ ] Pushed to origin
- [ ] Worktree cleaned up
- [ ] Story/issue updated
