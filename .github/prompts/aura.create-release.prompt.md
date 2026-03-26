---
agent: agent
description: Analyze changes, prepare documentation, validate quality, and execute an Aura release ceremony.
---

# Prepare and Execute an Aura Release

Analyze changes since the last release, prepare documentation, validate quality, and execute the release ceremony.

---

## Phase 1: Analyze Release Scope

1. **Get the current version** from `CHANGELOG.md` or the latest git tag:
   ```powershell
   $lastTag = git describe --tags --abbrev=0 2>$null
   if ($lastTag) { Write-Host "Last release: $lastTag" }
   ```

2. **List changes since last release**:
   ```powershell
   if ($lastTag) {
       git log "$lastTag..HEAD" --oneline
   } else {
       git log --oneline -20
   }
   ```

3. **Categorize changes** and determine version bump:

   | Change Type | Version Bump | Examples |
   |-------------|--------------|----------|
   | Breaking API changes | **MAJOR** (X.0.0) | Removed MCP tools, changed data model |
   | New features | **MINOR** (0.X.0) | New MCP tools, new indexing languages |
   | Bug fixes, docs, tests | **PATCH** (0.0.X) | Fixed edge cases, improved coverage |

4. **Report recommendation** to user and ask for confirmation.

---

## Phase 2: Prepare Documentation

1. **Update CHANGELOG.md** with the new version entry (Added, Changed, Fixed, Deprecated sections).

2. **Validate README.md** is current:
   - Compare against current MCP tools in `src/Aura.Api/Mcp/McpHandler.cs`
   - Verify installation instructions match current setup
   - Check for hardcoded old versions

---

## Phase 3: Quality Validation

1. **Run full test suite**:
   ```powershell
   dotnet test --configuration Release --verbosity minimal
   ```

2. **Run linting and formatting**:
   ```powershell
   dotnet format Aura.sln --verify-no-changes
   ```

3. **Build the solution**:
   ```powershell
   dotnet build -c Release
   ```

4. **Report quality status**:
   ```
   ## Quality Validation

   | Check | Status |
   |-------|--------|
   | Tests | ✅ XXX passed |
   | Formatting | ✅ Clean |
   | Build (Release) | ✅ Success |
   ```

---

## Phase 4: Execute Release

### Publish Artifacts

Use `Publish-Release.ps1` to build self-contained binaries:

```powershell
# Windows only
.\scripts\Publish-Release.ps1 -Version "X.Y.Z" -Runtime win-x64

# macOS only
.\scripts\Publish-Release.ps1 -Version "X.Y.Z" -Runtime osx-arm64

# Both platforms
.\scripts\Publish-Release.ps1 -Version "X.Y.Z" -Runtime all
```

Parameters:
- `-Version` — Release version (default `"1.0.0"`)
- `-Runtime` — `win-x64`, `osx-arm64`, or `all`
- `-OutputDir` — Output directory (default `publish`)
- `-SkipPostgres` — Skip bundling PostgreSQL for Windows

The script:
- Publishes `Aura.Api` + `Aura.Tray` as self-contained binaries
- Copies `patterns/`, TypeScript dist, Python tooling
- Writes `version.json` with version, runtime, build date, git SHA
- For `win-x64`: downloads PostgreSQL 16.4 + pgvector (unless `-SkipPostgres`)

### Tag and Push

1. **Commit version bump** (if any changes):
   ```powershell
   git add CHANGELOG.md
   git commit -m "chore: bump version to X.Y.Z"
   git push origin main
   ```

2. **Create annotated tag**:
   ```powershell
   git tag -a vX.Y.Z -m "Release vX.Y.Z

   Highlights:
   - Feature 1
   - Feature 2"
   ```

3. **Push tag to trigger release workflow**:
   ```powershell
   git push origin vX.Y.Z
   ```

---

## Phase 5: Monitor and Verify

1. **Watch the release workflow**:
   ```powershell
   gh run list --limit 5
   gh run watch
   ```

2. **If pipeline fails**:
   ```powershell
   gh run view <run-id> --log-failed

   # Fix, delete and recreate tag
   git tag -d vX.Y.Z
   git push origin --delete vX.Y.Z
   git tag -a vX.Y.Z -m "Release vX.Y.Z"
   git push origin vX.Y.Z
   ```

3. **Verify GitHub Release**:
   - Confirm artifacts are uploaded
   - Verify release notes are accurate

---

## Release Artifacts

| Artifact | Description |
|----------|-------------|
| `publish/win-x64/` | Windows self-contained API + Tray + bundled PostgreSQL |
| `publish/osx-arm64/` | macOS self-contained API + Tray |
| `version.json` | Build metadata (version, runtime, date, commit SHA) |

---

## Pre-Release Versions

Append suffix: `-alpha`, `-beta`, `-rc1`, `-preview`

```powershell
git tag -a v1.1.0-preview -m "Preview: New indexing features"
git push origin v1.1.0-preview
```

---

## Rollback Procedure

1. Remove or mark release as pre-release on GitHub
2. Create hotfix branch, fix, merge to main
3. Release patch version following the same ceremony

---

## Automation Notes

- `.github/workflows/ci.yml` — Tests on every push
- `.github/workflows/release.yml` — Builds and publishes on tag push

---

## Checklist Summary

**Before tagging:**
- [ ] All tests pass
- [ ] Formatting is clean
- [ ] Solution builds in Release mode
- [ ] CHANGELOG.md updated
- [ ] Changes committed and pushed

**After tagging:**
- [ ] CI/CD pipeline succeeds
- [ ] GitHub Release created with artifacts
- [ ] Release notes are accurate
