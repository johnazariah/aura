# Automated Test Generation

**Status:** ✅ Complete (Phase 1: C#)
**Completed:** 2026-01-28
**Created:** 2026-01-17

## Overview

MCP tool operation `aura_generate(operation: "tests")` for automated test generation. The primary UX is **hands-off**: "test this class" generates comprehensive tests without requiring the user to specify a count.

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
    "testsAdded": 5
  },
  "stoppingReason": "All public methods have at least one test"
}
```

### MCP Tool Schema

```
operation: "tests"
target: string          # Class name, method name, or namespace
solutionPath: string    # For C#
count: number?          # Optional: explicit count (default: comprehensive)
maxTests: number?       # Optional: hard cap (default: 20)
focus: string?          # Optional: "edge_cases" | "error_handling" | "happy_path" | "all"
testFramework: string?  # Optional: override detection (xunit, nunit, mstest)
analyzeOnly: boolean?   # Return analysis without generating
validateCompilation: boolean?  # Check generated code compiles
outputDirectory: string?  # Control test file placement
```

## Implementation

### Architecture

```
src/Aura.Module.Developer/
├── Services/
│   ├── Testing/
│   │   ├── ITestGenerationService.cs       # Interface + DTOs
│   │   ├── RoslynTestGenerator.cs          # C# implementation
```

### Phase 1: C# (Roslyn) - ✅ Complete

- [x] `ITestGenerationService` interface
- [x] `RoslynTestGenerator` implementation
- [x] xUnit, NUnit, MSTest template support
- [x] Gap detection (no tests for method)
- [x] Insert into existing test file or create new
- [x] Wire up in `aura_generate` MCP handler
- [x] Nullable parameter gap detection
- [x] Exception documentation gap detection
- [x] Unique test names for overloaded methods
- [x] Complete namespace collection for all types
- [x] Return-type aware assertions
- [x] Optional compilation validation
- [x] Required property initialization
- [x] Generic dependency namespace collection
- [x] Missing usings added when appending to existing files
- [x] Test files placed in correct folder mirroring source structure
- [x] `outputDirectory` parameter to control test file placement
- [x] Static class detection - generates static method calls
- [x] Mocking library namespace always included
- [x] `IOptions<T>` uses `Options.Create()` pattern

### Future Phases (Not Started)

**Phase 2: Python (tree-sitter)**
- `PythonTestGenerator` implementation
- pytest template (most common)
- Test discovery for Python

**Phase 3: Enhanced Gap Detection**
- Numeric bounds → boundary tests
- Multiple code paths → branch tests

**Phase 4: Polish**
- unittest template for Python
- Better TODO comments with parameter hints
- Mock setup suggestions

## Files Changed

- `src/Aura.Module.Developer/Services/Testing/ITestGenerationService.cs`
- `src/Aura.Module.Developer/Services/Testing/RoslynTestGenerator.cs`
- `src/Aura.Module.Developer/DeveloperModule.cs` (registration)
- `src/Aura.Api/Endpoints/McpHandler.cs` (MCP integration)

## Related

- [Pattern-Driven UX Gaps](completed/pattern-driven-ux-gaps.md) - 35 fixes applied to test generation
- [Multi-Language Refactoring](upcoming/multi-language-refactoring.md) - shares language detection
