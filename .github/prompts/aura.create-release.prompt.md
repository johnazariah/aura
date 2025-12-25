---
mode: agent
description: Create a new release for the Aura product
tools: ['run_in_terminal', 'read_file', 'replace_string_in_file']
---

# Create a New Aura Release

Create and publish a new release of Aura with the Windows installer and VS Code extension.

## Prerequisites Check

Before creating a release, verify:

1. All tests pass: `dotnet test`
2. No uncommitted changes: `git status`
3. You're on the main branch: `git branch --show-current`
4. Main is up to date: `git pull origin main`

## Release Process

### Step 1: Determine Version

Ask the user for the version number. Follow semantic versioning:
- **Major** (X.0.0): Breaking changes
- **Minor** (0.X.0): New features, backward compatible
- **Patch** (0.0.X): Bug fixes only

Version format: `X.Y.Z` (e.g., `1.0.0`, `1.1.0`, `1.0.1`)

For pre-releases, append suffix:
- `-alpha` for early testing
- `-beta` for feature-complete testing
- `-rc1`, `-rc2` for release candidates
- `-preview` for preview releases

### Step 2: Update Changelog (Optional)

If there's a CHANGELOG.md, update it with the new version and changes.

### Step 3: Create and Push Tag

```powershell
# Create annotated tag
git tag -a v{VERSION} -m "Release {VERSION}"

# Push tag to trigger release workflow
git push origin v{VERSION}
```

### Step 4: Monitor Release

The GitHub Actions workflow will automatically:
1. Run tests
2. Build Windows installer with bundled PostgreSQL
3. Build and package VS Code extension
4. Create GitHub Release with artifacts
5. Generate changelog from commits

Monitor at: https://github.com/johnazariah/aura/actions

### Step 5: Verify Release

Once complete, verify:
1. GitHub Release page has correct artifacts:
   - `Aura-Setup-{VERSION}.exe`
   - `aura-{VERSION}.vsix`
2. Release notes are accurate
3. Pre-release flag is correct (if applicable)

## Release Artifacts

| Artifact | Description |
|----------|-------------|
| `Aura-Setup-{VERSION}.exe` | Windows installer with bundled PostgreSQL, VS Code extension, and all components |
| `aura-{VERSION}.vsix` | Standalone VS Code extension for manual installation |

## Rollback

If something goes wrong:

```powershell
# Delete remote tag
git push origin --delete v{VERSION}

# Delete local tag
git tag -d v{VERSION}
```

Then fix the issue and try again.

## User Instructions

Provide these instructions in the release notes:

1. Download `Aura-Setup-{VERSION}.exe` and run the installer
2. The VS Code extension will be installed automatically if VS Code is detected
3. Ensure Ollama is installed: https://ollama.com
4. Open VS Code and look for the Aura icon in the sidebar

## Example

To create release 1.0.0:

```powershell
git tag -a v1.0.0 -m "Release 1.0.0 - Developer Module MVP"
git push origin v1.0.0
```

To create a preview release:

```powershell
git tag -a v1.1.0-preview -m "Preview: New indexing features"
git push origin v1.1.0-preview
```
