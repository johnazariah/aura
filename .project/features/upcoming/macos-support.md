# macOS Support

**Status:** 🔜 Planned
**Priority:** Medium
**Effort:** Large (1-2 weeks)

## Summary

Enable Aura to run natively on macOS with full functionality, including TreeSitter parsing, Docker-based integration tests, and a native installer.

## Current State

- ✅ Code compiles on macOS
- ✅ .NET 10 runtime available on macOS
- ❌ TreeSitter native library not bundled for macOS (arm64/x64)
- ❌ Integration tests fail (Docker not available on GitHub macOS runners)
- ❌ No macOS installer/package
- ❌ PostgreSQL bundling not implemented for macOS

## Requirements

### 1. TreeSitter Native Library

**Problem**: The TreeSitter .NET binding requires platform-specific native libraries. Currently only Windows binaries are bundled.

**Solution**:

- Build or source `libtree-sitter.dylib` for macOS (both arm64 and x64)
- Update the NuGet package or add runtime-specific assets
- Test on both Intel and Apple Silicon Macs

**Files affected**:

- `src/Aura.Module.Developer/Aura.Module.Developer.csproj` (native library references)
- Possibly need to create a separate NuGet package for TreeSitter bindings

### 2. Integration Tests in CI

**Problem**: GitHub's macOS runners don't have Docker pre-installed.

**Options**:

1. Use `colima` or `lima` to run Docker on macOS runners
2. Use self-hosted macOS runners with Docker Desktop
3. Accept that integration tests only run on Linux/Windows

**Recommendation**: Option 3 for now, with clear documentation that integration tests require Docker.

### 3. macOS Installer

**Options**:

1. **Homebrew Cask** - Most idiomatic for macOS users
2. **DMG installer** - Traditional drag-and-drop installation
3. **PKG installer** - System-level installation with scripts

**Recommendation**: Start with Homebrew Cask for developer audience.

**Components to install**:

- Aura API service (as a launchd daemon)
- Aura Tray app (menu bar app)
- VS Code extension (via `code --install-extension`)
- PostgreSQL (or require user to install via Homebrew)

### 4. PostgreSQL on macOS

**Options**:

1. Require user to install via `brew install postgresql@16`
2. Bundle Postgres.app style self-contained PostgreSQL
3. Use the official PostgreSQL installer

**Recommendation**: Option 1 (Homebrew) for simplicity, with clear setup docs.

### 5. Container Runtime

**Problem**: Aspire uses containers for local development.

**Solution**:

- Support OrbStack (already documented in copilot-instructions.md)
- Support Docker Desktop
- Support Colima as lightweight alternative

## Implementation Plan

### Phase 1: TreeSitter (3-5 days)

1. Research TreeSitter.NET macOS support
2. Build or obtain macOS native libraries
3. Update project to include macOS runtime assets
4. Add CI step to test TreeSitter on macOS

### Phase 2: Installer (3-5 days)

1. Create Homebrew formula/cask
2. Build launchd plist for API service
3. Create menu bar app bundle for Aura.Tray
4. Write installation/setup documentation

### Phase 3: CI & Testing (2-3 days)

1. Re-enable macOS in CI matrix
2. Skip integration tests on macOS (or find Docker solution)
3. Ensure all unit tests pass
4. Add macOS-specific test for TreeSitter

### Phase 4: Documentation (1 day)

1. Update installation docs for macOS
2. Add troubleshooting section for common macOS issues
3. Update README with macOS badge

## Acceptance Criteria

- [ ] TreeSitter parsing works on macOS (both arm64 and x64)
- [ ] Unit tests pass on macOS in CI
- [ ] Homebrew cask installs Aura successfully
- [ ] Aura API starts as launchd service
- [ ] Aura Tray appears in menu bar
- [ ] VS Code extension connects to local API
- [ ] Documentation covers macOS setup

## Dependencies

- TreeSitter native library for macOS
- Homebrew tap setup
- Apple Developer certificate (for code signing, optional but recommended)

## Related

- [install-mac.sh](../../../setup/install-mac.sh) - Existing placeholder script
- [Bundled Extension](../completed/bundled-extension.md) - Windows extension bundling pattern
- [PostgreSQL Setup](../completed/postgresql-setup.md) - Windows PostgreSQL bundling pattern
