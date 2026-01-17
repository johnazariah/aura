# Pattern: Generate Tests

Generate comprehensive unit tests for a class or module, covering happy paths, edge cases, and error handling.

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
