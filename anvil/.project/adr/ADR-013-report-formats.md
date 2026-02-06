---
title: "ADR-013: Report Formats"
status: "Accepted"
date: "2026-01-30"
authors: "Anvil Team"
tags: ["architecture", "reporting", "cli"]
supersedes: ""
superseded_by: ""
---

# ADR-013: Report Formats

## Status

Accepted

## Context

Anvil needs to output test results. Different scenarios need different formats:

| Scenario | Need |
|----------|------|
| **Developer at terminal** | Rich, colorful console output |
| **Programmatic access** | Machine-readable for tooling |
| **Historical comparison** | Stored in SQLite |

**Key constraint:** Anvil runs on a **desktop machine**, not in CI. We are not asking CI to run UI/E2E tests—those require a human at a workstation with VS Code, browsers, etc.

## Decision

**Anvil supports two report formats: Console (default) and JSON.**

### Console Output (Default)

Rich terminal output using Spectre.Console:

```
╭─────────────────────────────────────────────────────────╮
│                    Anvil Test Run                       │
│                    Run ID: run-456                      │
│                    2026-01-30 14:30:22                  │
╰─────────────────────────────────────────────────────────╯

 [1/5] cli-hello-world ································ ✅  12.3s
 [2/5] rest-api-basic ································· ❌  45.6s
       Error: Build failed - CS1002: ; expected
       Worktree: .worktrees/rest-api-basic-abc123
 [3/5] library-tests ·································· ✅  23.1s
 [4/5] add-feature ···································· ✅  34.2s
 [5/5] refactor-rename ································ ✅  18.9s

╭─────────────────────────────────────────────────────────╮
│  Summary                                                │
├─────────────────────────────────────────────────────────┤
│  Total:    5 stories                                    │
│  Passed:   4 (80%)                                      │
│  Failed:   1 (20%)                                      │
│  Duration: 2m 14s                                       │
╰─────────────────────────────────────────────────────────╯
```

### JSON Output

Machine-readable format for tooling:

```bash
anvil run stories/ --output json > results.json
anvil run stories/ --output json --output-file results.json
```

```json
{
  "runId": "run-456",
  "startedAt": "2026-01-30T14:30:22Z",
  "completedAt": "2026-01-30T14:32:36Z",
  "duration": "PT2M14S",
  "summary": {
    "total": 5,
    "passed": 4,
    "failed": 1,
    "passRate": 0.80
  },
  "results": [
    {
      "storyId": "cli-hello-world",
      "status": "passed",
      "duration": "PT12.3S",
      "worktreePath": null
    },
    {
      "storyId": "rest-api-basic",
      "status": "failed",
      "duration": "PT45.6S",
      "errorMessage": "Build failed - CS1002: ; expected",
      "worktreePath": ".worktrees/rest-api-basic-abc123",
      "auraRequestId": "req-789"
    }
  ]
}
```

### CLI Options

```bash
# Default: console output
anvil run stories/

# JSON to stdout
anvil run stories/ --output json

# JSON to file (console still shows progress)
anvil run stories/ --output-file results.json

# Quiet mode (no console, just JSON)
anvil run stories/ --output json --quiet
```

### Implementation

```csharp
public interface IReportWriter
{
    Task WriteAsync(TestRunResult result, CancellationToken ct);
}

public class ConsoleReportWriter(IAnsiConsole console) : IReportWriter
{
    public async Task WriteAsync(TestRunResult result, CancellationToken ct)
    {
        // Spectre.Console rendering
    }
}

public class JsonReportWriter(TextWriter output) : IReportWriter
{
    public async Task WriteAsync(TestRunResult result, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(result, _options);
        await output.WriteLineAsync(json);
    }
}
```

## Consequences

**Positive**

- **POS-001**: Simple—only two formats to maintain
- **POS-002**: Console is rich and developer-friendly
- **POS-003**: JSON enables scripting and tooling
- **POS-004**: SQLite provides historical data (no need for file-based history)
- **POS-005**: No CI integration complexity

**Negative**

- **NEG-001**: No JUnit XML (not needed—not running in CI)
- **NEG-002**: No HTML reports (can add later if needed)

## Alternatives Considered

### Alternative 1: Console Only

- **Description**: Only Spectre.Console output
- **Rejection Reason**: No programmatic access for tooling

### Alternative 2: Console + JSON + JUnit

- **Description**: Add JUnit XML for CI
- **Rejection Reason**: Anvil runs on desktop, not CI; JUnit unnecessary

### Alternative 3: All Formats (Console, JSON, JUnit, HTML)

- **Description**: Maximum flexibility
- **Rejection Reason**: Over-engineering; YAGNI

## Implementation Notes

- **IMP-001**: Use Spectre.Console for rich terminal output
- **IMP-002**: Use System.Text.Json for JSON serialization
- **IMP-003**: `--output json` writes to stdout
- **IMP-004**: `--output-file` writes to file while showing console progress
- **IMP-005**: `--quiet` suppresses console output

## References

- [Spectre.Console Documentation](https://spectreconsole.net/)
- [ADR-005: Database Strategy](ADR-005-database-strategy.md)
