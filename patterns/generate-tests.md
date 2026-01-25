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

```
aura_generate(
  operation: "tests",
  target: "{ClassName}",
  solutionPath: "{path}",
  count: {optional},
  maxTests: 20,
  focus: "all",  // or "happy_path", "edge_cases", "error_handling"
  analyzeOnly: false
)
```

### 4. Fix Generated Tests (Critical)

Aura's generated tests are **skeletons** that require post-processing:

#### 4.1 Fix Imports

Common import issues to fix:

| Generated | Should Be |
|-----------|-----------|
| `IFileSystem` (unqualified) | Add `using System.IO.Abstractions;` |
| `ILogger<T>` (unqualified) | Add `using Microsoft.Extensions.Logging;` |
| `Assert.NotNull(x)` | Consider `using FluentAssertions;` with `x.Should().NotBeNull()` |

#### 4.2 Fix Test Infrastructure

**For classes using `IFileSystem`:**
```csharp
// Replace NSubstitute mock with MockFileSystem
using System.IO.Abstractions.TestingHelpers;

private readonly MockFileSystem _fileSystem = new();
```

**For classes with `ILogger<T>` dependencies:**
```csharp
// Replace NSubstitute mock with NullLogger
using Microsoft.Extensions.Logging.Abstractions;

var logger = NullLogger<TargetClass>.Instance;
```

**For classes implementing `IDisposable`:**
```csharp
public class FooTests : IDisposable
{
    private readonly Foo _sut;
    
    public FooTests()
    {
        _sut = new Foo(...);
    }
    
    public void Dispose()
    {
        _sut.Dispose();
        GC.SuppressFinalize(this);
    }
}
```

#### 4.3 Replace Placeholder Assertions

Aura generates placeholders like:
```csharp
// TODO: Add meaningful assertions
Assert.NotNull(result);
```

Replace with behavior-specific assertions based on actual method behavior:
```csharp
// For methods that return null on "not found":
result.Should().BeNull();

// For collection methods:
result.Should().BeEmpty();
result.Should().HaveCount(2);

// For methods with side effects:
_sut.SomeProperty.Should().Be(expectedValue);
```

#### 4.4 Add Test Data

For configuration loaders, create realistic test fixtures:
```csharp
private const string ValidYaml = """
    language:
      id: python
      name: Python
    capabilities:
      - coding
    """;

// In test:
_fileSystem.AddFile("/config/test.yaml", new MockFileData(ValidYaml));
```

### 5. Build and Verify

```bash
dotnet build {test-project}
dotnet test {test-project} --filter "FullyQualifiedName~{ClassName}"
```

### 6. Iterate if Needed

If user wants more tests or different focus:
- Re-run with different `focus` parameter
- Or use `aura_generate method` with `body` for specific tests

## Alternative: Fine-Grained Control with `aura_generate method`

For maximum control, use `aura_generate method` with `body` parameter:

```
aura_generate(
  operation: "method",
  solutionPath: "{path}",
  className: "FooTests",
  methodName: "GetItem_WithValidId_ReturnsItem",
  body: """
    // Arrange
    _fileSystem.AddFile("/items/test.json", new MockFileData("{...}"));
    await _sut.LoadAsync("/items");

    // Act
    var result = _sut.GetItem("test-id");

    // Assert
    result.Should().NotBeNull();
    result!.Name.Should().Be("Test Item");
    """
)
```

This gives full control over test logic while Aura handles:
- **`[Fact]` attribute** - automatically added when class has xUnit reference
- **Return type inference** - detects `async Task` from `await` keywords in body
- **Method visibility** - defaults to `public` for test methods

You do NOT need to specify `isAsync: true` or `returnType: "Task"` - Aura infers these from the body content.

## Anti-patterns

❌ **Never generate tests without analyzing first** - you need to know what exists

❌ **Never use generated tests without fixing imports and assertions** - they won't compile or pass

❌ **Never generate more than `maxTests` (default 20)** - overwhelming

❌ **Never skip the build step** - generated tests may have syntax issues

❌ **Never assume mock setup is correct** - review `// TODO:` comments

❌ **Never use text-based `create_file` when Aura tools are available** - defeats dogfooding

## Test Gap Categories

| Gap | Description | Example Test |
|-----|-------------|--------------|
| `NoTests` | Method has no tests | Basic happy path |
| `NullHandling` | Nullable params not tested | Pass null, expect exception |
| `ErrorHandling` | Exception paths not tested | Trigger error condition |
| `BoundaryValues` | Edge cases not tested | Empty list, max int |
| `AsyncBehavior` | Async methods not tested | Await properly, test cancellation |

## Known Aura Test Generation Limitations

| # | Issue | Workaround |
|---|-------|------------|
| 1 | Wrong folder placement (defaults to `/Agents/`) | Move file to correct location after generation |
| 2 | Missing/unqualified namespace imports | Add full namespace imports manually |
| 3 | Placeholder assertions that fail | Replace with behavior-specific assertions |
| 4 | No test data generation for config loaders | Manually create YAML/JSON fixtures |
| 5 | Uses NSubstitute for all mocks | Replace `ILogger` mocks with `NullLogger` |
| 6 | No IDisposable cleanup pattern | Add `IDisposable` implementation if SUT requires disposal |
| 7 | File locking on parallel writes | Sequential method additions only |

## What Aura Does Correctly

When using `aura_generate method` with a `body` parameter in a test class:

| Feature | Behavior |
|---------|----------|
| `[Fact]` attribute | Auto-added when target class is in xUnit test project |
| Async detection | Infers `async Task` return type from `await` in body |
| Method visibility | Defaults to `public` for test methods |
| Method insertion | Places new methods at end of class before closing brace |
| Whitespace | Preserves class formatting and indentation |

This means you only need to provide:
- `className` - the test class name
- `methodName` - following `Method_Condition_ExpectedResult` convention
- `body` - the Arrange/Act/Assert code

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

## Complete Example Workflow

**User**: "Write tests for the GuardianRegistry class"

**Step 1 - Analyze:**
```
aura_generate(operation: "tests", target: "GuardianRegistry", analyzeOnly: true)
```
→ "Found 5 testable members, 4 gaps identified"

**Step 2 - Generate:**
```
aura_generate(operation: "tests", target: "GuardianRegistry", focus: "all")
```
→ Creates skeleton tests with TODO placeholders

**Step 3 - Fix imports:**
```
replace_string_in_file(...)
```
→ Add `System.IO.Abstractions.TestingHelpers`, `FluentAssertions`, `Microsoft.Extensions.Logging.Abstractions`

**Step 4 - Fix infrastructure:**
```
replace_string_in_file(...)
```
→ Replace `Substitute.For<IFileSystem>()` with `new MockFileSystem()`

**Step 5 - Fix assertions:**
```
replace_string_in_file(...)
```
→ Replace `Assert.NotNull(result)` with `result.Should().BeNull()` (when that's the expected behavior)

**Step 6 - Build and test:**
```
dotnet test --filter "GuardianRegistryTests"
```
→ "4 passed, 0 failed"
