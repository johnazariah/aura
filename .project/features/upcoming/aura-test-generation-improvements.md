# Aura Test Generation Tool Improvements

**Status:** 🔴 Not Started
**Priority:** High
**Created:** 2026-01-18
**Source:** Dogfooding session - Story 9c43eea5-a229-4869-a60d-d8107fabb542

## Overview

During the "Increase Test Coverage Using Aura Test Generation" workflow, significant deficiencies were discovered in the Aura MCP tools, particularly around test generation, workspace caching, and Roslyn integration. This document catalogs all issues found and proposes improvements.

## Context

The task was to generate unit tests for `WorkflowService` (25 public methods, 0 existing tests). The generate-tests pattern was followed, but numerous tool failures required manual workarounds.

### Final Outcome
- **32 tests** successfully created for `WorkflowService`
- **124 total tests** in `Aura.Module.Developer.Tests` (all passing)
- Significant manual intervention required due to tool deficiencies

---

## Critical Issues (High Priority)

### 1. `aura_generate tests` - Wrong Test Project Location

**Symptom:** Tests are placed in `Aura.Foundation.Tests/Agents/` instead of `Aura.Module.Developer.Tests/Services/`.

**Impact:** Generated tests don't compile because the test project lacks the necessary dependencies.

**Root Cause:** Tool doesn't analyze which project contains the target class and map to the corresponding test project.

**Suggested Fix:**
1. Resolve the target class's containing project
2. Find the corresponding test project (convention: `{ProjectName}.Tests`)
3. Place test file in appropriate namespace-matching folder

---

### 2. `aura_generate tests` - Unusable Skeleton Tests

**Symptom:** Generated tests contain:
```csharp
var sut = new WorkflowService(); // TODO: Add constructor parameters/dependencies
var result = await sut.Method(default /* param */);
// TODO: Add assertions
```

**Impact:** Tests don't compile and require complete rewrite.

**Root Cause:** Tool doesn't analyze constructor dependencies or generate mock setup.

**Suggested Fix:**
1. Use Roslyn to analyze constructor parameters
2. Generate NSubstitute/Moq setup for interface dependencies
3. Create proper test fixtures with Arrange/Act/Assert pattern
4. Use actual parameter values, not `default`

---

### 3. `aura_generate tests` - Incorrect Type References

**Symptom:** Generated code uses non-existent enum values like `StepStatus.Approved` instead of `StepApproval.Approved`.

**Impact:** Compilation errors.

**Root Cause:** Tool doesn't properly resolve types and their members from Roslyn.

**Suggested Fix:** Use semantic model to resolve actual enum values and property types.

---

### 4. `aura_generate tests` - Overwrites Instead of Appending

**Symptom:** Each call to generate tests for a different method replaces the entire file content.

**Impact:** Previously generated tests are lost.

**Suggested Fix:** Append new test methods to existing test class, or merge intelligently.

---

### 5. Workspace Cache Staleness

**Symptom:** 
- `aura_inspect list_types` doesn't show newly created files
- `aura_generate method` can't find classes until cache invalidated
- `roslynWorkspaceCached: false` in worktrees

**Impact:** Tools fail silently or produce wrong results.

**Root Cause:** Roslyn workspace not automatically refreshed when files change.

**Suggested Fix:**
1. Auto-invalidate cache when files are modified by Aura tools
2. Or: Refresh workspace on each tool invocation (performance tradeoff)
3. Document that `invalidate_cache` is required after file changes

---

### 6. `aura_generate method` - Missing Test Attributes

**Symptom:** Methods added to test classes lack `[Fact]` attribute.

**Impact:** xUnit analyzer error, tests don't run.

**Suggested Fix:** Detect test class context and add appropriate test framework attributes (`[Fact]`, `[Test]`, etc.).

---

### 7. `aura_inspect type_members` - Returns Empty

**Symptom:** Returns `[]` for valid types like `WorkflowService` and `WorkflowServiceTests`.

**Impact:** Can't use tool to understand class structure.

**Root Cause:** Unknown - possibly related to workspace caching or type resolution.

---

## Medium Priority Issues

### 8. `aura_navigate definition` - Key Not Present Error

**Symptom:** `ERROR: The given key was not present in the dictionary`

**Workaround:** Use `aura_navigate usages` instead.

---

### 9. `aura_validate compilation` - Requires Project Name

**Symptom:** Solution-level validation fails with "key not present" error.

**Workaround:** Always specify `projectName` parameter.

---

### 10. `aura_search` - Irrelevant Results

**Symptom:** Searching for `IGitWorktreeService CreateAsync WorktreeResult` returns unrelated markdown files.

**Impact:** Can't use semantic search to find code patterns.

---

### 11. `aura_validate tests` - Truncated Output

**Symptom:** Returns `total: 0` with truncated build output, even when tests exist and pass.

**Impact:** Can't verify test results through the tool.

---

### 12. `aura_generate method` - Formatting Issues

**Symptom:** Generated method body has inconsistent indentation, closing brace on same line as code.

**Impact:** Code style violations, harder to read.

---

## Proposed Improvements

### Short-term (Bug Fixes)

1. **Fix test project resolution** - Map source project to test project correctly
2. **Add `[Fact]` attribute** - Detect test class context
3. **Auto-invalidate cache** - After any `aura_generate` operation
4. **Fix `type_members`** - Investigate and fix empty results

### Medium-term (Enhancements)

1. **Smart test generation** - Analyze constructor, generate mock setup
2. **Append mode for tests** - Don't overwrite existing test methods
3. **Improve search relevance** - Weight code symbols higher than docs

### Long-term (Architecture)

1. **Incremental workspace updates** - Instead of full cache invalidation
2. **Test coverage analysis** - Know which methods are already tested
3. **Test template system** - Customizable test patterns per project

---

## Reproduction Steps

```bash
# 1. Create worktree for a story
# 2. Try to generate tests for WorkflowService

aura_generate(
  operation: "tests",
  target: "WorkflowService",
  solutionPath: "path/to/Aura.sln"
)

# Observe: Tests placed in wrong project with skeleton code
```

---

## Related Files

- [generate-tests.md](../../../patterns/generate-tests.md) - The pattern being followed
- [WorkflowServiceTests.cs](../../../tests/Aura.Module.Developer.Tests/Services/WorkflowServiceTests.cs) - Manually corrected tests

---

## Appendix: All Deficiencies Found

| # | Tool | Issue | Severity |
|---|------|-------|----------|
| 1 | `aura_generate tests` | Wrong project location | High |
| 2 | `aura_generate tests` | Skeleton tests with TODO | High |
| 3 | `aura_generate tests` | Wrong enum values | High |
| 4 | `aura_inspect type_members` | Returns empty | High |
| 5 | `aura_navigate definition` | Key not present error | Medium |
| 6 | `aura_validate compilation` | Fails without projectName | Medium |
| 7 | `aura_search` | Irrelevant results | Medium |
| 8 | `aura_search` | Can't find StepStatus enum | Medium |
| 9 | `aura_generate tests` | Keeps writing to wrong project | High |
| 10 | `aura_generate tests` | Doesn't detect existing test file | High |
| 11 | `aura_generate tests` | Doesn't scan all test folders | High |
| 12 | `aura_validate tests` | Returns 0 tests, truncated | Medium |
| 13 | `aura_generate tests` | Overwrites instead of appending | High |
| 14 | `aura_generate tests` | Skeleton pattern unusable | High |
| 15 | `aura_validate tests` | Shows 0 even when tests exist | Medium |
| 16 | `aura_generate method` | Can't find class without cache invalidation | High |
| 17 | `aura_generate method` | Full namespace doesn't help | High |
| 18 | `aura_workspace status` | Cache not auto-refreshed | Medium |
| 19 | `aura_inspect list_types` | Missing new files | High |
| 20 | General | Cache staleness | High |
| 21 | `aura_inspect type_members` | Empty after cache invalidation | High |
| 22 | `aura_generate method` | Missing `[Fact]` attribute | High |
| 23 | `aura_generate method` | Mangled formatting | Medium |
