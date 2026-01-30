# Post-Code Build Validation in ReAct Executor

**Status:** ✅ Complete
**Completed:** 2025-01-30
**Priority:** High
**Type:** Feature / Reliability

## Problem Statement

Agents that modify code files often declare "finish" without verifying compilation. This results in broken code being committed to stories. The validation should be automatic and language-agnostic, happening transparently in the ReAct loop.

## Design

### Location

`ReActExecutor.cs` - intercept the "finish" action before returning success.

### Flow

```
Agent: "finish" action
    ↓
Executor: Check if code files were modified in prior steps
    ↓
If yes → Detect language(s) from file extensions
    ↓
Run build command(s) per language
    ↓
If build fails → Inject errors as observation, continue loop
    ↓
If build passes (or no code files) → Return success
```

### Language Detection

Map file extensions to build commands:

| Extension(s) | Language | Build Command | Working Dir Detection |
|--------------|----------|---------------|----------------------|
| `.cs` | C# | `dotnet build` | Find nearest `.sln` or `.csproj` |
| `.ts`, `.tsx` | TypeScript | `npx tsc --noEmit` | Find nearest `tsconfig.json` |
| `.py` | Python | `python -m py_compile {file}` | Per-file, or `mypy` if configured |
| `.go` | Go | `go build ./...` | Find nearest `go.mod` |
| `.rs` | Rust | `cargo check` | Find nearest `Cargo.toml` |

### Implementation

```csharp
// In ReActExecutor.ExecuteAsync, at the "finish" handling:

if (parsed.Action.Equals("finish", StringComparison.OrdinalIgnoreCase))
{
    // Check for modified code files
    var modifiedCodeFiles = GetModifiedCodeFiles(steps);
    
    if (modifiedCodeFiles.Count > 0)
    {
        var validationResult = await ValidateCodeChangesAsync(
            modifiedCodeFiles, 
            options.WorkingDirectory, 
            ct);
        
        if (!validationResult.Success)
        {
            // Inject errors as observation, continue loop
            observation = $"""
                ❌ Build validation failed. You cannot finish until these errors are fixed:
                
                {validationResult.Errors}
                
                Use file.modify to fix the errors, then try finishing again.
                """;
            
            // Continue the loop instead of returning
            steps.Add(new ReActStep { ... observation ... });
            conversationHistory.Add(...);
            continue;
        }
    }
    
    // Build passed or no code files - return success
    return new ReActResult { Success = true, ... };
}
```

### Helper Methods

```csharp
private List<string> GetModifiedCodeFiles(IReadOnlyList<ReActStep> steps)
{
    var codeExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".ts", ".tsx", ".py", ".go", ".rs", ".fs", ".js", ".jsx"
    };
    
    return steps
        .Where(s => s.Action is "file.write" or "file.modify")
        .Select(s => ExtractFilePath(s.ActionInput))
        .Where(path => codeExtensions.Contains(Path.GetExtension(path)))
        .Distinct()
        .ToList();
}

private async Task<ValidationResult> ValidateCodeChangesAsync(
    List<string> files,
    string? workingDirectory,
    CancellationToken ct)
{
    // Group by language
    var byLanguage = files.GroupBy(GetLanguageFromExtension);
    
    var errors = new StringBuilder();
    
    foreach (var group in byLanguage)
    {
        var result = group.Key switch
        {
            "csharp" => await RunDotnetBuildAsync(workingDirectory, ct),
            "typescript" => await RunTscAsync(workingDirectory, ct),
            "python" => await RunPythonCheckAsync(group.ToList(), workingDirectory, ct),
            _ => ValidationResult.Success // Unknown language, skip
        };
        
        if (!result.Success)
        {
            errors.AppendLine($"## {group.Key} errors:");
            errors.AppendLine(result.Errors);
        }
    }
    
    return errors.Length > 0 
        ? new ValidationResult(false, errors.ToString())
        : ValidationResult.Success;
}
```

### Edge Cases

1. **Multiple languages in one step** - Validate all, aggregate errors
2. **Build already failing before agent ran** - Only report new errors? Or all?
3. **Agent creates invalid path** - File.write to non-existent directory should fail at tool level
4. **Timeout** - Build commands need timeouts (30s default)
5. **Infinite loop** - Max 3 validation failures before force-failing the step

### Configuration

Add to `ReActOptions`:

```csharp
/// <summary>
/// Whether to validate code compilation before allowing finish.
/// Default: true.
/// </summary>
public bool ValidateCodeOnFinish { get; init; } = true;

/// <summary>
/// Maximum validation retry attempts before force-failing.
/// Default: 3.
/// </summary>
public int MaxValidationRetries { get; init; } = 3;

/// <summary>
/// Timeout for build commands in seconds.
/// Default: 30.
/// </summary>
public int BuildTimeoutSeconds { get; init; } = 30;
```

## Acceptance Criteria

- [ ] Agent cannot "finish" with compilation errors in modified files
- [ ] Works for C#, TypeScript, Python at minimum
- [ ] Errors are injected as observation, loop continues
- [ ] Max retry limit prevents infinite loops
- [ ] Can be disabled via `ValidateCodeOnFinish = false`
- [ ] Build timeout prevents hanging
- [ ] Language-agnostic design allows adding more languages

## Future Enhancements

- Run tests after build (optional)
- Cache "known good" build state to detect regressions
- Use language YAML configs for build commands instead of hardcoding
