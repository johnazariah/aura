# Aura Orchestrator: Parallel Agent Dispatch via GH Copilot CLI

> **Status:** 📋 Upcoming
> **Created:** 2026-01-24
> **Author:** Aura Team
> **Priority:** High
> **Estimated Effort:** 2 weeks

## Summary

Transform Aura from a single-agent coding assistant into an **orchestrator** that dispatches parallelizable tasks to multiple GitHub Copilot CLI agents running in YOLO mode with Claude Opus.

Aura provides: story management, worktree isolation, task decomposition, MCP tools, quality gates, and merge ceremonies.

GH Copilot CLI provides: Claude Opus reasoning, YOLO autonomy, parallel execution.

---

## Problem Statement

Current Aura workflow limitations:
1. **Single-threaded** — One agent, one task at a time
2. **GPT-4o ceiling** — Good but not best-in-class
3. **Context exhaustion** — Long tasks fill the context window
4. **Slow iteration** — Manual step-by-step execution

Meanwhile, GH Copilot CLI offers:
- Claude Opus (best reasoning)
- YOLO mode (autonomous until done)
- Multiple terminals (parallel execution)
- MCP support (can use Aura's tools)

**Opportunity:** Combine Aura's orchestration strengths with GH CP CLI's execution power.

---

## Goals

1. **One-liner to done** — `aura story create "Add feature X"` → completed, tested, merged
2. **Parallel execution** — 3-5 agents working simultaneously on non-conflicting tasks
3. **Quality gates** — Build/test validation between phases
4. **Leverage Claude Opus** — Best-in-class model for actual coding
5. **Preserve Aura value** — MCP tools, story tracking, worktree isolation

## Non-Goals

1. Replacing Aura's MCP server (that's our moat)
2. Building our own Claude integration (use GH CP CLI)
3. Full autonomy without human oversight (gates remain)
4. Supporting non-GH Copilot CLI agents (for now)

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                         AURA ORCHESTRATOR                           │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐      │
│  │  Story   │───▶│ Worktree │───▶│   Task   │───▶│ Dispatch │      │
│  │  Create  │    │  Setup   │    │ Decompose│    │  Engine  │      │
│  └──────────┘    └──────────┘    └──────────┘    └────┬─────┘      │
│                                                       │             │
│                         ┌─────────────────────────────┼─────┐       │
│                         │                             │     │       │
│                         ▼                             ▼     ▼       │
│                  ┌─────────────┐              ┌─────────────┐       │
│                  │  Terminal 1 │              │  Terminal N │       │
│                  │  GH CP CLI  │    • • •     │  GH CP CLI  │       │
│                  │  (YOLO)     │              │  (YOLO)     │       │
│                  └──────┬──────┘              └──────┬──────┘       │
│                         │                           │               │
│                         └─────────────┬─────────────┘               │
│                                       ▼                             │
│                              ┌─────────────────┐                    │
│                              │    Monitor &    │                    │
│                              │  Quality Gate   │                    │
│                              └────────┬────────┘                    │
│                                       │                             │
│                                       ▼                             │
│                              ┌─────────────────┐                    │
│                              │   Integration   │                    │
│                              │   & Merge       │                    │
│                              └─────────────────┘                    │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Feature Specification

### Component 1: Story CLI

New CLI commands for story-driven orchestration.

#### `aura story create`

```powershell
aura story create "Add email validation to UserService per RFC 5322"
aura story create --from-issue https://github.com/org/repo/issues/123
aura story create --from-issue 123  # Uses current repo
```

**Behavior:**
1. Create story in database with title/description
2. Create git worktree at `c:\work\aura-worktrees\{slug}-{short-id}`
3. Enrich context via RAG search (find related code, patterns)
4. Output story ID and worktree path

**Output:**
```
✅ Story created: email-validation-a1b2c3
   Worktree: c:\work\aura-worktrees\email-validation-a1b2c3
   Branch: story/email-validation-a1b2c3

   Context found:
   - UserService.cs (validation patterns)
   - EmailValidator in external libs
   - Existing validation tests

   Next: aura story decompose
```

#### `aura story decompose`

```powershell
aura story decompose                    # Decompose current story
aura story decompose --story-id abc123  # Specific story
aura story decompose --output tasks.yaml
```

**Behavior:**
1. Analyze story context and description
2. Generate task breakdown with:
   - Task name and description
   - Files to create/modify
   - Dependencies (which tasks must complete first)
   - Parallel safety (can run with other tasks)
   - Suggested pattern (if applicable)
3. Store tasks in story record
4. Output task list

**Output:**
```yaml
story: email-validation-a1b2c3
tasks:
  - id: task-1
    name: "Create IEmailValidator interface"
    description: "Define interface with Validate(string email) method"
    files: [src/Services/IEmailValidator.cs]
    pattern: create-interface
    parallel_safe: true
    depends_on: []
    
  - id: task-2
    name: "Implement EmailValidator"
    description: "RFC 5322 compliant email validation"
    files: [src/Services/EmailValidator.cs]
    pattern: create-service
    parallel_safe: true
    depends_on: [task-1]
    
  - id: task-3
    name: "Add EmailValidator tests"
    description: "Unit tests for valid/invalid emails, edge cases"
    files: [tests/Services/EmailValidatorTests.cs]
    pattern: generate-tests
    parallel_safe: true
    depends_on: [task-2]
    
  - id: task-4
    name: "Integrate into UserService"
    description: "Wire EmailValidator into UserService.ValidateUser"
    files: [src/Services/UserService.cs]
    parallel_safe: false  # Touches shared file
    depends_on: [task-2]
```

#### `aura story run`

```powershell
aura story run                          # Execute all tasks
aura story run --parallel 3             # Max 3 parallel agents
aura story run --task task-2            # Run specific task
aura story run --dry-run                # Show what would happen
```

**Behavior:**
1. Resolve task dependencies (topological sort)
2. Identify parallelizable wave (tasks with satisfied deps, parallel_safe=true)
3. Dispatch wave to GH Copilot CLI agents
4. Monitor completion
5. Run quality gate (build/test)
6. Repeat for next wave
7. Final validation

#### `aura story status`

```powershell
aura story status
```

**Output:**
```
Story: email-validation-a1b2c3
Status: Running

Tasks:
  ✅ task-1: Create IEmailValidator interface (12s)
  ✅ task-2: Implement EmailValidator (45s)
  🔄 task-3: Add EmailValidator tests (running, 30s)
  ⏳ task-4: Integrate into UserService (waiting for task-2)

Quality Gates:
  ✅ Build after task-1
  ✅ Build after task-2
  ⏳ Tests after task-3

Agents: 1 running, 1 waiting
```

#### `aura story complete`

```powershell
aura story complete                     # Validate and prepare for merge
aura story complete --merge             # Also merge to main
aura story complete --cleanup           # Remove worktree after merge
```

---

### Component 2: Task Decomposition Engine

LLM-powered analysis to break stories into parallelizable tasks.

#### Input

```json
{
  "story": {
    "title": "Add email validation to UserService",
    "description": "Implement RFC 5322 email validation...",
    "enrichedContext": "Found: UserService.cs, ValidationHelper.cs..."
  },
  "codebaseInfo": {
    "projectType": "dotnet",
    "testFramework": "xunit",
    "existingPatterns": ["Service classes implement interfaces", "Tests in parallel folder"]
  }
}
```

#### Output

```json
{
  "tasks": [
    {
      "id": "task-1",
      "name": "Create IEmailValidator interface",
      "description": "...",
      "files": ["src/Services/IEmailValidator.cs"],
      "estimatedComplexity": "small",
      "parallelSafe": true,
      "dependsOn": [],
      "promptHints": "Use aura_generate(create_type, interface)..."
    }
  ],
  "waves": [
    {"wave": 1, "tasks": ["task-1"]},
    {"wave": 2, "tasks": ["task-2", "task-3"]},
    {"wave": 3, "tasks": ["task-4"]}
  ]
}
```

#### Decomposition Rules

1. **Interface before implementation** — Create contracts first
2. **Tests parallel with integration** — Tests can run once interface exists
3. **Shared files are sequential** — Tasks touching same file can't parallelize
4. **Small tasks preferred** — Easier for agents, faster feedback
5. **Pattern matching** — Suggest operational patterns when applicable

---

### Component 3: Dispatch Engine

Spawns and manages GH Copilot CLI processes.

#### Agent Configuration

```yaml
# .aura/agent-config.yaml
dispatch:
  maxParallel: 3
  defaultMode: yolo
  model: claude-sonnet  # or claude-opus
  timeout: 300  # seconds per task
  
mcp:
  enabled: true
  server: aura
  tools:
    - aura_search
    - aura_navigate
    - aura_inspect
    - aura_generate
    - aura_refactor
    - aura_validate
```

#### Prompt Template

```handlebars
You are completing a development task as part of a larger story.

## Task
{{task.name}}

## Description
{{task.description}}

## Files to Create/Modify
{{#each task.files}}
- {{this}}
{{/each}}

## Context
{{story.enrichedContext}}

## Available MCP Tools
You have access to Aura's MCP tools:
- `aura_search` - Semantic code search
- `aura_navigate` - Find callers, implementations, usages
- `aura_generate` - Create types, tests, implement interfaces
- `aura_refactor` - Rename, extract, change signatures

## Instructions
1. Use MCP tools to understand existing code patterns
2. Implement the required changes
3. Ensure code compiles (run `dotnet build` to verify)
4. Follow existing code conventions

{{#if task.pattern}}
## Pattern
Follow the `{{task.pattern}}` operational pattern.
{{/if}}

Complete this task. When done, the files should compile and follow project conventions.
```

#### Process Management

```csharp
public class AgentDispatcher
{
    public async Task<AgentResult> DispatchAsync(StoryTask task, CancellationToken ct)
    {
        var prompt = RenderPrompt(task);
        var workingDir = task.Story.WorktreePath;
        
        // Spawn GH Copilot CLI process
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "gh",
                Arguments = $"copilot agent --yolo --mcp aura",
                WorkingDirectory = workingDir,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            }
        };
        
        process.Start();
        await process.StandardInput.WriteLineAsync(prompt);
        
        // Monitor for completion
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync(ct);
        
        return new AgentResult
        {
            Success = process.ExitCode == 0,
            Output = output,
            Duration = stopwatch.Elapsed
        };
    }
}
```

---

### Component 4: Quality Gates

Automated validation between waves.

#### Gate: Build

```powershell
dotnet build -c Release --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    # Capture errors for retry context
    $errors = dotnet build 2>&1 | Select-String "error"
    return @{ Pass = $false; Errors = $errors }
}
return @{ Pass = $true }
```

#### Gate: Tests

```powershell
dotnet test --configuration Release --verbosity minimal
# Parse test results
```

#### Gate: Lint (Optional)

```powershell
dotnet format --verify-no-changes
```

#### Gate: Secrets

```powershell
# Check for accidentally committed secrets
.\scripts\hooks\pre-commit.ps1
```

#### Retry on Failure

If a gate fails, the dispatch engine:
1. Captures error context
2. Re-dispatches the failing task with error context appended
3. Retries up to 3 times
4. If still failing, pauses for human intervention

---

### Component 5: Integration & Merge

After all tasks complete successfully.

#### Conflict Detection

```powershell
# Check for uncommitted changes in shared files
$conflicts = git status --porcelain | Where-Object { $_ -match "^UU" }
if ($conflicts) {
    # Pause for human resolution
}
```

#### Commit Strategy

Option A: **One commit per task**
```powershell
foreach ($task in $completedTasks) {
    git add $task.Files
    git commit -m "$($task.Name)"
}
```

Option B: **Squash to single commit**
```powershell
git add -A
git commit -m "feat: $($story.Title)

Tasks completed:
$(($tasks | ForEach-Object { "- $($_.Name)" }) -join "`n")
"
```

#### Merge to Main

Follows the `aura.merge-worktree.prompt.md` ceremony:
1. Rebase onto latest main
2. Run full test suite
3. Push to main
4. Clean up worktree

---

## Data Model

### Story (Updated)

```csharp
public class Story
{
    // Existing fields...
    
    // New orchestration fields
    public string? TasksJson { get; set; }  // Serialized task list
    public OrchestratorStatus OrchestratorStatus { get; set; }
    public int CurrentWave { get; set; }
    public string? LastGateResult { get; set; }
}

public enum OrchestratorStatus
{
    Created,
    Decomposing,
    Dispatching,
    Running,
    GateFailed,
    Integrating,
    Completed,
    Failed
}
```

### StoryTask

```csharp
public record StoryTask
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required IReadOnlyList<string> Files { get; init; }
    public string? Pattern { get; init; }
    public bool ParallelSafe { get; init; }
    public IReadOnlyList<string> DependsOn { get; init; } = [];
    public TaskStatus Status { get; set; }
    public string? AgentOutput { get; set; }
    public TimeSpan? Duration { get; set; }
    public int Attempts { get; set; }
}

public enum TaskStatus
{
    Pending,
    Queued,
    Running,
    Completed,
    Failed,
    Skipped
}
```

---

## API Endpoints

### `POST /api/stories/{id}/decompose`

Trigger task decomposition for a story.

### `POST /api/stories/{id}/run`

Start orchestrated execution.

```json
{
  "maxParallel": 3,
  "dryRun": false,
  "taskFilter": ["task-1", "task-2"]  // Optional
}
```

### `GET /api/stories/{id}/status`

Get current orchestration status.

```json
{
  "storyId": "abc123",
  "status": "Running",
  "currentWave": 2,
  "tasks": [
    {"id": "task-1", "status": "Completed", "duration": "00:00:12"},
    {"id": "task-2", "status": "Running", "duration": "00:00:30"}
  ],
  "gates": [
    {"wave": 1, "type": "Build", "passed": true}
  ]
}
```

### `POST /api/stories/{id}/complete`

Finalize and optionally merge.

---

## CLI Implementation

### PowerShell Module

```powershell
# scripts/Aura-Orchestrator.psm1

function New-AuraStory {
    param(
        [Parameter(Mandatory)]
        [string]$Title,
        
        [string]$FromIssue
    )
    
    $body = @{ title = $Title; issueUrl = $FromIssue } | ConvertTo-Json
    $response = Invoke-RestMethod -Uri "http://localhost:5300/api/developer/stories" `
        -Method Post -Body $body -ContentType "application/json"
    
    # Create worktree
    $worktreePath = "c:\work\aura-worktrees\$($response.id)"
    git worktree add $worktreePath -b "story/$($response.id)"
    
    Write-Host "✅ Story created: $($response.id)"
    Write-Host "   Worktree: $worktreePath"
    
    return $response
}

function Start-AuraStoryRun {
    param(
        [string]$StoryId,
        [int]$MaxParallel = 3
    )
    
    # Get tasks
    $story = Invoke-RestMethod "http://localhost:5300/api/developer/stories/$StoryId"
    $tasks = $story.tasks | ConvertFrom-Json
    
    # Execute waves
    foreach ($wave in (Group-TasksByWave $tasks)) {
        Write-Host "🌊 Wave $($wave.Number): $($wave.Tasks.Count) tasks"
        
        # Dispatch parallel tasks
        $jobs = $wave.Tasks | ForEach-Object {
            Start-Job -ScriptBlock {
                param($task, $worktree)
                Set-Location $worktree
                $prompt = Get-TaskPrompt $task
                gh copilot agent --yolo --mcp aura $prompt
            } -ArgumentList $_, $story.worktreePath
        }
        
        # Wait for completion
        $jobs | Wait-Job | Receive-Job
        
        # Quality gate
        Push-Location $story.worktreePath
        dotnet build
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Build failed after wave $($wave.Number)"
            return
        }
        Pop-Location
    }
    
    Write-Host "✅ All tasks completed"
}
```

---

## Acceptance Criteria

### MVP (Week 1)

- [ ] `aura story create` creates story + worktree
- [ ] `aura story decompose` generates task list (single file initially)
- [ ] `aura story run` dispatches to 1 GH CP CLI agent serially
- [ ] Build gate runs after each task
- [ ] `aura story status` shows progress
- [ ] `aura story complete` validates and outputs summary

### Parallel Execution (Week 2)

- [ ] Task dependency resolution works correctly
- [ ] Multiple agents dispatch in parallel (configurable max)
- [ ] Wave-based execution with gates between waves
- [ ] Failed tasks retry with error context
- [ ] `aura story complete --merge` merges to main

### Polish (Week 2+)

- [ ] Progress UI in terminal (live updates)
- [ ] Integration with VS Code extension (status in sidebar)
- [ ] Automatic pattern detection and injection
- [ ] Conflict detection and resolution hints

---

## Testing Strategy

### Unit Tests

- Task decomposition logic (dependency resolution)
- Wave calculation
- Prompt generation

### Integration Tests

- Story CLI commands
- API endpoints
- Worktree creation/cleanup

### E2E Tests (Manual Initially)

- Create story from issue
- Decompose into tasks
- Run with parallel dispatch
- Validate quality gates
- Complete and merge

---

## Rollout Plan

### Phase 1: Internal Dogfooding

Use on Aura's own development:
- Pick a medium-sized feature
- Create story, decompose, run
- Document friction points
- Iterate

### Phase 2: Documentation

- Update getting-started guide
- Add orchestrator tutorial
- Record demo video

### Phase 3: Release

- Include in next minor release
- Announce capability
- Gather feedback

---

## Dependencies

- **GH Copilot CLI** with YOLO mode and MCP support
- **Aura MCP server** running and accessible
- **Git worktree** support (already implemented)
- **PowerShell** (Windows) or **Bash** (Mac/Linux) for CLI

---

## Open Questions

1. **Terminal spawning** — Best way to manage multiple terminal processes?
   - PowerShell jobs
   - Direct Process spawning
   - tmux/screen on Mac/Linux

2. **YOLO completion** — How to detect when agent is "done"?
   - Process exit
   - File change monitoring
   - Agent output parsing

3. **MCP authentication** — How does GH CP CLI auth to Aura MCP?
   - Local socket (no auth needed)
   - Bearer token
   - mTLS

4. **Cost tracking** — Should we track token usage per task?
   - Nice to have, not MVP

---

## References

- [GH Copilot CLI Documentation](https://docs.github.com/en/copilot/github-copilot-in-the-cli)
- [MCP Specification](https://modelcontextprotocol.io/)
- [Aura Merge Worktree Prompt](.github/prompts/aura.merge-worktree.prompt.md)
- [Agentic Execution v2 Spec](.project/features/completed/agentic-execution-v2.md)
