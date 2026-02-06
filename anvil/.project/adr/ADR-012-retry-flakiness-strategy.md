---
title: "ADR-012: Retry and Flakiness Strategy"
status: "Accepted"
date: "2026-01-30"
authors: "Anvil Team"
tags: ["architecture", "reliability", "testing"]
supersedes: ""
superseded_by: ""
---

# ADR-012: Retry and Flakiness Strategy

## Status

Accepted

## Context

Story execution can fail for various reasons:

| Failure Type | Example | Retryable? |
|--------------|---------|------------|
| **Transient network** | Aura API timeout | Maybe |
| **Rate limiting** | LLM provider throttling | Maybe |
| **LLM non-determinism** | Generated code differs each run | No |
| **Aura bug** | Agent produces invalid code | No |
| **Story bug** | Ambiguous requirements | No |
| **Infrastructure** | Aura service down | Maybe |

The question: should Anvil automatically retry failed stories?

## Decision

**Anvil does not retry failed stories.** Failures are recorded and reported immediately.

### Rationale

The purpose of Anvil is to **detect bugs in Aura**. Retrying masks these bugs:

- If a story fails due to LLM non-determinism → Aura's prompts need improvement
- If a story fails due to network issues → Aura's error handling needs improvement
- If a story fails due to rate limits → Aura's throttling needs improvement

**Every failure is a bug in Aura until proven otherwise.**

### Behavior

```
Story execution fails
        ↓
    Log failure with full context
        ↓
    Record in database
        ↓
    Continue to next story (unless --fail-fast)
        ↓
    Include in final report
```

### Error Recording

All failures are captured with sufficient context for debugging:

```csharp
public record StoryResult
{
    public required string StoryId { get; init; }
    public required StoryStatus Status { get; init; }  // Passed, Failed, Error
    public required TimeSpan Duration { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorStackTrace { get; init; }
    public string? WorktreePath { get; init; }  // Preserved for debugging
    public string? AuraRequestId { get; init; }  // For correlating with Aura logs
}
```

### CLI Output

```bash
anvil run stories/

# [1/5] cli-hello-world ✅ (12.3s)
# [2/5] rest-api-basic ❌ (45.6s)
#       Error: Build failed - CS1002: ; expected
#       Worktree: /path/to/.worktrees/rest-api-basic-abc123
#       Aura Request: req-789
# [3/5] library-tests ✅ (23.1s)
# ...

# Summary: 4 passed, 1 failed
```

### Manual Retry

Users can manually retry a specific story:

```bash
# Re-run a failed story
anvil run stories/rest-api-basic.md

# Re-run all failed stories from a previous run
anvil rerun run-123 --failed-only
```

### Future: Flakiness Detection

If a story passes sometimes and fails sometimes across multiple runs, that's valuable data. SQLite stores results, enabling queries like:

```sql
-- Find flaky stories (passed and failed in last 7 days)
SELECT StoryId, 
       COUNT(CASE WHEN Status = 'passed' THEN 1 END) as passes,
       COUNT(CASE WHEN Status = 'failed' THEN 1 END) as failures
FROM StoryResults
WHERE ExecutedAt > datetime('now', '-7 days')
GROUP BY StoryId
HAVING passes > 0 AND failures > 0;
```

This is a reporting feature, not a retry feature.

## Consequences

**Positive**

- **POS-001**: Simple implementation
- **POS-002**: All failures are visible (not masked by retry)
- **POS-003**: Faster feedback (no retry delays)
- **POS-004**: Forces Aura bugs to be fixed, not worked around
- **POS-005**: Deterministic results (same input → same pass/fail)

**Negative**

- **NEG-001**: Transient failures appear as real failures
- **NEG-002**: May need manual re-runs to confirm flakiness
- **NEG-003**: CI may fail due to infrastructure issues

## Alternatives Considered

### Alternative 1: Auto-Retry N Times

- **Description**: Retry failed stories up to 3 times
- **Rejection Reason**: Masks bugs in Aura; retry success doesn't mean the bug is fixed

### Alternative 2: Retry Specific Errors

- **Description**: Only retry "retryable" errors (timeout, rate limit)
- **Rejection Reason**: Adds complexity; those errors should be handled in Aura

### Alternative 3: Configurable Retry

- **Description**: `--retry N` flag
- **Rejection Reason**: Encourages masking bugs; can add later if truly needed

## Implementation Notes

- **IMP-001**: No retry logic in StoryRunner
- **IMP-002**: Capture full error context (message, stack, request ID)
- **IMP-003**: Preserve failed worktrees for debugging
- **IMP-004**: `anvil rerun` command for manual retry of failed stories
- **IMP-005**: Flakiness detection as future reporting feature

## References

- [ADR-005: Database Strategy](ADR-005-database-strategy.md)
- [ADR-011: Concurrency Strategy](ADR-011-concurrency-strategy.md)
- [Aura Failure Investigation Protocol](../../../.github/copilot-instructions.md)
