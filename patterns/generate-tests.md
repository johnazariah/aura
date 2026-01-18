# Pattern: Generate Tests

Generate comprehensive unit tests for a class or module, covering happy paths, edge cases, and error handling.

> **Language-specific guidance**: Use `aura_pattern(operation: "get", name: "generate-tests", language: "csharp")` to load with language overlay.

## When to Use

- User asks to "write tests for X"
- User asks to "increase test coverage for X"
- A new class/service has been created and needs tests

## Prerequisites

- Target class/module exists and builds
- Test project/directory exists with appropriate framework
- Project path is known

## Universal Workflow

### 1. Analyze Test Surface

Understand what needs testing:
- List public methods/functions
- Identify existing tests (if any)
- Categorize gaps: no tests, null handling, error handling, edge cases

### 2. Present Analysis to User

Show:
- Number of testable members
- Current coverage status
- Gap categories identified

Ask user preference:
- Comprehensive coverage (default)
- Specific focus (happy path, edge cases, error handling)

### 3. Generate Tests

Use language-appropriate tooling:
- **C#**: `aura_generate(operation: "tests")` or `aura_generate(operation: "method")`
- **Python**: pytest fixtures + test functions
- **TypeScript**: Jest/Vitest test suites
- **Other**: Framework-appropriate test structure

### 4. Fix Generated Tests

All generators produce imperfect output. Common fixes:
- Import/using statements
- Mock/stub infrastructure
- Placeholder assertions → real assertions
- Test data/fixtures

### 5. Validate

Build and run tests to verify:
- Tests compile/parse
- Tests pass (or fail as expected for TDD)
- Coverage improved

### 6. Iterate

If user wants more tests or different focus, repeat from step 3.

## Test Structure (Universal)

All tests follow Arrange/Act/Assert:

```
Arrange: Set up test fixtures, mocks, inputs
Act:     Execute the code under test  
Assert:  Verify the expected outcome
```

## Test Gap Categories

| Gap | Description | Example Test |
|-----|-------------|--------------|
| NoTests | No tests exist | Basic happy path |
| NullHandling | Nullable params not tested | Pass null, expect error |
| ErrorHandling | Exception paths not tested | Trigger error condition |
| BoundaryValues | Edge cases not tested | Empty list, max value |
| AsyncBehavior | Async not tested | Cancellation, timeout |

## Anti-patterns (Universal)

❌ **Never skip analysis** - understand what exists first
❌ **Never leave placeholder assertions** - tests must verify behavior
❌ **Never skip validation** - generated tests may have errors
❌ **Never assume mocks are correct** - review and fix infrastructure

## Language Overlays

For language-specific tooling, imports, and frameworks:

- `csharp` - Roslyn tools, xUnit/NUnit, FluentAssertions, MockFileSystem
- `python` - pytest, unittest.mock, fixtures
- `typescript` - Jest/Vitest, ts-jest

Load with: `aura_pattern(operation: "get", name: "generate-tests", language: "...")`
