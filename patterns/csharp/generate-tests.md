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

## What `aura_generate tests` Provides vs. Requires Fixing

| ✅ Aura Does | ❌ Agent Must Fix |
|--------------|------------------|
| Analyzes testable methods | Wrong folder placement (defaults to `Agents/`) |
| Creates test file with namespace | Unqualified imports (`IFileSystem` not full path) |
| Sets up Arrange/Act/Assert skeleton | Placeholder assertions (`// TODO:`) |
| Detects test framework (xUnit/NUnit/MSTest) | Uses `Substitute.For<ILogger>()` |
| Detects mocking library (NSubstitute/Moq/FakeItEasy) | No `IDisposable` cleanup pattern |

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

### Approach A: Bulk Generation + Fix

```
# 1. Analyze
aura_generate(operation: "tests", target: "MyService", analyzeOnly: true, solutionPath: "...")

# 2. Generate skeletons
aura_generate(operation: "tests", target: "MyService", solutionPath: "...")

# 3. Fix imports (use replace_string_in_file)
# 4. Fix infrastructure (NullLogger, MockFileSystem)
# 5. Replace placeholder assertions
# 6. Build and test
aura_validate(operation: "compilation", solutionPath: "...")
aura_validate(operation: "tests", projectPath: "...", filter: "MyServiceTests")
```

### Approach B: Individual Method Generation (More Control)

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
