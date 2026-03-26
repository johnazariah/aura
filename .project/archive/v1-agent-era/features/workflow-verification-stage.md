# Workflow Verification Stage

**Status:** ✅ Complete  
**Completed:** 2026-01-19  
**Priority:** Medium  
**Created:** 2026-01-12

## Problem

Workflows currently skip git pre-commit hooks (`--no-verify`) because:

1. Pre-commit hooks can timeout waiting for user input
2. Pre-commit hooks may require tools not available in the workflow context
3. The 30-second process timeout causes commits to fail

However, this means **no verification** happens before commits in automated workflows.

## Solution

Add an explicit **Verification Stage** to the workflow pipeline that runs the same checks as pre-commit hooks, but in a controlled, non-blocking way.

### Proposed Design

1. **Detect project verification tools** during workflow creation:
   - Parse `.pre-commit-config.yaml` if present
   - Parse `package.json` scripts (lint, format, test)
   - Parse `.csproj` for analyzers
   - Check for `Makefile`, `justfile`, etc.

2. **Add verification tools** to step tool list:
   - `dotnet.format` - Run `dotnet format --verify-no-changes`
   - `dotnet.build` - Verify compilation
   - `eslint.check` - Run ESLint
   - `prettier.check` - Run Prettier check mode
   - `python.lint` - Run ruff/flake8/pylint

3. **Create implicit verification step** before commit steps:
   - Auto-insert "Verify changes" step before any step that commits
   - Or add verification as part of the commit step instructions

4. **Surface verification failures** clearly:
   - Show which check failed
   - Provide actionable fix suggestions
   - Allow retry after fixing

## Acceptance Criteria

- [ ] Workflows detect and run appropriate verification for the project type
- [ ] Verification failures are reported with clear error messages
- [ ] Agent can fix verification failures and retry
- [ ] No reliance on pre-commit hooks (which can block)
- [ ] Works for: C#, TypeScript, Python, Rust, Go

## Technical Notes

- The `roslyn.validate_compilation` tool already exists for C#
- Need to add format/lint tools for other languages
- Consider making verification configurable per-project via `.aura/config.yaml`

## Related

- Pre-commit hook bypass added in `GitService.CommitAsync` with `skipHooks` parameter
- Current workaround: `git commit --no-verify`
