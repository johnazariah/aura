# Research: Aura Integration Points for Anvil

## Date
2026-01-30

## Executive Summary

Anvil is a test harness CLI that validates Aura's agent quality by running curated stories against test repositories and verifying the generated code compiles and tests pass. The integration is straightforward: Anvil calls Aura's HTTP API at `localhost:5300` to create stories, execute steps, and monitor progress. Key technical approach is delegating all code generation, workspace management, and build/test execution to Aura—Anvil only orchestrates the test scenarios and collects results.

## Challenge Summary

Build a C# CLI application that:
1. Loads test scenarios (YAML-defined stories with expected outcomes)
2. Provisions test repositories (via git submodules)
3. Calls Aura's Story API to create and execute each story
4. Validates that Aura-generated code compiles and passes tests
5. Produces human-readable and machine-parseable reports
6. Runs sequentially without retry logic (failures indicate Aura bugs)

## Acceptance Criteria

- [ ] CLI can load story definitions from YAML files
- [ ] CLI can run a single story against Aura
- [ ] CLI can run all stories in a suite
- [ ] CLI produces console output with pass/fail status
- [ ] CLI produces JSON report with detailed results
- [ ] CLI returns non-zero exit code on any failure
- [ ] Test repositories are git submodules, not embedded
- [ ] Aura manages worktrees; Anvil never creates them directly

---

## Aura API Analysis

### Base URL
```
http://localhost:5300
```

### Health Check
```http
GET /health
```

### Story Lifecycle Endpoints

| Operation | Method | Endpoint | Purpose |
|-----------|--------|----------|---------|
| Create | POST | `/api/developer/stories` | Create story with title, description, repositoryPath |
| Get | GET | `/api/developer/stories/{id}` | Get story with all steps |
| Get by Path | GET | `/api/developer/stories/by-path?path={worktreePath}` | Find story by worktree |
| Analyze | POST | `/api/developer/stories/{id}/analyze` | Enrich with codebase context |
| Plan | POST | `/api/developer/stories/{id}/plan` | Generate execution steps |
| Run | POST | `/api/developer/stories/{id}/run` | Execute orchestrator (runs all steps) |
| Stream | GET | `/api/developer/stories/{id}/stream` | SSE stream for execution progress |
| Execute Step | POST | `/api/developer/stories/{storyId}/steps/{stepId}/execute` | Run single step |
| Complete | POST | `/api/developer/stories/{id}/complete` | Mark story complete |
| Cancel | POST | `/api/developer/stories/{id}/cancel` | Cancel in-progress story |
| Delete | DELETE | `/api/developer/stories/{id}` | Remove story and worktree |

### Create Story Request

```json
{
  "title": "Add validation to User model",
  "description": "Implement email validation with regex pattern...",
  "repositoryPath": "c:\\work\\test-repos\\my-csharp-project",
  "automationMode": "Autonomous"  // or "Assisted"
}
```

### Create Story Response

```json
{
  "id": "guid",
  "title": "...",
  "description": "...",
  "status": "Created",
  "automationMode": "Autonomous",
  "gitBranch": "feature/workflow-{guid}",
  "worktreePath": "/workspaces/my-csharp-project-wt-{guid}",
  "repositoryPath": "c:\\work\\test-repos\\my-csharp-project",
  "createdAt": "2026-01-30T..."
}
```

### Story Status Flow

```
Created → Analyzed → Planned → Executing → Completed
                                    ↓
                                 Failed
                                    ↓
                                Cancelled
```

### Get Story Response (with steps)

```json
{
  "id": "guid",
  "title": "...",
  "status": "Planned",
  "steps": [
    {
      "id": "guid",
      "order": 1,
      "wave": 0,
      "name": "Implement email validation",
      "capability": "csharp-coding",
      "language": "csharp",
      "description": "Add EmailAttribute and validation logic...",
      "status": "Pending",
      "assignedAgentId": "roslyn-coding-agent",
      "output": null,
      "error": null
    }
  ],
  "currentWave": 0,
  "waveCount": 1
}
```

### Step Status Values

| Status | Meaning |
|--------|---------|
| Pending | Not yet executed |
| Running | Currently executing |
| Completed | Finished successfully |
| Failed | Execution failed |
| Skipped | User skipped this step |
| Approved | Output approved by user |
| Rejected | Output rejected, needs rework |

---

## Language Definitions

Aura has language-specific configurations in `agents/languages/*.yaml` that define:
- Build commands (e.g., `dotnet build`)
- Test commands (e.g., `dotnet test`, `pytest`)
- Lint/format commands
- Agent selection (e.g., C# → RoslynCodingAgent)

### Available Languages

| Language | File | Build Command | Test Command |
|----------|------|---------------|--------------|
| C# | csharp.yaml | `dotnet build` | `dotnet test` |
| Python | python.yaml | N/A | `pytest -v` |
| TypeScript | typescript.yaml | `npm run build` | `npm test` |
| Go | go.yaml | `go build ./...` | `go test ./...` |
| Rust | rust.yaml | `cargo build` | `cargo test` |
| F# | fsharp.yaml | `dotnet build` | `dotnet test` |
| PowerShell | powershell.yaml | N/A | `Invoke-Pester` |
| Bash | bash.yaml | N/A | `bats` |
| Terraform | terraform.yaml | `terraform validate` | N/A |
| Bicep | bicep.yaml | `bicep build` | N/A |
| Elm | elm.yaml | `elm make` | `elm-test` |
| Haskell | haskell.yaml | `stack build` | `stack test` |
| ARM | arm.yaml | N/A | N/A |

### Implication for Anvil

Anvil story definitions should specify `language` and let Aura handle build/test. Anvil only needs to:
1. Call the API
2. Check if the story completed successfully
3. Optionally verify build/test exit codes from step output

---

## Worktree Management

### Aura Responsibility (per ADR-010)

Aura creates and manages git worktrees automatically:
- On story creation: Creates `feature/workflow-{id}` branch and worktree
- Worktree path: `{workspacesRoot}/{repoName}-wt-{workflowId}`
- On story delete: Removes worktree and branch

### Worktree Service Interface (from git-worktrees.md)

```csharp
public interface IWorktreeService
{
    Task<WorktreeInfo> CreateAsync(string mainRepoPath, Guid workflowId, string? baseBranch = null, CancellationToken ct = default);
    Task<WorktreeInfo?> GetAsync(Guid workflowId, CancellationToken ct = default);
    Task<IReadOnlyList<WorktreeInfo>> ListAsync(string mainRepoPath, CancellationToken ct = default);
    Task RemoveAsync(Guid workflowId, bool force = false, CancellationToken ct = default);
    Task<string> CommitAsync(Guid workflowId, string message, CancellationToken ct = default);
    Task PushAsync(Guid workflowId, CancellationToken ct = default);
}

public record WorktreeInfo(
    Guid WorkflowId,
    string WorktreePath,
    string BranchName,
    string MainRepoPath,
    bool IsClean,
    DateTime CreatedAt
);
```

### Anvil Implication

- Anvil passes `repositoryPath` pointing to the test repo (git submodule)
- Aura creates worktree and returns `worktreePath` in response
- Anvil can use `worktreePath` to verify file changes if needed
- Anvil calls `DELETE /api/developer/stories/{id}` for cleanup

---

## Story Model Details

### Workflow Entity (from story-model.md)

```csharp
public sealed class Workflow  // "Story" in UI
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? RepositoryPath { get; set; }
    public WorkflowStatus Status { get; set; }
    public string? WorktreePath { get; set; }
    public string? GitBranch { get; set; }
    public string? AnalyzedContext { get; set; }
    public string? ExecutionPlan { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    
    // Wave-based execution
    public int CurrentWave { get; set; }
    public int MaxParallelism { get; set; }
    
    // GitHub integration (not used by Anvil)
    public string? IssueUrl { get; set; }
    public IssueProvider? IssueProvider { get; set; }
    
    public ICollection<WorkflowStep> Steps { get; set; }
}
```

### Step Entity

```csharp
public sealed class WorkflowStep
{
    public Guid Id { get; set; }
    public Guid WorkflowId { get; set; }
    
    public int Order { get; set; }
    public int Wave { get; set; }  // For parallel execution
    public required string Name { get; set; }
    public required string Capability { get; set; }
    public string? Language { get; set; }
    public string? Description { get; set; }
    
    public StepStatus Status { get; set; }
    public string? AssignedAgentId { get; set; }
    public int Attempts { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
```

---

## Constraints Discovered

| Source | Constraint | Implication |
|--------|------------|-------------|
| ADR-001 | C# / .NET 10 | Use modern C# features, nullable refs |
| ADR-002 | xUnit + FluentAssertions + NSubstitute | Test framework choices |
| ADR-003 | Spectre.Console CLI | Use Spectre for output formatting |
| ADR-004 | Clean Architecture | Services, Repositories, Adapters |
| ADR-005 | Single project to start | Begin with Anvil.Cli only |
| ADR-007 | Test docs standard | Given-When-Then naming |
| ADR-010 | Delegate worktrees to Aura | Never call `git worktree` directly |
| ADR-011 | Sequential execution | No parallelism in v1 |
| ADR-012 | No retry | Failures are bugs to fix |
| ADR-013 | Console + JSON reports | No JUnit/CI formats |
| ADR-014 | Shell out to git | Use CLI, not libgit2 |
| ADR-015 | Delegate build/test to Aura | Stories specify language only |

---

## Architecture Implications

### Layers Affected

- **CLI Layer**: Commands for `anvil run`, `anvil list`, `anvil validate`
- **Service Layer**: `StoryRunner`, `ReportGenerator`, `ScenarioLoader`
- **Adapter Layer**: `AuraApiClient` (HttpClient wrapper)
- **Data Layer**: `ScenarioRepository` (reads YAML files)

### New Interfaces Required

```csharp
// Aura API interaction
public interface IAuraClient
{
    Task<bool> HealthCheckAsync(CancellationToken ct = default);
    Task<Story> CreateStoryAsync(CreateStoryRequest request, CancellationToken ct = default);
    Task<Story> GetStoryAsync(Guid id, CancellationToken ct = default);
    Task<Story> AnalyzeStoryAsync(Guid id, CancellationToken ct = default);
    Task<Story> PlanStoryAsync(Guid id, CancellationToken ct = default);
    Task<Story> RunStoryAsync(Guid id, CancellationToken ct = default);
    Task DeleteStoryAsync(Guid id, CancellationToken ct = default);
}

// Scenario loading
public interface IScenarioLoader
{
    Task<IReadOnlyList<Scenario>> LoadAllAsync(string scenariosPath, CancellationToken ct = default);
    Task<Scenario> LoadAsync(string path, CancellationToken ct = default);
}

// Story execution
public interface IStoryRunner
{
    Task<StoryResult> RunAsync(Scenario scenario, CancellationToken ct = default);
}

// Report generation
public interface IReportGenerator
{
    Task WriteConsoleReportAsync(SuiteResult result, CancellationToken ct = default);
    Task WriteJsonReportAsync(SuiteResult result, string outputPath, CancellationToken ct = default);
}
```

### Dependency Flow

```
CLI Commands
    ↓
StoryRunner (orchestrates test execution)
    ↓
├── ScenarioLoader (reads YAML scenarios)
├── AuraClient (calls Aura API)
└── ReportGenerator (writes output)
```

---

## Test Repositories (Fixtures)

### Structure (per ADR-014)

```
anvil/
├── fixtures/
│   └── repos/
│       ├── csharp-console/          # git submodule
│       ├── python-flask/            # git submodule
│       └── typescript-express/      # git submodule
└── scenarios/
    ├── csharp/
    │   ├── add-validation.yaml
    │   └── implement-interface.yaml
    └── python/
        └── add-endpoint.yaml
```

### Git Submodule Commands

```bash
# Add a test repo
git submodule add https://github.com/test-org/csharp-console.git anvil/fixtures/repos/csharp-console

# Clone with submodules
git clone --recursive https://github.com/org/aura-anvil.git

# Update submodules
git submodule update --init --recursive
```

---

## Scenario Definition Format

Based on research, propose this YAML schema:

```yaml
# scenarios/csharp/add-email-validation.yaml
name: Add Email Validation
description: |
  Add email validation to the User model using DataAnnotations.
  The validator should reject invalid email formats.

language: csharp
repository: fixtures/repos/csharp-console

story:
  title: Add email validation to User model
  description: |
    Implement email validation using DataAnnotations on the User class.
    - Add EmailAddress attribute to Email property
    - Create custom validation that rejects emails without @ symbol
    - Add unit tests for validation logic

expectations:
  - type: compiles
    description: Solution should compile without errors
  
  - type: tests_pass
    description: All tests should pass
  
  - type: file_exists
    path: src/Models/User.cs
    description: User model should exist
  
  - type: file_contains
    path: src/Models/User.cs
    pattern: "\\[EmailAddress\\]"
    description: Should have EmailAddress attribute

timeout: 300  # seconds (5 minutes)

tags:
  - validation
  - dataannotations
  - beginner
```

---

## Exemplars Identified

| What | Aura Location | Why Relevant |
|------|---------------|--------------|
| HTTP Client wrapper | `src/Aura.Module.Developer/GitHub/GitHubService.cs` | Shows typed HTTP client pattern |
| Story entity | `src/Aura.Module.Developer/Data/Entities/` | Shows entity structure |
| API endpoints | `src/Aura.Api/Endpoints/DeveloperEndpoints.cs` | Shows request/response shapes |
| Language definitions | `agents/languages/*.yaml` | Shows build/test commands |
| YAML loading | (needs search) | Need to find YAML parsing pattern |

---

## Gaps to Fill

1. **Scenario YAML schema** - Need to finalize the exact format
2. **Test repository selection** - Need to choose/create initial test repos
3. **Aura automation mode** - Need to confirm `Autonomous` mode works end-to-end
4. **Error categorization** - Need to define how to categorize failures (Aura bug vs test repo issue)
5. **Timeout handling** - Need to implement story execution timeouts

---

## Open Questions

1. **Autonomous vs Assisted mode**: Should Anvil use `Autonomous` mode (Aura runs all steps) or `Assisted` (Anvil triggers each step)? 
   - **Recommendation**: Use `Autonomous` with `/run` endpoint for simplicity

2. **SSE streaming**: Should Anvil use the `/stream` endpoint for real-time progress?
   - **Recommendation**: Start with polling `/api/developer/stories/{id}`, add streaming later

3. **Existing test repos**: Are there any existing test repositories we can use?
   - **Action**: Search for or create minimal test repos for each language

4. **Cleanup strategy**: Should Anvil delete stories after test run?
   - **Recommendation**: Yes, always clean up (delete story + worktree)

---

## Key Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Aura API not running | High | Blocks all testing | Health check before suite, clear error message |
| Story execution hangs | Medium | Test suite stuck | Implement timeout, use CancellationToken |
| Worktree cleanup fails | Medium | Disk space issues | Track created stories, cleanup in finally block |
| Test repo becomes stale | Low | False positives | Git submodule update, version lock |

---

## Handoff to Planning

### Ready for Planning: ✅

**Acceptance Criteria Count**: 7 criteria defined
**Components Identified**: CLI, AuraClient, ScenarioLoader, StoryRunner, ReportGenerator
**Exemplars Found**: 4 exemplars for 4 component types
**Blocking Questions**: None (recommendations provided)

### Key Decisions Made

1. Use `POST /api/developer/stories/{id}/run` for autonomous execution
2. Use polling for status (not SSE streaming) initially
3. Delete stories after each test run for cleanup
4. Scenario files use YAML with expectations array
5. Test repos are git submodules in `fixtures/repos/`

### Risks for Planning to Address

1. Define exact scenario YAML schema (may need iteration)
2. Choose initial test repositories (or create minimal ones)
3. Implement graceful timeout handling
4. Handle partial cleanup on Ctrl+C

---

## Research Notes (Appendix)

### Aura Health Endpoint

```http
GET /health
Response: {"status": "Healthy"}
```

### Aura Story Create Full Response

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "title": "string",
  "description": "string",
  "status": "Created",
  "automationMode": "Autonomous",
  "gitBranch": "feature/workflow-3fa85f64",
  "worktreePath": "/workspaces/repo-wt-3fa85f64",
  "repositoryPath": "c:\\work\\repo",
  "issueUrl": null,
  "issueProvider": null,
  "issueNumber": null,
  "issueOwner": null,
  "issueRepo": null,
  "createdAt": "2026-01-30T10:00:00Z"
}
```

### Key Files Examined

- `.project/STATUS.md` - Aura v1.3.1, all components complete
- `.project/features/completed/git-worktrees.md` - Worktree lifecycle
- `.project/features/completed/developer-module.md` - Story workflow
- `.project/features/completed/story-model.md` - Entity structure
- `.project/reference/api-cheat-sheet.md` - API endpoints
- `src/Aura.Api/Endpoints/DeveloperEndpoints.cs` - Actual endpoints
- `src/Aura.Api/Contracts/ApiContracts.cs` - Request DTOs
- `agents/languages/csharp.yaml` - C# tool definitions
- `agents/languages/python.yaml` - Python tool definitions
