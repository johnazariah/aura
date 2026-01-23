# Aura as Orchestrator: GH Copilot CLI Integration

> **Status:** 📋 Unplanned (Exploratory)
> **Created:** 2026-01-24
> **Author:** Aura Team
> **Category:** Architecture / Strategy

## The Insight

Aura was built when we had one chat window. Now:
- **GitHub Copilot CLI** can run agents in YOLO mode with Claude Opus
- **Multiple terminals** can run agents in parallel
- **MCP** provides a standard protocol for tool sharing

Aura's coding agents are slower (GPT-4o) and single-threaded. But Aura has unique strengths:
- **Story management** — Create from GitHub issues, track progress
- **Worktree isolation** — Each story gets a clean git worktree
- **MCP server** — Powerful Roslyn/TreeSitter tools for C#, TS, Python
- **Pattern catalog** — Step-by-step operational procedures
- **Quality gates** — Build, test, validation ceremonies

**The play:** Turn Aura into an **orchestrator** that dispatches work to GH Copilot CLI agents.

---

## Architecture Vision

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         AURA ORCHESTRATOR                               │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  GitHub Issue ──► Story ──► Worktree ──► Task Decomposition            │
│                                              │                          │
│                           ┌──────────────────┼──────────────────┐       │
│                           │                  │                  │       │
│                           ▼                  ▼                  ▼       │
│                    ┌───────────┐      ┌───────────┐      ┌───────────┐  │
│                    │ GH CP CLI │      │ GH CP CLI │      │ GH CP CLI │  │
│                    │ Terminal 1│      │ Terminal 2│      │ Terminal 3│  │
│                    │  (YOLO)   │      │  (YOLO)   │      │  (YOLO)   │  │
│                    └─────┬─────┘      └─────┬─────┘      └─────┬─────┘  │
│                          │                  │                  │        │
│                          └──────────────────┼──────────────────┘        │
│                                             │                           │
│                                             ▼                           │
│                                    ┌─────────────────┐                  │
│                                    │ Integration &   │                  │
│                                    │ Validation      │                  │
│                                    │ (Build/Test)    │                  │
│                                    └─────────────────┘                  │
│                                             │                           │
│                                             ▼                           │
│                                    Merge to Main                        │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## What Aura Provides (Unique Value)

| Capability | How GH CP CLI Uses It |
|------------|----------------------|
| **Story from Issue** | One-line problem → enriched context |
| **Worktree Isolation** | Each story gets clean workspace |
| **MCP Server** | `aura_navigate`, `aura_inspect`, `aura_generate`, `aura_refactor` |
| **Task Decomposition** | Break story into parallelizable tasks |
| **Pattern Injection** | Load operational patterns for complex tasks |
| **Quality Gates** | Build, test, validate after each agent completes |
| **Integration** | Combine outputs, resolve conflicts |
| **Merge Ceremony** | Safe merge back to main |

## What GH Copilot CLI Provides

| Capability | Advantage |
|------------|-----------|
| **Claude Opus** | Best-in-class reasoning |
| **YOLO Mode** | Autonomous until done |
| **Parallel Execution** | Multiple terminals |
| **MCP Client** | Can use Aura's tools |
| **Fast Iteration** | Tight feedback loops |

---

## Workflow: One-Line to Done

### Step 1: Story Creation (Aura)

```powershell
# User provides one-liner
aura story create "Add email validation to UserService per RFC 5322"

# Aura:
# 1. Creates worktree: c:\work\aura-worktrees\email-validation-abc123
# 2. Enriches with codebase context (RAG search for UserService, validation patterns)
# 3. Generates task breakdown
```

### Step 2: Task Decomposition (Aura)

Aura analyzes the story and creates **parallelizable tasks**:

```yaml
story: email-validation
worktree: c:\work\aura-worktrees\email-validation-abc123
tasks:
  - id: impl
    name: "Implement EmailValidator class"
    files: [src/Services/EmailValidator.cs]
    pattern: create-service
    parallel_safe: true
    
  - id: tests
    name: "Add unit tests for EmailValidator"
    files: [tests/Services/EmailValidatorTests.cs]
    pattern: generate-tests
    depends_on: [impl]  # Can start after impl creates interface
    parallel_safe: true
    
  - id: integrate
    name: "Wire EmailValidator into UserService"
    files: [src/Services/UserService.cs]
    depends_on: [impl]
    parallel_safe: false  # Touches shared file
```

### Step 3: Parallel Dispatch (Aura → GH CP CLI)

Aura spawns GH Copilot CLI terminals for parallel-safe tasks:

```powershell
# Terminal 1: Implementation
cd $worktree
gh copilot agent --yolo --mcp aura "
  Implement EmailValidator class.
  Use aura_navigate to find existing validation patterns.
  Use aura_generate to create the class.
  File: src/Services/EmailValidator.cs
"

# Terminal 2: Tests (can start once interface exists)
gh copilot agent --yolo --mcp aura "
  Generate comprehensive tests for EmailValidator.
  Use aura_generate(operation: tests, target: EmailValidator).
  File: tests/Services/EmailValidatorTests.cs
"
```

### Step 4: Monitor & Gate (Aura)

Aura watches for task completion:

```powershell
# Poll for file changes or terminal exit
while ($runningTasks) {
    foreach ($task in $runningTasks) {
        if (Test-TaskComplete $task) {
            # Run quality gate
            dotnet build
            if ($LASTEXITCODE -eq 0) {
                Mark-TaskComplete $task
            } else {
                # Feed errors back to agent for retry
                Send-ErrorContext $task (Get-BuildErrors)
            }
        }
    }
    Start-Sleep -Seconds 5
}
```

### Step 5: Integration (Aura)

After parallel tasks complete, handle sequential/conflicting tasks:

```powershell
# Now safe to integrate into UserService
gh copilot agent --yolo --mcp aura "
  Wire EmailValidator into UserService.ValidateUser method.
  Use aura_navigate(callers, ValidateUser) to understand usage.
  Use aura_refactor if signature changes needed.
"
```

### Step 6: Final Validation (Aura)

```powershell
# Full quality gate
dotnet build -c Release
dotnet test --configuration Release
.\scripts\Build-Extension.ps1 -SkipInstall  # If extension touched

# All pass? Ready for merge ceremony
aura story complete --merge-to-main
```

---

## MCP Integration

GH Copilot CLI can use Aura's MCP server for powerful codebase operations:

```json
// .github/copilot/mcp.json
{
  "servers": {
    "aura": {
      "command": "dotnet",
      "args": ["run", "--project", "src/Aura.Api"],
      "env": {
        "AURA_MCP_MODE": "true"
      }
    }
  }
}
```

Tools available to GH Copilot CLI:
- `aura_search` — Semantic code search
- `aura_navigate` — Find callers, implementations, usages
- `aura_inspect` — Explore type structure
- `aura_generate` — Create types, tests, implement interfaces
- `aura_refactor` — Rename, extract, change signatures
- `aura_validate` — Build and test

---

## Implementation Plan

### Phase 1: CLI Enhancements (1-2 days)

1. **`aura story create`** — Create story + worktree from one-liner
   ```powershell
   aura story create "Add feature X" --from-issue 123
   ```

2. **`aura story decompose`** — Generate parallelizable task list
   ```powershell
   aura story decompose --output tasks.yaml
   ```

3. **`aura story status`** — Check task completion status
   ```powershell
   aura story status
   # impl: ✅ complete, tests: 🔄 running, integrate: ⏳ waiting
   ```

### Phase 2: GH Copilot CLI Integration (2-3 days)

1. **Task dispatcher** — Spawn GH CP CLI terminals per task
2. **Prompt generator** — Create task-specific prompts with context
3. **Completion detector** — Watch for task completion (file changes, exit codes)
4. **Error feedback** — Pipe build/test errors back to running agents

### Phase 3: Orchestration Loop (3-5 days)

1. **Dependency resolution** — Start tasks when dependencies complete
2. **Conflict detection** — Identify file conflicts from parallel edits
3. **Merge strategy** — Combine parallel outputs safely
4. **Retry logic** — Re-dispatch failed tasks with error context

### Phase 4: Quality & Ceremony (1-2 days)

1. **Quality gates** — Build, test, lint after each task
2. **Final validation** — Full test suite before merge
3. **Merge ceremony** — Automated merge-worktree flow
4. **Story completion** — Update GitHub issue, close story

---

## Key Design Decisions

### Q: Why not just use GH Copilot CLI directly?

**A:** You can! But you lose:
- Story tracking and GitHub issue integration
- Worktree isolation (parallel work on multiple features)
- Parallelizable task breakdown
- Quality gates between tasks
- MCP tools (Roslyn-powered navigation, refactoring)
- Merge ceremony with validation

### Q: Why not improve Aura's own agents?

**A:** We'd be chasing a moving target:
- GH Copilot CLI has Anthropic's best models
- Microsoft/GitHub are investing heavily in agent UX
- Aura's value is in **orchestration + context**, not raw coding

### Q: What about token costs?

**A:** Each GH CP CLI agent uses its own context. Parallel execution means:
- 3 agents × 100K tokens = 300K tokens
- But tasks complete 3× faster
- And context doesn't get polluted (fresh start per task)

### Q: What if parallel edits conflict?

**A:** Task decomposition should minimize conflicts:
1. **File-based parallelism** — Different tasks touch different files
2. **Dependency ordering** — Sequential tasks wait for dependencies
3. **Conflict resolution** — Aura detects and prompts for manual merge

---

## Success Metrics

| Metric | Current (Aura-only) | Target (Aura + GH CP CLI) |
|--------|---------------------|---------------------------|
| Time to complete medium feature | 30-60 min | 10-15 min |
| Agent iterations to success | 5-10 | 2-3 (parallel) |
| Context exhaustion issues | Common | Rare (fresh contexts) |
| Model quality | GPT-4o | Claude Opus |
| Parallel execution | No | Yes (3-5 agents) |

---

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| GH CP CLI API changes | Thin wrapper, easy to update |
| Parallel conflicts | Conservative task decomposition |
| Lost context between agents | MCP provides shared code knowledge |
| Cost explosion | Token budgets per task, monitoring |
| GH CP CLI rate limits | Backoff and retry logic |

---

## Open Questions

1. **Terminal management** — How to spawn/monitor multiple terminals programmatically?
2. **YOLO exit detection** — How to know when agent is "done"?
3. **Prompt format** — What's the optimal prompt structure for GH CP CLI tasks?
4. **MCP auth** — How does GH CP CLI authenticate to Aura's MCP server?
5. **VS Code integration** — Should this run from extension or pure CLI?

---

## Next Steps

1. **Prototype** — Manual test of the flow:
   - Create worktree manually
   - Run 2-3 GH CP CLI terminals with Aura MCP
   - Observe parallel execution
   - Manually validate and merge

2. **CLI scaffolding** — Build `aura story` CLI commands

3. **Task decomposition** — Train/prompt for parallelizable breakdown

4. **Dispatcher MVP** — Simple PowerShell orchestrator

5. **Iterate** — Learn from real usage, refine
