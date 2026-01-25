# macOS Local Development Support

**Status:** ✅ Complete
**Completed:** 2026-01-19
**Priority:** Medium
**Effort:** Small (1 day)

## Summary

Enable Aura to build and run locally on macOS with full TreeSitter parsing support for both Intel and Apple Silicon Macs.

## What Was Done

### 1. TreeSitter Native Library ✅

**Problem**: The TreeSitter.DotNet 1.1.1 package only included Windows and Linux native libraries.

**Solution**: Upgraded to TreeSitter.DotNet 1.2.0 which includes:
- `osx-x64` (Intel Mac)
- `osx-arm64` (Apple Silicon M1/M2/M3/M4)

**Files changed**:
- `src/Aura.Module.Developer/Aura.Module.Developer.csproj` - Updated package version

### 2. Installation Script ✅

Enhanced `setup/install-mac.sh` with:
- Better error handling and messaging
- Consistent formatting matching Windows installer
- Full prerequisite installation (OrbStack, PostgreSQL, Ollama, .NET SDK)
- Database setup with pgvector extension
- LLM model pulling

### 3. Documentation ✅

Updated `docs/getting-started/installation.md` with:
- macOS prerequisites section
- Quick start using the install script
- Manual installation steps
- Verification instructions

## What's NOT Included (See: macos-ci-and-distribution.md)

- macOS CI builds (deferred until self-hosted runners available)
- Homebrew cask for distribution
- macOS menu bar app (Tray equivalent)
- launchd service setup

## Acceptance Criteria

- [x] TreeSitter parsing works on macOS (both arm64 and x64)
- [x] Code compiles on macOS
- [x] Installation script sets up all prerequisites
- [x] Documentation covers macOS local setup

## Related

- [macos-ci-and-distribution.md](../upcoming/macos-ci-and-distribution.md) - Remaining macOS work
- [install-mac.sh](../../../setup/install-mac.sh) - Installation script
