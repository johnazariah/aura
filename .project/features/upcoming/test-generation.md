# Automated Test Generation

**Status:** � In Progress (Phase 1 Complete)  
**Priority:** High  
**Estimated Effort:** 1-2 weeks  
**Created:** 2026-01-17  
**Phase 1 Completed:** 2026-01-17  
**Dependencies:** Roslyn infrastructure (complete), Tree-sitter Python parsing (complete)

## Overview

Add MCP tool operations for automated test generation. The primary UX is **hands-off**: "test this class" generates comprehensive tests without requiring the user to specify a count. Explicit count is an opt-in override.

## Goals

1. **Simple intent**: User says "write tests for X" → tests are written
2. **No coverage tooling required**: Uses static analysis, not coverage data
3. **Multi-language**: C# (Roslyn) and Python (tree-sitter) from day one
4. **Guardrails**: Hard cap on test count, clear stopping conditions

## Non-Goals

- Coverage percentage tracking (user's job to verify)
- Test execution (user runs tests separately)
- Mutation testing
- Mocking framework integration (v1 generates stubs, user adds mocks)

## User Experience

### Primary Flow (Hands-Off)

```
User: "Write tests for OrderService"

Agent calls:
  aura_generate(operation: "tests", target: "OrderService", solutionPath: "...")

Tool returns:
{
  "analysis": {
    "testableMembers": ["PlaceOrder", "CancelOrder", "GetOrderStatus"],
    "existingTests": ["OrderServiceTests.cs (2 tests)"],
    "gaps": ["CancelOrder (no tests)", "PlaceOrder (no error handling test)"]
  },
  "generated": {
    "testFile": "c:\\...\\OrderServiceTests.cs",
    "testsAdded": 5,
    "summary": [
      "PlaceOrder_WithValidOrder_ReturnsOrderId",
      "PlaceOrder_WithNullOrder_ThrowsArgumentNullException",
      "PlaceOrder_WithInvalidItems_ThrowsValidationException",
      "CancelOrder_WithExistingOrder_ReturnsTrue",
      "CancelOrder_WithNonExistentOrder_ReturnsFalse"
    ]
  },
  "stoppingReason": "All public methods have at least one test"
}
```

### Explicit Count (Opt-In)

```
aura_generate(operation: "tests", target: "OrderService", count: 3, ...)
```

Generates exactly 3 tests, prioritizing gaps.

### Target Granularity

| Target | Scope |
|--------|-------|
| `"OrderService"` | All public methods in class |
| `"OrderService.PlaceOrder"` | Single method |
| `"Aura.Module.Developer"` | All public classes in namespace/project |

## Technical Design

### MCP Tool Schema

Add to `aura_generate`:

```
operation: "tests"
target: string          # Class name, method name, or namespace
solutionPath: string    # For C#
projectPath: string     # For Python
count: number?          # Optional: explicit count (default: comprehensive)
maxTests: number?       # Optional: hard cap (default: 20)
focus: string?          # Optional: "edge_cases" | "error_handling" | "happy_path" | "all"
testFramework: string?  # Optional: override detection (xunit, nunit, pytest, unittest)
```

### Stopping Conditions (Priority Order)

1. **All public methods have at least one test** → done ✅
2. **`maxTests` reached** → done with note ("reached limit, N methods still untested")
3. **`count` specified and reached** → done

### Gap Detection Heuristics

Static analysis signals (no coverage data needed):

| Signal | Test Suggestion |
|--------|-----------------|
| Method has no tests calling it | Happy path test |
| Method has nullable parameter | Null input test |
| Method has numeric parameter | Boundary value tests (0, -1, max) |
| Method throws exceptions (documented or inferred) | Error handling tests |
| Method has multiple code paths (if/switch) | Branch coverage tests |
| Method has async signature | Async completion test |

### Test Discovery

Find existing tests by:
1. **Naming convention**: `*Tests.cs`, `*Test.cs`, `test_*.py`, `*_test.py`
2. **Attribute detection**: `[Fact]`, `[Test]`, `@pytest.mark`, `def test_*`
3. **Symbol grep**: Search test files for target method name

### Test Generation Strategy

1. **Find or create test file**
   - Look for `{ClassName}Tests.cs` / `test_{module}.py`
   - If not found, create in conventional location (`/tests/` or same folder)

2. **Detect framework**
   - C#: Check project references (xUnit, NUnit, MSTest)
   - Python: Check imports (pytest, unittest)

3. **Generate test methods**
   - Use idiomatic framework patterns
   - Include Arrange/Act/Assert structure
   - Add `// TODO:` comments for assertions user should verify

4. **Insert into file**
   - Append to existing test class
   - Or create new test class if needed

### Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    aura_generate(operation: "tests")            │
├─────────────────────────────────────────────────────────────────┤
│  Language Detection (from target symbol lookup)                 │
│  C#  → ITestGenerationService (Roslyn)                         │
│  .py → IPythonTestGenerationService (tree-sitter)              │
└─────────────────────────────────────────────────────────────────┘

ITestGenerationService:
  - AnalyzeTestSurface(target) → {methods, existing, gaps}
  - DetectFramework(project) → {framework, conventions}
  - GenerateTests(target, options) → {file, tests, stoppingReason}

IRoslynTestGenerator : ITestGenerationService
  - Uses Roslyn to find public methods
  - Uses Roslyn to find exception throws
  - Generates C# test methods

IPythonTestGenerator : ITestGenerationService
  - Uses tree-sitter to find public functions
  - Generates Python test functions
```

### C# Implementation Details

**Finding testable surface:**
```csharp
// Get all public methods, excluding:
// - Property getters/setters (test via property)
// - Constructors (test via instantiation)
// - Object overrides (ToString, Equals, GetHashCode)
var methods = typeSymbol.GetMembers()
    .OfType<IMethodSymbol>()
    .Where(m => m.DeclaredAccessibility == Accessibility.Public)
    .Where(m => m.MethodKind == MethodKind.Ordinary)
    .ToList();
```

**Framework detection:**
```csharp
var references = project.MetadataReferences;
if (references.Any(r => r.Display.Contains("xunit"))) return "xunit";
if (references.Any(r => r.Display.Contains("nunit"))) return "nunit";
if (references.Any(r => r.Display.Contains("MSTest"))) return "mstest";
return "xunit"; // default
```

**Test template (xUnit):**
```csharp
[Fact]
public void {MethodName}_{Scenario}_{ExpectedResult}()
{
    // Arrange
    var sut = new {ClassName}(/* TODO: dependencies */);
    
    // Act
    var result = sut.{MethodName}(/* TODO: parameters */);
    
    // Assert
    // TODO: Add assertions
    Assert.NotNull(result);
}
```

### Python Implementation Details

**Finding testable surface:**
```python
# Tree-sitter query for public functions
(function_definition
  name: (identifier) @name
  (#not-match? @name "^_"))  # Exclude private (underscore prefix)
```

**Framework detection:**
```python
# Check imports in test files
if "pytest" in imports: return "pytest"
if "unittest" in imports: return "unittest"
return "pytest"  # default
```

**Test template (pytest):**
```python
def test_{method_name}_{scenario}():
    """Test that {method_name} {expected_behavior}."""
    # Arrange
    sut = {ClassName}()  # TODO: dependencies
    
    # Act
    result = sut.{method_name}()  # TODO: parameters
    
    # Assert
    assert result is not None  # TODO: real assertions
```

## File Structure

```
src/Aura.Module.Developer/
├── Services/
│   ├── Testing/
│   │   ├── ITestGenerationService.cs       # Interface + all DTOs
│   │   ├── RoslynTestGenerator.cs          # C# implementation ✅
│   │   └── PythonTestGenerator.cs          # Python implementation (Phase 2)
```

## Phases

### Phase 1: C# (Roslyn) - MVP ✅ COMPLETE
- [x] `ITestGenerationService` interface
- [x] `RoslynTestGenerator` implementation
- [x] xUnit template (most common)
- [x] Basic gap detection (no tests for method)
- [x] Insert into existing test file or create new
- [x] Wire up in `aura_generate` MCP handler
- [x] NUnit and MSTest template support
- [x] Nullable parameter gap detection
- [x] Exception documentation gap detection

### Phase 2: Python (tree-sitter)
- [ ] `PythonTestGenerator` implementation
- [ ] pytest template (most common)
- [ ] Test discovery for Python

### Phase 3: Enhanced Gap Detection
- [ ] Numeric bounds → boundary tests
- [ ] Multiple code paths → branch tests

### Phase 4: Polish
- [ ] unittest template for Python
- [ ] Better TODO comments with parameter hints
- [ ] Mock setup suggestions

## Success Criteria

1. `aura_generate(operation: "tests", target: "SomeClass")` generates useful tests
2. Tests compile (C#) / parse (Python) without errors
3. Agent can use this in a workflow: "increase test coverage for module X"
4. Existing tests are not duplicated
5. Generated tests have clear TODOs for user to complete

## Open Questions

1. **Test file location**: Same folder as source, or `/tests/` mirror?
   - Recommendation: Match existing project convention, default to `/tests/` mirror

2. **Dependency injection**: Generate constructor mocks?
   - Recommendation: v1 uses `TODO` comments, v2 adds Moq/NSubstitute setup

3. **Async methods**: Special handling?
   - Recommendation: Generate async test methods with `await`

## Related

- [Multi-Language Refactoring](multi-language-refactoring.md) - shares language detection
- [MCP Tools Enhancement](../completed/mcp-tools-enhancement.md) - foundation
