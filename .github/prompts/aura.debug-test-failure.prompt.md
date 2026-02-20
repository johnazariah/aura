---
description: Systematic investigation of failing tests — read output, find root cause, fix properly.
---

# Debug Test Failure

You are investigating a test failure. Follow this structured approach to find and fix the root cause.

## Step 1: Reproduce and Read the Failure

```powershell
# Run the specific failing test(s)
dotnet test tests/{TestProject} --filter "FullyQualifiedName~{TestClassName}" --verbosity normal

# Or run all tests to see full picture
dotnet test --verbosity normal
```

**Capture the exact error:**
- Exception type and message
- Stack trace (which line in which file)
- Expected vs actual values (for assertion failures)
- Any inner exceptions

## Step 2: Classify the Failure

| Failure Type | Typical Cause | Investigation Path |
|--------------|---------------|-------------------|
| `Assert.*` failure | Logic bug or test expectation wrong | Check both test and source |
| `NullReferenceException` | Missing mock setup or null return | Check test arrangement |
| `InvalidOperationException` | Service misconfiguration | Check DI and test setup |
| Compilation error in test | Source signature changed | Update test to match new API |
| `Substitute.*` mismatch | NSubstitute argument mismatch | Check `Arg.Is<>` matchers |
| Timeout | Async deadlock or slow operation | Check `await` usage |

## Step 3: Read the Test Code

```powershell
# Find the test file
Get-ChildItem -Path tests/ -Recurse -Filter "*{TestClassName}*"
```

Understand the test:
- **Arrange**: What's being set up? Are mocks configured correctly?
- **Act**: What method is being called? With what arguments?
- **Assert**: What's expected? Is the expectation correct?

## Step 4: Read the Source Code

Navigate to the class/method under test:
- Check if the signature changed (new parameters, different return type)
- Check if behavior changed (new validation, different flow)
- Look for recent git changes: `git log --oneline -5 -- {source_file}`

## Step 5: Determine the Fix

**Test is wrong** (source changed legitimately):
- Update test expectations to match new behavior
- Add/remove mock setups for changed dependencies
- Update test method signatures

**Source is wrong** (test caught a regression):
- Fix the source code bug
- Verify other tests still pass after fix

**Both need updating** (refactoring needed):
- Fix source first, then update tests
- Run full test suite after each change

## Step 6: Common Patterns in This Codebase

### NSubstitute mock issues
```csharp
// Wrong: too strict matching
_service.GetAsync(Arg.Is<Guid>(g => g == someId), Arg.Any<CancellationToken>())
    .Returns(result);

// Better: use Arg.Any if the exact value doesn't matter for the test
_service.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
    .Returns(result);
```

### Logger mocking
```csharp
// Don't mock ILogger — use NullLogger
var logger = NullLogger<MyService>.Instance;
```

### IFileSystem mocking
```csharp
// Use MockFileSystem from System.IO.Abstractions.TestingHelpers
var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
{
    { "/path/to/file.cs", new MockFileData("content") }
});
```

### Result<T> assertions
```csharp
// Check success
Assert.True(result.IsSuccess);
Assert.NotNull(result.Value);

// Check failure
Assert.True(result.IsFailure);
Assert.Contains("expected error", result.Error);
```

## Step 7: Verify the Fix

```powershell
# Run the specific test
dotnet test tests/{TestProject} --filter "FullyQualifiedName~{TestName}" --verbosity normal

# Run the full test class to check for side effects
dotnet test tests/{TestProject} --filter "FullyQualifiedName~{TestClassName}" --verbosity normal

# Run all tests to ensure no regressions
dotnet test --verbosity minimal
```

## Step 8: Build Verification

```powershell
dotnet build -c Release --verbosity minimal
```

## Checklist

- [ ] Exact error message and stack trace captured
- [ ] Failure classified (assertion, null ref, compilation, etc.)
- [ ] Test code read and understood (arrange/act/assert)
- [ ] Source code checked for recent changes
- [ ] Root cause identified (test wrong vs source wrong)
- [ ] Fix applied to correct location
- [ ] Specific test passes
- [ ] Full test suite passes
- [ ] Build succeeds
