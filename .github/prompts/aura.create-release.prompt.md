---
agent: agent
description: Analyze changes, prepare documentation, validate quality, and execute an Aura release ceremony.
---

# Prepare and Execute an Aura Release

Analyze changes since the last release, prepare documentation, validate quality, and execute the release ceremony.

---

## Phase 1: Analyze Release Scope

1. **Get the current version** from `extension/package.json`:
   ```powershell
   (Get-Content extension/package.json | ConvertFrom-Json).version
   ```

2. **List changes since last release**:
   ```powershell
   $lastTag = git describe --tags --abbrev=0 2>$null
   if ($lastTag) {
       git log "$lastTag..HEAD" --oneline
   } else {
       git log --oneline -20
   }
   ```

3. **Categorize changes** and determine version bump:

   | Change Type | Version Bump | Examples |
   |-------------|--------------|----------|
   | Breaking API changes | **MAJOR** (X.0.0) | Removed MCP tools, changed agent format |
   | New features | **MINOR** (0.X.0) | New MCP tools, new indexing languages |
   | Bug fixes, docs, tests | **PATCH** (0.0.X) | Fixed edge cases, improved coverage |

4. **Report recommendation** to user:
   ```
   ## Release Analysis

   Current version: X.Y.Z
   Recommended bump: MINOR → X.(Y+1).0

   ### Changes included:
   - feat: ...
   - fix: ...
   - docs: ...

   ### Breaking changes: None / [list them]
   ```

5. **Ask for confirmation** before proceeding.

---

## Phase 2: Prepare Documentation

1. **Update CHANGELOG.md** (create if missing):
   ```markdown
   ## [X.Y.Z] - YYYY-MM-DD

   ### Added
   - New feature 1
   - New feature 2

   ### Changed
   - Changed behavior 1

   ### Fixed
   - Bug fix 1

   ### Deprecated
   - Deprecated feature 1 (if any)
   ```

2. **Validate README.md** is current:

   a. **Check feature coverage** — Ensure all major features are documented:
      - Compare README against current MCP tools in `src/Aura.Api/Mcp/McpHandler.cs`
      - New agents, tools, or indexing features should be mentioned

   b. **Verify installation instructions** match current setup:
      - Windows installer process
      - VS Code extension installation
      - Prerequisites (Ollama, etc.)

   c. **Check version references** — Ensure no hardcoded old versions

3. **Update docs/** if needed:
   - Check `docs/getting-started/` for accuracy
   - Verify `docs/user-guide/` reflects current features

---

## Phase 3: Quality Validation

1. **Run full test suite**:
   ```powershell
   dotnet test --configuration Release --verbosity minimal
   ```
   - All tests must pass
   - Note test count and any skipped tests

2. **Run linting and formatting**:
   ```powershell
   dotnet format Aura.sln --verify-no-changes
   ```
   - No formatting issues allowed

3. **Build the solution**:
   ```powershell
   dotnet build -c Release
   ```

4. **Build VS Code extension**:
   ```powershell
   .\scripts\Build-Extension.ps1
   ```

5. **Verify pre-commit hooks pass**:
   ```powershell
   git stash  # Temporarily stash any changes
   .\scripts\hooks\pre-commit
   git stash pop  # Restore changes
   ```

6. **Report quality status**:
   ```
   ## Quality Validation

   | Check | Status |
   |-------|--------|
   | Tests | ✅ XXX passed |
   | Formatting | ✅ Clean |
   | Build (Release) | ✅ Success |
   | Extension Build | ✅ Success |
   | Pre-commit | ✅ Passed |
   ```

---

## Phase 4: Execute Release

### Version Sources

The release uses version numbers from these sources:

| Artifact | Version Source |
|----------|----------------|
| **VS Code Extension (.vsix)** | `extension/package.json` → `version` field |
| **Windows Installer (.exe)** | Git tag (passed via `/DMyAppVersion=X.Y.Z` to ISCC) |
| **GitHub Release** | Git tag name |

**Only `extension/package.json` needs manual update.** The installer derives its version from the git tag automatically.

1. **Update version** in `extension/package.json`:
   ```powershell
   # Edit extension/package.json to update "version": "X.Y.Z"
   ```

2. **Rebuild the extension** (to update dist files):
   ```powershell
   .\scripts\Build-Extension.ps1
   ```

3. **Commit version bump** (if any changes):
   ```powershell
   git add extension/package.json extension/dist/
   git commit -m "chore(extension): bump version to X.Y.Z"
   git push origin main
   ```

4. **Create annotated tag** (version MUST match package.json):
   ```powershell
   git tag -a vX.Y.Z -m "Release vX.Y.Z

   Highlights:
   - Feature 1
   - Feature 2
   - Bug fix 1"
   ```

5. **Push tag to trigger release workflow**:
   ```powershell
   git push origin vX.Y.Z
   ```

---

## Phase 5: Monitor and Verify

1. **Watch the release workflow**:
   ```powershell
   gh run list --limit 5
   gh run watch  # Interactive watch
   ```

2. **If pipeline fails**:
   ```powershell
   # Get failure details
   gh run view <run-id> --log-failed

   # Fix the issue, then delete and recreate tag
   git tag -d vX.Y.Z
   git push origin --delete vX.Y.Z

   # After fix, re-tag and push
   git tag -a vX.Y.Z -m "Release vX.Y.Z"
   git push origin vX.Y.Z
   ```

3. **Verify GitHub Release**:
   - Check https://github.com/johnazariah/aura/releases
   - Confirm artifacts are present:
     - `Aura-Setup-X.Y.Z.exe`
     - `aura-X.Y.Z.vsix`
   - Verify release notes are accurate
   - Confirm pre-release flag is correct (if applicable)

4. **Test installation** (optional but recommended):
   ```powershell
   # Download and run installer on a clean machine or VM
   # Verify VS Code extension activates
   # Verify API server starts
   ```

5. **Report final status**:
   ```
   ## Release Complete ✅

   - **Version**: X.Y.Z
   - **Release**: https://github.com/johnazariah/aura/releases/tag/vX.Y.Z
   - **Installer**: Aura-Setup-X.Y.Z.exe
   - **Extension**: aura-X.Y.Z.vsix

   ### Post-release tasks:
   - [ ] Announce on social media
   - [ ] Update roadmap in .project/STATUS.md
   - [ ] Close related issues/PRs
   ```

---

## Release Artifacts

| Artifact | Description |
|----------|-------------|
| `Aura-Setup-{VERSION}.exe` | Windows installer with bundled PostgreSQL, VS Code extension, and all components |
| `aura-{VERSION}.vsix` | Standalone VS Code extension for manual installation |

---

## Pre-Release Versions

For pre-releases, append suffix to version:
- `-alpha` for early testing
- `-beta` for feature-complete testing  
- `-rc1`, `-rc2` for release candidates
- `-preview` for preview releases

Example:
```powershell
git tag -a v1.1.0-preview -m "Preview: New indexing features"
git push origin v1.1.0-preview
```

---

## Rollback Procedure

If a release has critical issues after publication:

1. **Remove the release** (or mark as pre-release):
   - Go to GitHub Releases page
   - Edit the release and either delete or mark as pre-release

2. **Create hotfix**:
   ```powershell
   git checkout -b hotfix/X.Y.(Z+1)
   # Fix the issue
   git commit -m "fix: critical issue in X.Y.Z"
   git checkout main
   git merge hotfix/X.Y.(Z+1)
   ```

3. **Release patch version** following the same ceremony.

---

## Automation Notes

This prompt works with GitHub Actions workflows:
- `.github/workflows/ci.yml` - Runs tests on every push
- `.github/workflows/release.yml` - Builds and publishes on tag push

The release workflow:
1. Runs tests
2. Builds Windows self-contained binaries
3. Creates Windows installer with Inno Setup
4. Packages VS Code extension
5. Creates GitHub Release with all artifacts
6. Auto-generates changelog from commits

---

## Checklist Summary

**Before tagging:**
- [ ] All tests pass
- [ ] Formatting is clean
- [ ] Solution builds in Release mode
- [ ] Extension builds successfully
- [ ] CHANGELOG.md updated (if maintained)
- [ ] Version bumped in extension/package.json
- [ ] Changes committed and pushed

**After tagging:**
- [ ] CI/CD pipeline succeeds
- [ ] GitHub Release created with artifacts
- [ ] Installer and VSIX available for download
- [ ] Release notes are accurate
