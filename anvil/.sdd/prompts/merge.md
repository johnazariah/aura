# Merge Branch with Quality Gates

Merge a feature branch back to main with full quality validation. This is a local PR ceremony.

## Prerequisites

- You are on a feature branch (not main)
- All work is committed
- Ready to merge to main

## Instructions

### Phase 1: Identify Context

```powershell
# Confirm current branch
git branch --show-current

# Check for uncommitted changes
git status --short

# List commits to merge
git log main..HEAD --oneline
```

Report to user:
```
## Merge Analysis

**Branch:** feature/xyz
**Commits to merge:** 5
**Files changed:** 12

### Commits:
- abc1234 feat: add capability
- def5678 test: add tests
- ...

Proceed with quality gates? (yes / abort)
```

---

### Phase 2: Quality Gates

**All gates must pass. Stop and fix any failures.**

#### Gate 1: Clean Working Tree

```powershell
$status = git status --porcelain
if ($status) {
    Write-Error "Uncommitted changes. Commit or stash first."
}
```

#### Gate 2: Build Succeeds

```powershell
# Detect and run appropriate build
dotnet build -c Release      # .NET
npm run build                # Node
cargo build --release        # Rust
go build ./...               # Go
```

#### Gate 3: Tests Pass

```powershell
dotnet test -c Release       # .NET
npm test                     # Node
cargo test                   # Rust
go test ./...                # Go
pytest                       # Python
```

#### Gate 4: Linting Clean (if configured)

```powershell
dotnet format --verify-no-changes   # .NET
npm run lint                        # Node
cargo clippy                        # Rust
golangci-lint run                   # Go
ruff check .                        # Python
```

#### Gate 5: Commit Messages Follow Convention

```powershell
git log main..HEAD --pretty=format:"%s" | ForEach-Object {
    if ($_ -notmatch "^(feat|fix|docs|chore|refactor|test|style|perf|ci|build)(\(.+\))?:") {
        Write-Warning "Non-conventional commit: $_"
    }
}
```

Report gate status:
```
## Quality Gates

| Gate | Status |
|------|--------|
| Clean tree | ✅ |
| Build | ✅ |
| Tests | ✅ 45 passed |
| Linting | ✅ |
| Commit format | ✅ |

All gates passed. Ready to merge? (yes / abort)
```

---

### Phase 3: Prepare for Merge

```powershell
# Ensure main is up-to-date
git fetch origin main

# Rebase onto latest main (for clean history)
git rebase origin/main

# If conflicts, resolve and continue:
# git rebase --continue
```

**Optional: Squash commits** if history is noisy:
```powershell
git rebase -i main
# Mark commits as 'squash' or 'fixup' as needed
```

---

### Phase 4: Execute Merge

```powershell
# Switch to main
git checkout main

# Fast-forward merge (if rebased)
git merge feature/xyz --ff-only

# Or regular merge (creates merge commit)
git merge feature/xyz --no-ff -m "Merge feature/xyz"
```

---

### Phase 5: Post-Merge Validation

```powershell
# Verify merge
git log --oneline -5

# Run tests again in main
dotnet test -c Release   # or appropriate command
```

---

### Phase 6: Push and Cleanup

```powershell
# Push main
git push origin main

# Delete feature branch (optional)
git branch -d feature/xyz

# Delete remote branch (optional)
git push origin --delete feature/xyz
```

---

### Phase 7: Report Summary

```
## ✅ Merge Complete

| Phase | Status |
|-------|--------|
| Quality gates | ✅ All passed |
| Rebase | ✅ Clean |
| Merge | ✅ Fast-forward |
| Push | ✅ origin/main updated |
| Cleanup | ✅ Branch deleted |

### Commits merged:
- abc1234 feat: add capability
- def5678 test: add tests

### Next steps:
- [ ] Consider creating a release with `@release`
- [ ] Update project tracking if needed
```

---

## Rollback Procedure

If something goes wrong after merge but before push:

```powershell
# Find commit before merge
git log --oneline -10

# Reset to previous state
git reset --hard <commit-before-merge>
```

If already pushed (DANGEROUS):
```powershell
git reset --hard <commit-before-merge>
git push origin main --force
```

---

## Checklist

**Before merge:**
- [ ] Working tree clean
- [ ] Build succeeds
- [ ] All tests pass
- [ ] Linting clean
- [ ] Conventional commit messages
- [ ] Rebased onto latest main

**After merge:**
- [ ] Tests pass in main
- [ ] Pushed to origin
- [ ] Feature branch cleaned up
