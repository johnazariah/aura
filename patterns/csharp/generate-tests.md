# C# Test Generation Overlay

This overlay extends the base `generate-tests` pattern with C#-specific guidance.

## Aura Tools Available

C# has full semantic tooling via Roslyn:

| Tool | Operation | Use For |
|------|-----------|---------|
| `aura_generate` | `tests` | Bulk test generation with analysis |
| `aura_generate` | `method` | Individual test with full body control |
| `aura_validate` | `compilation` | Verify tests compile |
| `aura_validate` | `tests` | Run tests |

## What `aura_generate tests` Provides

| Feature | Details |
|---------|---------|
| Testable method analysis | `analyzeOnly: true` for gap discovery |
| **Unique test names for overloads** | `DoAsync_WithString_...` vs `DoAsync_WithInt_...` |
| **Complete namespace collection** | All parameter/return type namespaces in usings |
| **Return-type aware assertions** | `bool` → `Assert.True`, `string` → `Assert.NotEmpty`, collections → `Assert.NotEmpty` |
| **Optional compile validation** | `validateCompilation: true` returns diagnostics |
| Framework detection | xUnit, NUnit, MSTest |
| Mocking library detection | NSubstitute, Moq, FakeItEasy |

## What Agent May Still Need to Fix

| Issue | Fix |
|-------|-----|
| `IFileSystem` without TestingHelpers | Add `using System.IO.Abstractions.TestingHelpers;` + use `MockFileSystem` |
| `Substitute.For<ILogger>()` | Replace with `NullLogger<T>.Instance` |
| Wrong test folder placement | Move file to correct project/folder |
| Complex domain assertions | Enhance for business logic validation |

## Common Import Fixes

```csharp
// ❌ Aura generates
IFileSystem fileSystem;
ILogger<T> logger;

// ✅ Agent must add
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Microsoft.Extensions.Logging.Abstractions;

// ✅ And use
var fileSystem = new MockFileSystem();
var logger = NullLogger<MyService>.Instance;
```

## Test Infrastructure Patterns

### File System Testing
```csharp
using System.IO.Abstractions.TestingHelpers;

var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
{
    { "/config/settings.yaml", new MockFileData("key: value") }
});
```

### Logger Testing
```csharp
using Microsoft.Extensions.Logging.Abstractions;

var logger = NullLogger<MyService>.Instance;
// OR if you need to verify log calls:
var logger = Substitute.For<ILogger<MyService>>();
```

### Disposable Test Class
```csharp
public sealed class MyServiceTests : IDisposable
{
    private readonly MockFileSystem _fileSystem = new();
    
    public void Dispose()
    {
        // Cleanup if needed
    }
}
```

## Recommended Workflow

### Approach A: Generate with Validation (Recommended)

```
# 1. Analyze coverage gaps
aura_generate(operation: "tests", target: "MyService", analyzeOnly: true, solutionPath: "...")

# 2. Generate with compile validation
aura_generate(operation: "tests", target: "MyService", validateCompilation: true, solutionPath: "...")

# Response includes:
# - compilesSuccessfully: true/false
# - compilationDiagnostics: ["CS0246 - Type not found (line 15)"]

# 3. If errors, fix diagnostics (usually domain-specific like MockFileSystem)
# 4. Run tests
aura_validate(operation: "tests", projectPath: "...", filter: "MyServiceTests")
```

### Approach B: Individual Method Generation (Full Control)

```
aura_generate(
  operation: "method",
  solutionPath: "...",
  className: "MyServiceTests", 
  methodName: "LoadConfig_WhenFileExists_ReturnsConfig",
  returnType: "void",
  body: "// Arrange\nvar fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>\n{\n    { \"/config.yaml\", new MockFileData(\"key: value\") }\n});\nvar sut = new ConfigLoader(fileSystem);\n\n// Act\nvar result = sut.LoadConfig(\"/config.yaml\");\n\n// Assert\nresult.Should().NotBeNull();\nresult.Key.Should().Be(\"value\");"
)
```

## Test Frameworks

### xUnit (Preferred)
```csharp
[Fact]
public void Method_WhenCondition_ShouldResult() { }

[Theory]
[InlineData("input1", "expected1")]
public void Method_WithVariousInputs_ShouldHandle(string input, string expected) { }
```

### NUnit
```csharp
[Test]
public void Method_WhenCondition_ShouldResult() { }

[TestCase("input1", "expected1")]
public void Method_WithVariousInputs_ShouldHandle(string input, string expected) { }
```

## Assertion Libraries

Prefer FluentAssertions:
```csharp
result.Should().BeTrue();
result.Should().BeEquivalentTo(expected);
action.Should().Throw<InvalidOperationException>();
```

## Anti-patterns

❌ Don't use `create_file` for C# test files - use `aura_generate`
❌ Don't use `Substitute.For<ILogger>()` - use `NullLogger<T>.Instance`
❌ Don't leave `// TODO:` assertions - replace with real assertions
❌ Don't skip `aura_validate` after generation
