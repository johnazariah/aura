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

## Reasoning Layer: Dual-Mode Execution

Workflows can execute in two modes. The mode is selected per-workflow based on user preference or workflow type.

### Mode Selection

```csharp
public enum WorkflowMode
{
    /// <summary>
    /// Aura agents execute steps via ReAct loop with local/cloud LLM.
    /// Plan → Steps → Execute → Review
    /// </summary>
    Structured,
    
    /// <summary>
    /// Hand off to GitHub Copilot for free-form conversation in worktree.
    /// Tools available via MCP, expertise injected as context.
    /// </summary>
    Conversational,
}
```

### When to Use Each Mode

| Scenario | Recommended Mode | Why |
|----------|------------------|-----|
| Batch doc generation | Structured | Predictable, no interaction needed |
| Privacy-sensitive code | Structured | Local LLM, data stays on-prem |
| CI/CD automation | Structured | Needs to run unattended |
| Learning a new codebase | Conversational | Interactive exploration |
| Complex multi-file refactor | Conversational | Benefits from reasoning power |
| Debugging a tricky issue | Conversational | Back-and-forth dialogue |
| Offline/air-gapped | Structured | No cloud dependency |

### Workflow Schema Extension

```csharp
public record Workflow
{
    // ... existing fields ...
    
    /// <summary>How this workflow was created.</summary>
    public WorkflowSource Source { get; init; }
    
    /// <summary>Guardian ID if created by guardian.</summary>
    public string? SourceGuardianId { get; init; }
    
    /// <summary>Priority for UI sorting.</summary>
    public WorkflowPriority Priority { get; init; }
    
    /// <summary>Execution mode.</summary>
    public WorkflowMode Mode { get; init; } = WorkflowMode.Structured;
    
    /// <summary>Suggested specialist for this workflow.</summary>
    public string? SuggestedSpecialist { get; init; }
}
```

### Mode Handoff

**Structured Mode:**
```
Workflow created
    │
    ▼
WorkflowService.PlanAsync() ─► LLM generates steps
    │
    ▼
For each step:
    AgentRegistry.GetAgent(step.Capability)
    Agent.ExecuteAsync() ─► ReAct loop with tools
    │
    ▼
Human reviews, approves
```

**Conversational Mode:**
```
Workflow created
    │
    ▼
Extension opens worktree in new VS Code window
    │
    ▼
Injects specialist context into Copilot (via MCP resource)
    │
    ▼
User chats with Copilot
    Copilot calls aura_* tools via MCP
    │
    ▼
User marks workflow complete when done
```

---

## Expertise Layer: Specialists

Specialists provide domain knowledge that informs reasoning. The same YAML config serves both modes.

### Specialist Definition

```yaml
# agents/languages/rust.yaml
id: rust-specialist
name: Rust Language Specialist
version: 1

# === Used in Structured Mode (as agent) ===
capabilities:
  - software-development-rust

system_prompt: |
  You are an expert Rust developer. Follow these conventions:
  - Use `thiserror` for custom error types
  - Prefer `anyhow::Result` for application code
  - Use `#[derive(Debug, Clone)]` by default
  - Prefer `&str` over `String` in function parameters
  - Use `clippy` lints at warn level

tools_available:
  - aura_search
  - aura_navigate
  - aura_refactor
  - aura_validate

# === Used in Conversational Mode (as context) ===
copilot_context:
  # Injected into Copilot's system prompt when working on .rs files
  conventions: |
    Rust conventions for this project:
    - Error handling: `thiserror` for libraries, `anyhow` for apps
    - Derive Debug and Clone on all public types
    - Prefer borrowing over ownership in function signatures
    - Run `cargo clippy` before committing
    
  # Relevant docs to surface
  related_docs:
    - docs/rust-style-guide.md
    - CONTRIBUTING.md#rust-guidelines
```

### How Context Injection Works (Conversational Mode)

When a workflow opens in Conversational mode:

1. Extension detects primary language(s) in worktree
2. Loads matching specialists from `agents/languages/`
3. Registers an MCP **resource** with the specialist context
4. Copilot sees the context when it lists resources

```typescript
// Extension: Register specialist context as MCP resource
mcp.registerResource({
  uri: `aura://specialist/${workflowId}`,
  name: "Aura Specialist Context",
  description: "Project conventions and patterns",
  contents: buildSpecialistContext(workflow, detectedLanguages)
});
```

### How Agent Loading Works (Structured Mode)

When a workflow step executes:

1. `AgentRegistry.GetAgent(capability)` finds matching agent
2. Agent's `system_prompt` loaded from YAML
3. `tools_available` determines which tools are injected
4. ReAct loop executes with those tools

```csharp
// Existing agent loading path
var agent = _agentRegistry.GetByCapability("software-development-rust");
var result = await agent.ExecuteAsync(context, tools, cancellationToken);
```

---

## Orchestration Layer: Guardians

Guardians are background sensors that detect issues and create workflows.

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

## Current State vs Target

### What We Have Today

| Layer | Current State | Notes |
|-------|---------------|-------|
| **Orchestration** | ❌ None | No guardians, no automated detection |
| **Reasoning** | ⚠️ Partial | Aura agents work; Copilot works via MCP; but no mode selection |
| **Expertise** | ⚠️ Partial | Language specialists exist as agents; no context injection for Copilot |
| **Capabilities** | ✅ Complete | 8 meta-tools via MCP, Roslyn adapter, rope adapter |

### What We're Building

```
Current: User creates workflow → Aura agents execute
Target:  Guardians detect     → User chooses mode → Aura OR Copilot executes
```

---

## Implementation Phases

### Phase 1: Workflow Mode Infrastructure

**Goal:** Enable dual-mode execution on existing workflows.

- [ ] Add `Mode` field to `Workflow` entity (Structured | Conversational)
- [ ] Add `SuggestedSpecialist` field to `Workflow`
- [ ] Extension: Mode selector when creating/opening workflow
- [ ] Extension: "Open in Copilot" action for Conversational mode
- [ ] API: Accept `mode` in workflow creation

**Milestone:** User can create workflow and choose execution mode.

### Phase 2: Specialist Context Injection

**Goal:** Specialists inform Copilot in Conversational mode.

- [ ] Extend specialist YAML schema with `copilot_context` section
- [ ] `SpecialistContextService` — Load and merge specialist context
- [ ] MCP resource provider — Expose specialist context to Copilot
- [ ] Extension: Detect languages in worktree, load matching specialists
- [ ] Test with existing language specialists (C#, Python, Rust)

**Milestone:** Copilot receives project conventions when working in worktree.

### Phase 3: Guardian Framework

**Goal:** Background detection creates workflows automatically.

- [ ] `IGuardian` interface and `GuardianRegistry` (hot-reload from `guardians/`)
- [ ] `GuardianScheduler` background service (cron + file watch triggers)
- [ ] `GuardianExecutor` (run check → create workflow)
- [ ] Extend `Workflow` with `Source`, `SourceGuardianId`, `Priority`
- [ ] API endpoints: list, get, run, history
- [ ] Extension UI: filter/group workflows by source, priority badge

**Milestone:** Guardians can be defined in YAML and triggered on schedule.

### Phase 4: First Guardians

**Goal:** Prove the model with real detection.

- [ ] **CI Guardian** — Parse GitHub Actions / Azure Pipelines failures
- [ ] **Test Coverage Guardian** — Run coverage, detect regressions
- [ ] **Documentation Guardian** — Detect undocumented public APIs

**Milestone:** Maintenance chores appear automatically in workflow list.

### Phase 5: Production Hardening

**Goal:** Make it reliable for real use.

- [ ] Webhook receiver for real-time CI/CD events
- [ ] Workflow deduplication (don't create duplicates for same issue)
- [ ] Guardian dashboard in extension (status, history, manual trigger)
- [ ] User configuration for default mode per guardian type
- [ ] Notification preferences

**Milestone:** Guardians run reliably in production.

### Phase 6: Future Guardians

**Goal:** Expand detection capabilities.

- [ ] Dependency Guardian (CVE scanning, outdated packages)
- [ ] API Compatibility Guardian (breaking change detection)
- [ ] Style/Lint Guardian (consistent formatting)
- [ ] Custom user-defined guardians (project-specific rules)

---

## Migration Path

### Existing Components → New Architecture

| Existing | Becomes | Changes Needed |
|----------|---------|----------------|
| `agents/languages/*.yaml` | Expertise Layer (dual-use) | Add `copilot_context` section |
| `AgentRegistry` | Unchanged (Structured mode) | None |
| MCP Tools | Unchanged (Capabilities layer) | None |
| `Workflow` entity | Extended | Add `Mode`, `Source`, `Priority` |
| Workflow UI | Extended | Add mode selector, source filter |

### Breaking Changes

**None.** This is additive:
- Existing workflows default to `Mode=Structured`
- Existing agents continue to work
- Guardians are opt-in via `guardians/` folder

---

## Success Metrics

| Metric | Target |
|--------|--------|
| Mode adoption | >30% of workflows use Conversational mode |
| Specialist coverage | >80% of indexed languages have specialists |
| Workflow creation latency | <30 seconds from detection to UI |
| Build break visibility | 100% of CI failures result in workflows |
| Chore completion rate | >50% of guardian workflows completed within 1 week |

---

## Open Questions

1. **Mode recommendation** — Should guardians recommend a mode based on issue type?
2. **Workflow deduplication** — If CI fails twice, create two workflows or update one?
3. **Specialist conflicts** — Multiple specialists for same file (e.g., TypeScript + React)?
4. **Cross-repository guardians** — For monorepos, can a guardian span multiple workspaces?
5. **Offline Copilot** — What happens in Conversational mode without internet?

---

## Related Documents

- [MCP Tools Enhancement](completed/mcp-tools-enhancement.md) — Capabilities layer
- [Story Model](completed/story-model.md) — WorkflowMode definition
- [Agent Registry](../../spec/01-agents.md) — Hot-reload pattern
- [Language Specialists](completed/generic-language-agent.md) — Current specialist implementation

