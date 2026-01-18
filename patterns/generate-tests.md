# Pattern: Generate Tests

Generate comprehensive unit tests for a class or module, covering happy paths, edge cases, and error handling.

## ⚠️ Aura Test Generation: What It Does vs. What You Must Do

### What `aura_generate tests` Provides:

| ✅ Aura Does | Description |
|--------------|-------------|
| Analyzes testable methods | Finds public methods, identifies gaps |
| Creates test file | Correct namespace, test class structure |
| Sets up skeleton | Arrange/Act/Assert pattern with mocks |
| Detects test framework | xUnit, NUnit, MSTest auto-detection |

### What Aura Gets Wrong (Requires Agent Domain Knowledge):

| ❌ Aura Limitation | Agent Fix Required |
|-------------------|-------------------|
| Wrong folder placement (defaults to `Agents/`) | Move file or use `aura_generate method` individually |
| Unqualified imports (`IFileSystem` not `System.IO.Abstractions.IFileSystem`) | Add full `using` statements |
| Placeholder assertions (`// TODO: Add meaningful assertions`) | Replace with actual assertions that match behavior |
| Tests fail because assertions don't match actual behavior | Agent must understand what the method actually returns |
| Uses `Substitute.For<ILogger<T>>()` | Replace with `NullLogger<T>.Instance` |
| No `IDisposable` cleanup pattern | Add `Dispose()` method if test class holds resources |
| No test data generation | Create YAML/JSON fixtures for config loaders, etc. |

### Domain Knowledge Only the Agent Has:

- `IFileSystem` requires `System.IO.Abstractions.TestingHelpers.MockFileSystem`
- Loggers should use `Microsoft.Extensions.Logging.Abstractions.NullLogger<T>.Instance`
- Meaningful assertions require understanding what the method **actually does**
- Realistic test data (YAML configs, JSON payloads) for the specific domain
- Proper test fixtures with `IDisposable` for cleanup

## Two Approaches

### Approach A: Bulk Generation + Fix (Faster for Many Tests)

1. `aura_generate(operation: "tests", analyzeOnly: true)` - Understand gaps
2. `aura_generate(operation: "tests")` - Generate skeletons
3. Fix imports with `replace_string_in_file`
4. Fix infrastructure (loggers, mocks)
5. Replace placeholder assertions
6. Build and test

### Approach B: Individual Method Generation (More Control)

Use `aura_generate(operation: "method")` with `body` parameter for each test:

```
aura_generate(
  operation: "method",
  className: "MyServiceTests",
  methodName: "MyMethod_WhenCondition_ShouldResult",
  returnType: "void",
  body: "// Arrange\nvar sut = new MyService();\n\n// Act\nvar result = sut.MyMethod();\n\n// Assert\nresult.Should().BeTrue();",
  solutionPath: "..."
)
```

**Benefits:** Full control over test logic, correct assertions from the start, Aura handles file manipulation.

**Use when:** You need specific test logic, fixtures, or complex mocking.

## When to Use

- User asks to "write tests for X"
- User asks to "increase test coverage for X"
- A new class/service has been created and needs tests

## Prerequisites

- Target class/module exists and builds
- Test project exists with appropriate framework
- Solution/project path is known

## Execution Protocol

**For each step below:**
1. **Announce** which step you're executing
2. **Explain** why this step is needed
3. **Execute** the operation
4. **Report** the result before proceeding

Do not batch operations or skip explanations.

## Steps

### 1. Analyze Test Surface

```
aura_generate(
  operation: "tests",
  target: "{ClassName}",
  solutionPath: "{path}",
  analyzeOnly: true
)
```

This returns:
- Testable members (public methods, properties)
- Existing tests (if any)
- Detected test framework
- Suggested test count
- Identified gaps (no tests, null handling, error handling, etc.)

### 2. Present Analysis to User

Show:
- Number of testable members
- Current coverage status
- Recommended test count
- Gap categories identified

Ask if user wants:
- Comprehensive coverage (default)
- Specific count
- Focus on specific gap types

### 3. Generate Tests

```
aura_generate(
  operation: "tests",
  target: "{ClassName}",
  solutionPath: "{path}",
  count: {optional},
  maxTests: 20,
  focus: "All",  // or "HappyPath", "EdgeCases", "ErrorHandling"
  analyzeOnly: false
)
```

### 4. Review Generated Tests

The tool creates or modifies test files with:
- Proper test class structure
- Arrange/Act/Assert pattern
- `// TODO:` comments for setup that needs attention

### 5. Build and Verify

```bash
dotnet build
dotnet test --filter "ClassName"
```

### 6. Iterate if Needed

If user wants more tests or different focus:
- Re-run with different `focus` parameter
- Or specify explicit `count`

## Anti-patterns

❌ **Never generate tests without analyzing first** - you need to know what exists

❌ **Never generate more than `maxTests` (default 20)** - overwhelming

❌ **Never skip the build step** - generated tests may have syntax issues

❌ **Never assume mock setup is correct** - review `// TODO:` comments

## Test Gap Categories

| Gap | Description | Example Test |
|-----|-------------|--------------|
| `NoTests` | Method has no tests | Basic happy path |
| `NullHandling` | Nullable params not tested | Pass null, expect exception |
| `ErrorHandling` | Exception paths not tested | Trigger error condition |
| `BoundaryValues` | Edge cases not tested | Empty list, max int |
| `AsyncBehavior` | Async methods not tested | Await properly, test cancellation |

## Framework-Specific Templates

### xUnit (C#)
```csharp
public class {ClassName}Tests
{
    [Fact]
    public void {MethodName}_WhenCondition_ShouldResult()
    {
        // Arrange
        // Act
        // Assert
    }
}
```

### pytest (Python)
```python
class TestClassName:
    def test_method_name_when_condition_should_result(self):
        # Arrange
        # Act
        # Assert
```

## Example

**User**: "Write tests for the WorkflowService class"

**Assistant**:
1. Calls `aura_generate(operation: "tests", analyzeOnly: true)`
2. Shows: "Found 12 public methods, 3 have tests, 9 gaps identified"
3. User says: "Generate comprehensive tests"
4. Calls `aura_generate(operation: "tests")` 
5. Creates/updates `WorkflowServiceTests.cs` with 15 new tests
6. Builds and runs tests
7. Reports: "15 tests generated, all passing. Review TODO comments for mock setup."
