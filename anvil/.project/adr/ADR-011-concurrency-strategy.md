---
title: "ADR-011: Concurrency Strategy"
status: "Accepted"
date: "2026-01-30"
authors: "Anvil Team"
tags: ["architecture", "concurrency", "execution"]
supersedes: ""
superseded_by: ""
---

# ADR-011: Concurrency Strategy

## Status

Accepted

## Context

When running multiple stories, Anvil could execute them sequentially or in parallel. Considerations:

- **Aura API**: Can handle concurrent requests, but LLM rate limits apply
- **VS Code testing**: Only one VS Code instance per user profile
- **Resource usage**: Parallel builds compete for CPU/disk
- **Complexity**: Parallel execution adds error handling complexity
- **Debugging**: Sequential is easier to reason about

## Decision

**Anvil executes stories sequentially.** One story at a time, in order.

### Execution Model

```
Story 1 → Execute → Validate → Complete
                                   ↓
Story 2 → Execute → Validate → Complete
                                   ↓
Story 3 → Execute → Validate → Complete
                                   ↓
                              Report
```

### Implementation

```csharp
public async Task<TestRunResult> RunAsync(
    IReadOnlyList<Story> stories,
    CancellationToken ct)
{
    var results = new List<StoryResult>();
    
    foreach (var story in stories)
    {
        ct.ThrowIfCancellationRequested();
        
        _logger.LogInformation("Running story {StoryId} ({Current}/{Total})",
            story.Id, results.Count + 1, stories.Count);
        
        var result = await ExecuteStoryAsync(story, ct);
        results.Add(result);
        
        // Optional: fail-fast on first failure
        if (result.Status == StoryStatus.Failed && _options.FailFast)
            break;
    }
    
    return new TestRunResult(results);
}
```

### CLI Behavior

```bash
# Run all stories sequentially
anvil run stories/

# Stop on first failure
anvil run stories/ --fail-fast

# Progress shown for each story
# [1/10] cli-hello-world ✅ (12.3s)
# [2/10] rest-api-basic ✅ (45.6s)
# [3/10] library-tests ❌ (23.1s) - Build failed
# Stopped: --fail-fast enabled
```

### Future Consideration

If parallelism is needed later, add:
- `--parallel N` flag
- Mode-specific limits (VS Code always sequential)
- Supersede this ADR with ADR-011b

## Consequences

**Positive**

- **POS-001**: Simple implementation
- **POS-002**: Predictable execution order
- **POS-003**: Easy to debug failures
- **POS-004**: No race conditions
- **POS-005**: Works with all execution modes (API, VS Code, CLI)
- **POS-006**: No Aura API contention concerns

**Negative**

- **NEG-001**: Slower for large story suites
- **NEG-002**: Underutilizes resources on multi-core machines
- **NEG-003**: CI runs take longer

## Alternatives Considered

### Alternative 1: Parallel by Default

- **Description**: Run stories concurrently with `Parallel.ForEachAsync`
- **Rejection Reason**: VS Code mode can't parallelize; adds complexity for initial version

### Alternative 2: Configurable Parallelism

- **Description**: `--parallel N` flag with default of 1
- **Rejection Reason**: Premature optimization; can add later if needed

## Implementation Notes

- **IMP-001**: Use `foreach` with `await`, not `Parallel.ForEachAsync`
- **IMP-002**: Support `--fail-fast` flag to stop on first failure
- **IMP-003**: Show progress: `[N/Total] story-id status (duration)`
- **IMP-004**: Total run duration in final report

## References

- [ADR-007: VS Code Extension Testing](ADR-007-vscode-extension-testing.md)
- [ADR-010: Workspace Isolation](ADR-010-workspace-isolation.md)
