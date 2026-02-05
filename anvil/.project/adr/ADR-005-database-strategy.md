---
title: "ADR-005: Database Strategy"
status: "Accepted"
date: "2026-01-30"
authors: "Anvil Team"
tags: ["infrastructure", "persistence", "architecture", "regression"]
supersedes: ""
superseded_by: ""
---

# ADR-005: Database Strategy

## Status

Accepted

## Context

Anvil needs to persist:
- **Test run history** — When stories ran, which mode, duration, pass/fail
- **Individual story results** — Per-story outcomes, timing, errors, generated files
- **Regression data** — Historical comparisons, flaky story tracking, trend analysis

The database choice affects:
- Developer setup friction (do testers need to install anything?)
- Query capability (complex regression analysis vs simple lookups)
- Data volume (how long to retain history?)

Regression tracking is a key requirement — we want to detect when Aura's code generation quality degrades over time.

## Decision

We adopt **SQLite** as the embedded database for Anvil.

### Rationale

| Factor | SQLite Advantage |
|--------|------------------|
| **Zero setup** | File-based, ships with the app |
| **No dependencies** | No external services to install |
| **Works offline** | Local-first, no network required |
| **Simple backup** | Copy the `.db` file |
| **Sufficient queries** | SQL power for regression analysis |
| **Portable** | Database moves with the project |

### Schema Overview

```sql
-- Test runs (a collection of story executions)
CREATE TABLE TestRuns (
    Id TEXT PRIMARY KEY,
    StartedAt TEXT NOT NULL,
    CompletedAt TEXT,
    Mode TEXT NOT NULL,  -- 'copilot-cli', 'aura-agents', 'story-ui'
    TotalStories INTEGER,
    Passed INTEGER,
    Failed INTEGER,
    Skipped INTEGER
);

-- Individual story results
CREATE TABLE StoryResults (
    Id TEXT PRIMARY KEY,
    TestRunId TEXT NOT NULL REFERENCES TestRuns(Id),
    StoryId TEXT NOT NULL,
    StoryPath TEXT NOT NULL,
    Language TEXT NOT NULL,  -- 'csharp', 'python', 'typescript'
    Status TEXT NOT NULL,    -- 'passed', 'failed', 'skipped', 'error'
    DurationMs INTEGER,
    ErrorMessage TEXT,
    GeneratedFiles TEXT,     -- JSON array of file paths
    ExecutedAt TEXT NOT NULL
);

-- For regression tracking
CREATE INDEX IX_StoryResults_StoryId ON StoryResults(StoryId);
CREATE INDEX IX_StoryResults_ExecutedAt ON StoryResults(ExecutedAt);
```

### EF Core Integration

```csharp
public class AnvilDbContext : DbContext
{
    public DbSet<TestRun> TestRuns => Set<TestRun>();
    public DbSet<StoryResult> StoryResults => Set<StoryResult>();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite("Data Source=anvil.db");
}
```

### Regression Queries

```csharp
// Find stories that recently started failing
var recentRegressions = await context.StoryResults
    .Where(r => r.ExecutedAt > DateTime.UtcNow.AddDays(-7))
    .GroupBy(r => r.StoryId)
    .Where(g => g.OrderByDescending(r => r.ExecutedAt).First().Status == "failed"
             && g.OrderByDescending(r => r.ExecutedAt).Skip(1).First().Status == "passed")
    .Select(g => g.Key)
    .ToListAsync();

// Flaky story detection (passed and failed in recent runs)
var flakyStories = await context.StoryResults
    .Where(r => r.ExecutedAt > DateTime.UtcNow.AddDays(-7))
    .GroupBy(r => r.StoryId)
    .Where(g => g.Any(r => r.Status == "passed") && g.Any(r => r.Status == "failed"))
    .Select(g => new { StoryId = g.Key, PassRate = g.Count(r => r.Status == "passed") * 100 / g.Count() })
    .ToListAsync();
```

### Database Location

```csharp
// Default: alongside the CLI executable or in user's app data
var dbPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "Anvil",
    "anvil.db");
```

## Consequences

**Positive**
- **POS-001**: Zero installation — works immediately after build
- **POS-002**: Full SQL capability for regression analysis
- **POS-003**: EF Core provides migrations and LINQ queries
- **POS-004**: Database file is easily backed up or shared
- **POS-005**: Works offline and in CI environments

**Negative**
- **NEG-001**: Limited concurrent write access (fine for single-user CLI)
- **NEG-002**: Not suitable for multi-machine shared access
- **NEG-003**: Large history may need periodic cleanup

## Alternatives Considered

### Alternative 1: PostgreSQL
- **Description**: Full-featured relational database
- **Rejection Reason**: Requires installation or Docker; overkill for a CLI tool; adds operational burden

### Alternative 2: File-Based JSON
- **Description**: Store results as JSON files per run
- **Rejection Reason**: Difficult to query across runs; poor for regression analysis; no indexing

### Alternative 3: LiteDB (NoSQL)
- **Description**: Embedded NoSQL document database
- **Rejection Reason**: Less mature than SQLite; SQL queries are more powerful for analytics

## Implementation Notes

- **IMP-001**: Use EF Core with SQLite provider (`Microsoft.EntityFrameworkCore.Sqlite`)
- **IMP-002**: Create migrations for schema changes (`dotnet ef migrations add`)
- **IMP-003**: Store database in user's LocalApplicationData by default
- **IMP-004**: Allow database path override via configuration
- **IMP-005**: Consider periodic cleanup of old runs (e.g., keep last 90 days)

## References

- [SQLite Documentation](https://sqlite.org/docs.html)
- [EF Core SQLite Provider](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/)
- [Anvil Architecture](../architecture/project.md)
