# Layered Fleet Architecture

> **Status:** Draft  
> **Created:** 2026-01-16  
> **Author:** Aura Team

## Overview

This specification defines a **layered architecture** for Aura that separates concerns into distinct layers:

1. **Orchestration Layer** — Guardians that monitor repository health and create workflows
2. **Reasoning Layer** — Dual-mode execution (Aura agents OR GitHub Copilot)
3. **Expertise Layer** — Language/domain specialists that inform reasoning
4. **Capabilities Layer** — Unified tools (MCP + agent tools) and adapters

The key insight: **Guardians detect, workflows track, agents/Copilot reason, tools execute.**

```
┌─────────────────────────────────────────────────────────────┐
│  ORCHESTRATION    Guardians detect → Create workflows       │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  REASONING        Structured Mode    │  Conversational Mode │
│                   (Aura Agents)      │  (GitHub Copilot)    │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  EXPERTISE        Language Specialists (YAML configs)       │
│                   • Run as agents  • Inject as context      │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  CAPABILITIES     8 Meta-Tools │ RAG + Code Graph │ Adapters│
└─────────────────────────────────────────────────────────────┘
```

---

## Goals

1. **Surface hidden chores** — Guardians make maintenance work visible before it becomes debt
2. **Dual-mode execution** — Users choose local agents OR Copilot per workflow
3. **Unified expertise** — Same specialist knowledge works in both modes
4. **Non-intrusive** — Chores appear in the queue but don't interrupt feature work
5. **Extensible** — Hot-reloadable YAML definitions for guardians and specialists

## Non-Goals

1. Forcing users into one execution mode
2. Replacing GitHub Copilot (we extend it)
3. Autonomous fixing without human visibility

---

## Architecture

### Full System Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                      ORCHESTRATION LAYER                            │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  guardians/                         GuardianScheduler               │
│  ├─ ci-guardian.yaml                ├─ Cron triggers               │
│  ├─ test-coverage.yaml    ────────► ├─ File watchers               │
│  └─ documentation.yaml              └─ Webhook receivers           │
│                                              │                      │
│                                              ▼                      │
│                                     GuardianExecutor                │
│                                     └─► Creates Workflows           │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
                                         │
                                         ▼
┌─────────────────────────────────────────────────────────────────────┐
│                       REASONING LAYER                               │
├────────────────────────────┬────────────────────────────────────────┤
│    STRUCTURED MODE         │       CONVERSATIONAL MODE              │
│                            │                                        │
│  Aura Agents               │    GitHub Copilot                      │
│  ├─ Local/Cloud LLM        │    ├─ Cloud LLM (GPT-4, Claude)       │
│  ├─ ReAct execution loop   │    ├─ MCP tool calls                  │
│  └─ Step-by-step workflow  │    └─ Free-form chat in worktree      │
│                            │                                        │
│  Best for:                 │    Best for:                          │
│  • Batch operations        │    • Exploratory work                 │
│  • Privacy-sensitive       │    • Complex multi-file reasoning     │
│  • Offline/air-gapped      │    • Learning a codebase              │
│  • CI/CD integration       │    • Interactive debugging            │
│  • Predictable automation  │    • Ad-hoc refactoring               │
│                            │                                        │
└────────────────────────────┴────────────────────────────────────────┘
                                         │
                                         ▼
┌─────────────────────────────────────────────────────────────────────┐
│                       EXPERTISE LAYER                               │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  agents/languages/                  Domain Specialists              │
│  ├─ csharp.yaml                     ├─ testing-agent.md            │
│  ├─ python.yaml      ─────────────► ├─ documentation-agent.md      │
│  ├─ rust.yaml                       └─ build-fixer-agent.md        │
│  └─ typescript.yaml                                                │
│                                                                     │
│  Dual-use:                                                         │
│  • Structured Mode → Load as agent, run ReAct loop                 │
│  • Conversational Mode → Inject as context for Copilot             │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
                                         │
                                         ▼
┌─────────────────────────────────────────────────────────────────────┐
│                      CAPABILITIES LAYER                             │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  8 Meta-Tools                       Language Adapters               │
│  ├─ aura_search                     ├─ Roslyn (C#, F#)             │
│  ├─ aura_navigate                   ├─ rope (Python)               │
│  ├─ aura_inspect      ────────────► ├─ ts-morph (TypeScript)       │
│  ├─ aura_refactor                   ├─ LSP (Go, Rust)              │
│  ├─ aura_generate                   └─ TreeSitter (fallback)       │
│  ├─ aura_validate                                                  │
│  ├─ aura_workflow                   Context Services               │
│  └─ aura_architect                  ├─ RAG (semantic search)       │
│                                     └─ Code Graph (structural)     │
│  Exposed via:                                                      │
│  • MCP → Copilot calls directly                                    │
│  • Agent Tool Framework → Aura agents call                         │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

### Layer Responsibilities

| Layer | Responsibility | Components |
|-------|----------------|------------|
| **Orchestration** | Detect issues, create workflows | Guardians, Scheduler |
| **Reasoning** | Decide what to do, coordinate steps | Aura Agents OR Copilot |
| **Expertise** | Domain knowledge, conventions, patterns | Language/Domain YAML configs |
| **Capabilities** | Execute primitive operations | Tools, Adapters, RAG, Graph |

---

## Guardian Definition Format

Guardians are defined in `guardians/*.yaml` and hot-reloaded (same pattern as agents):

```yaml
# guardians/ci-guardian.yaml
id: ci-guardian
name: CI/CD Guardian
version: 1
description: Monitors CI pipelines and creates workflows for failures

triggers:
  - type: webhook
    events: [workflow_run.completed]  # GitHub Actions
  - type: schedule
    cron: "*/15 * * * *"  # Poll every 15 minutes (fallback)

detection:
  # How to detect issues
  sources:
    - type: github_actions
      repository: "{workspace.repository}"
      branches: [main, develop]
    - type: azure_pipelines
      project: "{workspace.azure_project}"
      
  failure_analysis:
    # Extract useful context from failures
    parse_logs: true
    identify_failing_tests: true
    identify_build_errors: true

workflow:
  # Template for created workflows
  title: "Fix CI: {failure_summary}"
  description: |
    CI pipeline failed on {branch}.
    
    **Pipeline:** {pipeline_name}
    **Failed at:** {timestamp}
    **Error:** {error_summary}
    
    ## Failure Details
    {parsed_failure_details}
    
  suggested_agent: build-fixer-agent
  priority: high
  
  context_gathering:
    - failure_logs
    - recent_commits
    - affected_files
```

```yaml
# guardians/test-coverage-guardian.yaml
id: test-coverage-guardian
name: Test Coverage Guardian
version: 1
description: Monitors test coverage and creates workflows for gaps

triggers:
  - type: schedule
    cron: "0 8 * * *"  # Daily 8 AM
  - type: file_changed
    patterns: ["src/**/*.cs", "src/**/*.ts"]
    debounce: 300  # Wait 5 min after changes settle

detection:
  tool: dotnet test --collect:"XPlat Code Coverage"
  parser: cobertura
  
  thresholds:
    overall_minimum: 70
    file_minimum: 50
    regression_tolerance: 5  # Alert if coverage drops 5%+

workflow:
  title: "Improve test coverage: {target}"
  description: |
    Test coverage needs attention.
    
    **Current:** {current_coverage}%
    **Threshold:** {threshold}%
    **Files needing tests:**
    {uncovered_files}
    
  suggested_agent: testing-agent
  priority: medium
  
  context_gathering:
    - coverage_report
    - uncovered_methods
    - similar_test_patterns  # RAG search
```

```yaml
# guardians/documentation-guardian.yaml
id: documentation-guardian
name: Documentation Guardian
version: 1
description: Detects missing or stale documentation

triggers:
  - type: file_changed
    patterns: ["src/**/*.cs", "src/**/*.ts", "src/**/*.py"]
  - type: schedule
    cron: "0 6 * * 1"  # Weekly Monday 6 AM

detection:
  rules:
    - id: public_members_undocumented
      description: Public API members without documentation
      languages: [csharp, typescript, python]
      
    - id: readme_outdated
      description: README doesn't reflect current structure
      check: file_age_vs_code_changes
      
    - id: api_docs_stale
      description: API docs don't match implementation
      check: signature_drift

workflow:
  title: "Document: {target}"
  description: |
    Documentation is missing or outdated.
    
    **Issue:** {rule_description}
    **Location:** {file_path}
    **Details:**
    {violation_details}
    
  suggested_agent: documentation-agent
  priority: low
```

---

## Guardian Interface

```csharp
/// <summary>
/// A guardian monitors repository health and creates workflows for issues.
/// Guardians do NOT fix issues directly — they detect and delegate.
/// </summary>
public interface IGuardian
{
    /// <summary>Unique identifier (matches YAML filename).</summary>
    string Id { get; }
    
    /// <summary>Human-readable name.</summary>
    string Name { get; }
    
    /// <summary>Triggers that activate this guardian.</summary>
    IReadOnlyList<GuardianTrigger> Triggers { get; }
    
    /// <summary>
    /// Check for violations. Called by scheduler or trigger.
    /// </summary>
    Task<GuardianCheckResult> CheckAsync(
        GuardianContext context, 
        CancellationToken ct);
    
    /// <summary>
    /// Create workflows for detected violations.
    /// </summary>
    Task<IReadOnlyList<CreateWorkflowRequest>> CreateWorkflowsAsync(
        IReadOnlyList<Violation> violations,
        GuardianContext context,
        CancellationToken ct);
}
```

### Supporting Types

```csharp
public record GuardianContext(
    /// <summary>Repository root path.</summary>
    string RepositoryPath,
    
    /// <summary>What triggered this check.</summary>
    GuardianTrigger Trigger,
    
    /// <summary>Files changed since last check (for incremental).</summary>
    IReadOnlyList<string>? ChangedFiles,
    
    /// <summary>External data (webhook payload, CI logs, etc.).</summary>
    IReadOnlyDictionary<string, object>? ExternalData
);

public record GuardianCheckResult(
    bool HasViolations,
    IReadOnlyList<Violation> Violations,
    GuardianMetrics Metrics
);

public record Violation(
    string RuleId,
    string Summary,
    string? FilePath,
    int? LineNumber,
    ViolationSeverity Severity,
    IReadOnlyDictionary<string, string> Context  // For workflow template
);

public record GuardianMetrics(
    TimeSpan CheckDuration,
    int FilesScanned,
    int ViolationsFound
);

public enum ViolationSeverity { Info, Warning, Error, Critical }
```

### Workflow Creation

Guardians create workflows using the existing workflow infrastructure:

```csharp
public record CreateWorkflowRequest(
    string Title,
    string Description,
    string RepositoryPath,
    
    /// <summary>Source guardian that created this workflow.</summary>
    string SourceGuardianId,
    
    /// <summary>Priority hint for user.</summary>
    WorkflowPriority Priority,
    
    /// <summary>Suggested agent capability for planning.</summary>
    string? SuggestedCapability,
    
    /// <summary>Pre-gathered context for RAG.</summary>
    IReadOnlyDictionary<string, string>? GatheredContext
);

public enum WorkflowPriority { Low, Medium, High, Critical }
```

---

## Infrastructure Components

### GuardianRegistry

Same pattern as `AgentRegistry` — loads YAML definitions from `guardians/` folder:

```csharp
public interface IGuardianRegistry
{
    /// <summary>Get all registered guardians.</summary>
    IReadOnlyList<IGuardian> GetAll();
    
    /// <summary>Get guardian by ID.</summary>
    IGuardian? Get(string guardianId);
    
    /// <summary>Reload guardian definitions (hot-reload).</summary>
    Task ReloadAsync(CancellationToken ct);
}
```

### GuardianScheduler

Background service that runs in the existing Aura service process:

```csharp
public class GuardianScheduler : BackgroundService
{
    // Responsibilities:
    // - Parse cron expressions from guardian definitions
    // - Maintain timer for each scheduled guardian
    // - Watch for file changes (debounced)
    // - Receive webhook events from HTTP endpoints
    // - Queue guardian checks to executor
}
```

### GuardianExecutor

Runs guardian checks and creates workflows:

```csharp
public interface IGuardianExecutor
{
    /// <summary>Run a guardian check and create workflows for violations.</summary>
    Task<GuardianRunResult> ExecuteAsync(
        string guardianId,
        GuardianContext context,
        CancellationToken ct);
}

public record GuardianRunResult(
    string GuardianId,
    DateTime RunTime,
    GuardianCheckResult CheckResult,
    IReadOnlyList<Workflow> CreatedWorkflows
);
```

### Workflow Source Tracking

Extend existing `Workflow` to track origin:

```csharp
public record Workflow
{
    // ... existing fields ...
    
    /// <summary>How this workflow was created.</summary>
    public WorkflowSource Source { get; init; }
    
    /// <summary>Guardian ID if created by guardian.</summary>
    public string? SourceGuardianId { get; init; }
    
    /// <summary>Priority (for UI sorting).</summary>
    public WorkflowPriority Priority { get; init; }
}

public enum WorkflowSource
{
    User,       // Created by human via UI/API
    Guardian,   // Created by guardian detection
    System      // Created by other automation
}
```

---

## API Endpoints

### Guardian API

```
GET  /api/guardians
     List all configured guardians

GET  /api/guardians/{id}
     Get guardian details and last run status

POST /api/guardians/{id}/run
     Trigger manual check, returns created workflows
     Body: { "scope": "full" | "incremental" }

GET  /api/guardians/{id}/history
     Get run history (checks, violations, workflows created)
```

### Workflow Extensions

Existing workflow endpoints, with guardian metadata:

```
GET /api/developer/workflows
    Now includes `source`, `sourceGuardianId`, `priority` fields
    Query param: ?source=guardian to filter guardian-created workflows

GET /api/developer/workflows/{id}
    Includes guardian context if workflow was guardian-created
```

---

## Configuration

### Repository Configuration

```yaml
# .aura/guardians.yaml
version: 1

# Which guardians to enable
enabled:
  - ci-guardian
  - test-coverage-guardian
  - documentation-guardian

# Guardian-specific overrides
config:
  ci-guardian:
    poll_interval: "*/15 * * * *"  # Every 15 min
    
  test-coverage-guardian:
    thresholds:
      overall: 70
      new_code: 80
    
  documentation-guardian:
    priority: low  # Don't surface these as urgent
```

---

## Implementation Phases

### Phase 1: Guardian Framework

- [ ] `IGuardian` interface and `GuardianRegistry` (hot-reload from `guardians/`)
- [ ] `GuardianScheduler` background service (cron + file watch triggers)
- [ ] `GuardianExecutor` (run check → create workflow)
- [ ] Extend `Workflow` with `Source`, `SourceGuardianId`, `Priority`
- [ ] API endpoints: list, get, run, history
- [ ] Extension UI: filter/group workflows by source

### Phase 2: First Guardians

- [ ] **CI Guardian** — Parse GitHub Actions / Azure Pipelines failures, create "fix build" workflows
- [ ] **Test Coverage Guardian** — Run coverage, detect regressions, create "write tests" workflows
- [ ] **Documentation Guardian** — Detect undocumented public APIs, create "document" workflows

### Phase 3: Detection Improvements

- [ ] Webhook receiver for real-time CI/CD events
- [ ] Smarter violation deduplication (don't create duplicate workflows)
- [ ] Guardian dashboard in extension (run status, last violations)

### Phase 4: Future Guardians

- [ ] Dependency Guardian (CVE scanning, outdated packages)
- [ ] API Compatibility Guardian (breaking change detection)
- [ ] Style/Lint Guardian (consistent formatting)
- [ ] Custom user-defined guardians (project-specific rules)

---

## Success Metrics

| Metric | Target |
|--------|--------|
| Workflow creation latency | <30 seconds from detection to workflow appearing |
| Coverage trend | Coverage doesn't regress quarter-over-quarter |
| Build break visibility | 100% of CI failures result in workflows |
| Chore completion rate | >50% of guardian workflows completed within 1 week |

---

## Open Questions

1. **Workflow deduplication** — If CI fails twice, do we create two workflows or update one?
2. **Guardian priorities** — Should users configure relative priority between guardians?
3. **Notification preferences** — Should guardians notify via extension only, or also email/Slack?
4. **Cross-repository guardians** — For monorepos, can a guardian span multiple workspaces?

---

## Related Documents

- [Workflow Spec](../completed/workflow-lifecycle.md) — Existing workflow infrastructure
- [Agent Registry](../../spec/01-agents.md) — Hot-reload pattern to follow
- [Background Jobs](../../adr/008-local-rag-foundation.md) — Existing background service pattern

