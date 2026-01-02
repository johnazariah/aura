# GitHub Release Automation

**Status:** ✅ Complete  
**Completed:** 2025-12-25  
**Effort:** 4-6 hours  
**Priority:** P1 - Near Term

## Summary

Automate the release process via GitHub Actions to create consistent, reproducible releases with minimal manual intervention.

## Current State

- `scripts/Publish-Release.ps1` creates Windows binaries
- `scripts/Build-Installer.ps1` creates Windows installer via Inno Setup
- `scripts/Build-Extension.ps1` creates VS Code `.vsix`
- Manual process to upload to GitHub Releases

## Goals

1. Single-click (or tag-triggered) release workflow
2. Automated versioning from git tags
3. Build artifacts for Windows (installer + VSIX)
4. Create GitHub Release with changelog

## Implementation

### GitHub Actions Workflow

**File:** `.github/workflows/release.yml`

```yaml
name: Release

on:
  push:
    tags:
      - 'v*'

jobs:
  build-windows:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      
      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: '20'
      
      - name: Extract version from tag
        id: version
        run: echo "VERSION=${GITHUB_REF#refs/tags/v}" >> $GITHUB_OUTPUT
        shell: bash
      
      - name: Publish Release
        run: .\scripts\Publish-Release.ps1 -Version ${{ steps.version.outputs.VERSION }}
      
      - name: Build VS Code Extension
        run: .\scripts\Build-Extension.ps1
      
      - name: Install Inno Setup
        run: choco install innosetup -y
      
      - name: Build Installer
        run: .\scripts\Build-Installer.ps1 -Version ${{ steps.version.outputs.VERSION }}
      
      - name: Upload Artifacts
        uses: actions/upload-artifact@v4
        with:
          name: windows-release
          path: |
            publish/installers/Aura-Setup-*.exe
            extension/aura-*.vsix

  create-release:
    needs: build-windows
    runs-on: ubuntu-latest
    permissions:
      contents: write
    steps:
      - uses: actions/checkout@v4
      
      - name: Download Artifacts
        uses: actions/download-artifact@v4
        with:
          name: windows-release
          path: artifacts
      
      - name: Generate Changelog
        id: changelog
        run: |
          # Extract changelog from git commits since last tag
          PREV_TAG=$(git describe --abbrev=0 --tags HEAD^ 2>/dev/null || echo "")
          if [ -n "$PREV_TAG" ]; then
            CHANGELOG=$(git log --oneline $PREV_TAG..HEAD)
          else
            CHANGELOG=$(git log --oneline -20)
          fi
          echo "CHANGELOG<<EOF" >> $GITHUB_OUTPUT
          echo "$CHANGELOG" >> $GITHUB_OUTPUT
          echo "EOF" >> $GITHUB_OUTPUT
      
      - name: Create GitHub Release
        uses: softprops/action-gh-release@v1
        with:
          files: |
            artifacts/Aura-Setup-*.exe
            artifacts/aura-*.vsix
          body: |
            ## What's New
            
            ${{ steps.changelog.outputs.CHANGELOG }}
            
            ## Installation
            
            1. Download `Aura-Setup-*.exe` and run the installer
            2. The VS Code extension is bundled and will be installed automatically
            3. Ensure Ollama is installed: https://ollama.com
            
            ## Requirements
            
            - Windows 10/11 (x64)
            - Ollama (for local LLM)
            - PostgreSQL 16+ (installed automatically or use Docker)
          draft: false
          prerelease: ${{ contains(github.ref, 'preview') || contains(github.ref, 'beta') }}
```

### Release Process

1. **Developer creates tag:**

   ```bash
   git tag -a v0.1.0 -m "Release 0.1.0"
   git push origin v0.1.0
   ```

2. **GitHub Actions automatically:**
   - Builds Windows binaries
   - Creates installer with VSIX bundled
   - Creates GitHub Release with artifacts
   - Generates changelog from commits

3. **For preview releases:**

   ```bash
   git tag -a v0.1.0-preview -m "Preview release"
   git push origin v0.1.0-preview
   ```

   This marks the release as "pre-release" on GitHub.

## Tasks

- [ ] Create `.github/workflows/release.yml`
- [ ] Update `Build-Installer.ps1` to bundle VSIX (see bundled-extension.md)
- [ ] Test with a `v0.0.1-test` tag
- [ ] Document release process in CONTRIBUTING.md

## Dependencies

- Bundled Extension spec (for VSIX in installer)
- PostgreSQL Setup spec (for installer requirements)
