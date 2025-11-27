# Git Hooks

This directory contains Git hooks that ensure code quality before pushing.

## Installation

After cloning the repository, run:

```powershell
git config core.hooksPath .githooks
```

Or use the install script:

```powershell
.\install-hooks.ps1
```

## Available Hooks

### pre-push

Runs before every `git push` to validate:
- ✅ Solution builds successfully
- ✅ All unit tests pass (uses `.runsettings` to exclude integration tests)

**To bypass** (not recommended):
```powershell
git push --no-verify
```

## What Gets Tested

The pre-push hook uses `.runsettings` which excludes:
- Integration tests
- E2E tests
- Tests requiring API server
- Tests requiring database
- Tests requiring Ollama

This matches the CI/CD pipeline configuration for fast local validation.
