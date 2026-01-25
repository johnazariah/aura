# Aura UX Testing Plan

> **Purpose**: Validate the three primary user experiences in Aura before v1.5.0 release.
> **Priority**: Mode 3 (Parallel Dispatch) > Mode 1 (MCP Chat) > Mode 2 (Sequential Steps)

---

## Mode 3: Parallel Multi-Agent Dispatch (Highest Priority)

### Overview
User creates a story, Aura decomposes it into parallelizable tasks, dispatches to GitHub Copilot CLI agents in waves, runs quality gates between waves.

### Prerequisites
- [ ] GitHub Copilot CLI installed (`gh extension install github/gh-copilot`)
- [ ] GH CLI authenticated (`gh auth status`)
- [ ] Aura API running (`http://localhost:5300/health`)
- [ ] PostgreSQL running on port 5433

### Test Scenarios

#### 3.1 Story Creation with Repository Path
**Steps:**
1. Create a story via API with `repositoryPath` set
2. Verify worktree is created at expected path
3. Verify git branch is created with correct naming convention

**Expected:**
- Story has `worktreePath` populated
- Story has `gitBranch` populated
- Directory exists at worktree path
- Branch exists in git

**Command:**
```powershell
$body = @{
    title = "Add logging to OrderService"
    description = "Add structured logging to all public methods in OrderService"
    repositoryPath = "c:\work\aura"
} | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:5300/api/developer/stories" -Method Post -Body $body -ContentType "application/json"
```

#### 3.2 Story Analysis
**Steps:**
1. Call `/analyze` endpoint on created story
2. Verify analysis agent ran successfully
3. Verify `analyzedContext` is populated

**Expected:**
- Status changes to `Analyzed`
- `analyzedContext` contains agent analysis with file paths and considerations
- Tool steps recorded in analysis

**Command:**
```powershell
Invoke-RestMethod -Uri "http://localhost:5300/api/developer/stories/{id}/analyze" -Method Post
```

#### 3.3 Task Decomposition
**Steps:**
1. Call `/decompose` endpoint on analyzed story
2. Verify tasks are created with wave assignments
3. Verify dependency graph is logical

**Expected:**
- Response contains `tasks` array
- Each task has `wave` number (1-N)
- Tasks with dependencies are in later waves than their dependencies
- `waveCount` matches highest wave number
- Task descriptions are actionable and specific

**Command:**
```powershell
Invoke-RestMethod -Uri "http://localhost:5300/api/developer/stories/{id}/decompose" -Method Post -Body "{}" -ContentType "application/json"
```

**Validation Checklist:**
- [ ] Wave 1 tasks have no dependencies
- [ ] Each task's `dependsOn` references only tasks in earlier waves
- [ ] Task descriptions include specific file paths
- [ ] Task descriptions are implementable by an agent

#### 3.4 Orchestrator Run (Happy Path)
**Steps:**
1. Call `/run` endpoint on decomposed story
2. Monitor task execution per wave
3. Verify quality gates run between waves
4. Verify final story state

**Expected:**
- Wave 1 tasks start in parallel
- Quality gate (build) runs after wave 1 completes
- Wave 2 tasks start after gate passes
- Process continues until all waves complete or failure

**Command:**
```powershell
Invoke-RestMethod -Uri "http://localhost:5300/api/developer/stories/{id}/run" -Method Post -Body "{}" -ContentType "application/json"
```

#### 3.5 Orchestrator Status Polling
**Steps:**
1. While orchestrator is running, poll `/orchestrator-status`
2. Verify real-time status updates

**Expected:**
- `currentWave` increments as waves complete
- `startedTasks` shows currently executing tasks
- `completedTasks` accumulates finished tasks
- `gateResult` shows quality gate outcome when applicable

**Command:**
```powershell
Invoke-RestMethod -Uri "http://localhost:5300/api/developer/stories/{id}/orchestrator-status"
```

#### 3.6 Quality Gate Failure
**Steps:**
1. Create a story that will produce invalid code
2. Run decompose and run
3. Verify quality gate catches failure
4. Verify orchestrator stops appropriately

**Expected:**
- Build gate fails with compilation errors
- Orchestrator status shows `waitingForGate: false`
- `gateResult` contains build errors
- Later waves do not execute

#### 3.7 Task Failure Recovery
**Steps:**
1. Simulate a task failure (e.g., agent timeout)
2. Verify failure is recorded
3. Verify other parallel tasks continue
4. Verify wave-level failure handling

**Expected:**
- Failed task appears in `failedTasks`
- Other wave tasks complete (or fail independently)
- Wave is marked failed if any task fails
- Error message is actionable

#### 3.8 PowerShell Module Experience
**Steps:**
1. Import `Aura-Story.psm1`
2. Test full workflow via cmdlets

**Commands:**
```powershell
Import-Module .\scripts\Aura-Story.psm1

# Create
$story = New-AuraStory -Title "Add caching" -Description "Add Redis caching to API" -RepositoryPath "c:\work\aura"

# Decompose
Start-AuraStoryDecompose -Id $story.id

# Run
Start-AuraStory -Id $story.id

# Monitor
Get-AuraStoryStatus -Id $story.id

# Complete
Complete-AuraStory -Id $story.id
```

**Expected:**
- All cmdlets work without errors
- Output is formatted and readable
- Progress is visible during long operations

### Known Limitations
- GitHub Copilot CLI must be installed and authenticated
- Large tasks may timeout (default 5 min per task)
- Quality gates require project to be buildable

---

## Mode 1: Free-Flowing MCP Chat (Second Priority)

### Overview
User has a conversation with GitHub Copilot in VS Code, Aura exposes tools via MCP server that Copilot can call.

### Prerequisites
- [ ] VS Code with GitHub Copilot extension
- [ ] Aura extension installed
- [ ] MCP server running (shown in Copilot tools panel)
- [ ] Aura API running

### Test Scenarios

#### 1.1 MCP Server Discovery
**Steps:**
1. Open VS Code
2. Open Copilot Chat
3. Check MCP tools are listed

**Expected:**
- Aura MCP tools appear in Copilot's tool list
- Tools include: `aura_search`, `aura_navigate`, `aura_inspect`, `aura_tree`, `aura_refactor`, `aura_generate`, `aura_validate`, `aura_workflow`, etc.

#### 1.2 Semantic Code Search
**Steps:**
1. Ask Copilot: "Search for code related to story decomposition"
2. Verify `aura_search` is called

**Expected:**
- Copilot calls `aura_search` with appropriate query
- Results include relevant code snippets
- File paths are correct and clickable

#### 1.3 Code Navigation
**Steps:**
1. Ask Copilot: "Find all callers of StoryService.DecomposeAsync"
2. Verify `aura_navigate` is called with `callers` operation

**Expected:**
- Returns list of call sites
- Includes file paths and line numbers
- Context is sufficient to understand usage

#### 1.4 Type Inspection
**Steps:**
1. Ask Copilot: "Show me all members of the Story class"
2. Verify `aura_inspect` with `type_members` operation

**Expected:**
- Lists all properties, methods, constructors
- Shows visibility and return types
- Organized logically

#### 1.5 Hierarchical Tree Exploration
**Steps:**
1. Ask Copilot: "Show me the structure of Aura.Module.Developer"
2. Verify `aura_tree` is called

**Expected:**
- Returns hierarchical view: folders → files → types → members
- Expandable structure
- Performance is acceptable (<5s for large projects)

#### 1.6 Refactoring via Chat
**Steps:**
1. Ask Copilot: "Rename the method GetHealthStatus to GetServiceHealth"
2. Verify `aura_refactor` with `rename` operation

**Expected:**
- Blast radius analysis shown first
- Asks for confirmation before executing
- All references updated consistently
- Build passes after refactoring

#### 1.7 Code Generation via Chat
**Steps:**
1. Ask Copilot: "Generate a new service interface INotificationService with a SendAsync method"
2. Verify `aura_generate` is called

**Expected:**
- Interface created with correct namespace
- Proper XML documentation
- File placed in appropriate location

#### 1.8 Build Validation
**Steps:**
1. After making changes, ask: "Validate the build"
2. Verify `aura_validate` with `compilation` operation

**Expected:**
- Build runs successfully or errors reported
- Error messages include file paths and line numbers
- Actionable feedback provided

#### 1.9 Story Management via Chat
**Steps:**
1. Ask Copilot: "Create a new story for adding email validation"
2. Verify `aura_workflow` with `create` operation

**Expected:**
- Story created successfully
- Story ID returned
- Story visible in Aura UI

#### 1.10 Multi-Tool Conversation
**Steps:**
1. Have a multi-turn conversation requiring multiple tools:
   - "Find the StoryService class"
   - "Show me its public methods"
   - "Find all usages of CreateAsync"
   - "Generate tests for CreateAsync"

**Expected:**
- Each tool called appropriately
- Context maintained across turns
- Final result is coherent and useful

### Known Limitations
- MCP tool results may be truncated for large outputs
- Some operations require solution to be indexed first
- Tool calling depends on Copilot's decision-making

---

## Mode 2: Structured Sequential Development (Third Priority)

### Overview
User creates a story, Aura generates implementation steps, user executes steps one at a time with agent assistance.

### Prerequisites
- [ ] Aura API running
- [ ] VS Code with Aura extension
- [ ] PostgreSQL running

### Test Scenarios

#### 2.1 Story Creation via UI
**Steps:**
1. Open Aura sidebar in VS Code
2. Click "New Story"
3. Enter title and description
4. Submit

**Expected:**
- Story appears in sidebar list
- Status shows "Created"
- Story details accessible

#### 2.2 Story Analysis via UI
**Steps:**
1. Select story in sidebar
2. Click "Analyze"
3. Wait for analysis to complete

**Expected:**
- Loading indicator shown
- Analysis summary displayed
- File recommendations visible
- Status changes to "Analyzed"

#### 2.3 Plan Generation
**Steps:**
1. Click "Plan" on analyzed story
2. Wait for steps to be generated

**Expected:**
- Steps listed in sidebar
- Each step has title and description
- Steps are in logical order
- Status changes to "Planned"

#### 2.4 Step Execution
**Steps:**
1. Select first step
2. Click "Execute"
3. Observe agent execution
4. Review proposed changes

**Expected:**
- Agent runs with visible progress
- Tool calls shown in execution log
- Proposed code changes displayed
- Diff view available

#### 2.5 Step Approval/Rejection
**Steps:**
1. After step execution, review changes
2. Click "Approve" or "Reject"

**Expected:**
- Approved: Changes applied, step marked complete
- Rejected: Changes discarded, step reset

#### 2.6 Code Validation After Step
**Steps:**
1. After approving a step, observe validation
2. Check for build errors

**Expected:**
- Automatic build validation runs
- Errors shown if any
- User can proceed or fix

#### 2.7 Step Chat Interaction
**Steps:**
1. During step execution, ask clarifying questions
2. Provide additional context

**Expected:**
- Agent responds to questions
- Context influences execution
- Conversation history maintained

#### 2.8 Execute All Steps
**Steps:**
1. On a planned story, click "Execute All"
2. Set `stopOnError: true`
3. Monitor progress

**Expected:**
- Steps execute sequentially
- Progress indicator updates
- Stops on first error if configured
- All steps complete if no errors

#### 2.9 Story Completion
**Steps:**
1. After all steps complete, click "Complete"
2. Provide commit message

**Expected:**
- Changes committed to git
- Branch ready for PR
- Story status changes to "Completed"

#### 2.10 Error Recovery
**Steps:**
1. Force an error during step execution
2. Observe error handling
3. Reset and retry

**Expected:**
- Error message is clear
- Step can be reset
- Retry works correctly

### Known Limitations
- Step execution requires appropriate agent to be available
- Large steps may require manual intervention
- UI may lag with very long execution logs

---

## Internal Agent Tools (New)

### Overview
Internal agents (coding-agent, build-fixer-agent) now have access to Aura tools that wrap Roslyn functionality. These are distinct from MCP tools - they're available via the tool registry during step execution.

### Tools Added
- `aura.refactor` - Roslyn-powered refactoring (rename, change_signature, extract_interface, safe_delete, move_type_to_file)
- `aura.generate` - Code generation (tests, create_type, implement_interface, constructor, property, method)
- `aura.validate` - Returns compilation/test validation commands

### Prerequisites
- [ ] Aura API running
- [ ] Solution path available to tools
- [ ] Roslyn workspace loaded

### Test Scenarios

#### 4.1 Rename with Blast Radius Analysis
**Steps:**
1. Execute a step that triggers `aura.refactor` with `operation: rename`
2. Verify blast radius is returned in analyze mode (default)

**Expected:**
- Tool returns reference count, files affected, related symbols
- Suggested execution plan provided
- No changes made until `analyze: false`

**Tool Call Example:**
```json
{
  "operation": "rename",
  "solutionPath": "c:\\work\\aura\\Aura.sln",
  "symbolName": "GetHealthStatus",
  "newName": "GetServiceHealth"
}
```

#### 4.2 Execute Rename Refactoring
**Steps:**
1. Call `aura.refactor` with `analyze: false`
2. Verify symbol is renamed across solution

**Expected:**
- All references updated
- Modified files list returned
- Build passes after rename

#### 4.3 Generate Tests
**Steps:**
1. Call `aura.generate` with `operation: tests`
2. Verify test file is created

**Expected:**
- Test class created in appropriate test project
- Framework auto-detected (xUnit)
- Mock library detected (NSubstitute)
- Tests compile (if `validateCompilation: true`)

**Tool Call Example:**
```json
{
  "operation": "tests",
  "solutionPath": "c:\\work\\aura\\Aura.sln",
  "target": "StoryService",
  "analyzeOnly": false
}
```

#### 4.4 Analyze Test Targets
**Steps:**
1. Call `aura.generate` with `operation: tests` and `analyzeOnly: true`
2. Review testable members

**Expected:**
- Returns list of testable methods
- Detected framework and mock library
- Suggested test count

#### 4.5 Create New Type
**Steps:**
1. Call `aura.generate` with `operation: create_type`
2. Verify file is created with correct structure

**Expected:**
- File created at target directory
- Namespace matches project conventions
- XML documentation included
- Interfaces implemented if specified

**Tool Call Example:**
```json
{
  "operation": "create_type",
  "solutionPath": "c:\\work\\aura\\Aura.sln",
  "typeName": "NotificationService",
  "typeKind": "class",
  "targetDirectory": "src/Aura.Foundation/Notifications",
  "implements": ["INotificationService"],
  "documentation": "Handles notification delivery."
}
```

#### 4.6 Add Property to Existing Class
**Steps:**
1. Call `aura.generate` with `operation: property`
2. Verify property added to class

**Expected:**
- Property added with correct type
- Access modifier applied
- XML documentation included if provided

#### 4.7 Add Method to Existing Class
**Steps:**
1. Call `aura.generate` with `operation: method`
2. Verify method added with parameters

**Expected:**
- Method signature correct
- Parameters included
- Body inserted if provided

#### 4.8 Implement Interface
**Steps:**
1. Call `aura.generate` with `operation: implement_interface`
2. Verify all interface members stubbed

**Expected:**
- All methods implemented (throw NotImplementedException)
- All properties implemented
- File modified in place

#### 4.9 Validate Compilation
**Steps:**
1. Call `aura.validate` with `operation: compilation`
2. Verify command is returned

**Expected:**
- Returns `dotnet build` command
- Agent can execute command with `shell.execute`
- Error handling guidance provided

#### 4.10 Change Method Signature
**Steps:**
1. Call `aura.refactor` with `operation: change_signature`
2. Add a parameter to a method

**Expected:**
- Method signature updated
- All call sites updated with default value
- Build passes after change

**Tool Call Example:**
```json
{
  "operation": "change_signature",
  "solutionPath": "c:\\work\\aura\\Aura.sln",
  "symbolName": "ProcessOrderAsync",
  "containingType": "OrderService",
  "addParameters": [{"name": "priority", "type": "int", "defaultValue": "0"}]
}
```

#### 4.11 Extract Interface
**Steps:**
1. Call `aura.refactor` with `operation: extract_interface`
2. Verify interface created and class updated

**Expected:**
- New interface file created
- Original class implements interface
- Selected members in interface

#### 4.12 Safe Delete
**Steps:**
1. Call `aura.refactor` with `operation: safe_delete` on unused symbol
2. Verify deletion occurs

**Expected:**
- Symbol removed if no usages
- Error if symbol has usages
- Files updated/deleted as appropriate

### Known Limitations
- Roslyn workspace must be loaded (may require solution open)
- Large solutions may have slower response times
- Some complex refactorings may require manual follow-up

---

## Cross-Cutting Concerns

### Performance
- [ ] API response times < 500ms for CRUD operations
- [ ] Code search < 3s for typical queries
- [ ] Build validation < 30s for solution
- [ ] UI remains responsive during long operations

### Error Handling
- [ ] All errors return RFC7807 Problem Details
- [ ] Error messages are actionable
- [ ] No unhandled exceptions crash the server
- [ ] UI shows friendly error messages

### Logging
- [ ] All operations logged with correlation IDs
- [ ] Sensitive data not logged
- [ ] Log levels appropriate (Info for normal, Error for failures)

### Database
- [ ] Migrations apply cleanly
- [ ] No data loss on upgrade
- [ ] Concurrent operations handled correctly

---

## Test Execution Checklist

### Before Testing
- [ ] Fresh database (or run migrations)
- [ ] All services running (API, PostgreSQL, MCP)
- [ ] GitHub Copilot CLI authenticated (for Mode 3)
- [ ] VS Code with latest Aura extension

### Mode 3 Tests
- [ ] 3.1 Story Creation with Repository Path
- [ ] 3.2 Story Analysis
- [ ] 3.3 Task Decomposition
- [ ] 3.4 Orchestrator Run (Happy Path)
- [ ] 3.5 Orchestrator Status Polling
- [ ] 3.6 Quality Gate Failure
- [ ] 3.7 Task Failure Recovery
- [ ] 3.8 PowerShell Module Experience

### Mode 1 Tests
- [ ] 1.1 MCP Server Discovery
- [ ] 1.2 Semantic Code Search
- [ ] 1.3 Code Navigation
- [ ] 1.4 Type Inspection
- [ ] 1.5 Hierarchical Tree Exploration
- [ ] 1.6 Refactoring via Chat
- [ ] 1.7 Code Generation via Chat
- [ ] 1.8 Build Validation
- [ ] 1.9 Story Management via Chat
- [ ] 1.10 Multi-Tool Conversation

### Mode 2 Tests
- [ ] 2.1 Story Creation via UI
- [ ] 2.2 Story Analysis via UI
- [ ] 2.3 Plan Generation
- [ ] 2.4 Step Execution
- [ ] 2.5 Step Approval/Rejection
- [ ] 2.6 Code Validation After Step
- [ ] 2.7 Step Chat Interaction
- [ ] 2.8 Execute All Steps
- [ ] 2.9 Story Completion
- [ ] 2.10 Error Recovery

### Internal Agent Tools Tests
- [ ] 4.1 Rename with Blast Radius Analysis
- [ ] 4.2 Execute Rename Refactoring
- [ ] 4.3 Generate Tests
- [ ] 4.4 Analyze Test Targets
- [ ] 4.5 Create New Type
- [ ] 4.6 Add Property to Existing Class
- [ ] 4.7 Add Method to Existing Class
- [ ] 4.8 Implement Interface
- [ ] 4.9 Validate Compilation
- [ ] 4.10 Change Method Signature
- [ ] 4.11 Extract Interface
- [ ] 4.12 Safe Delete

---

## Issue Tracking

| Test ID | Status | Issue | Notes |
|---------|--------|-------|-------|
| 3.4 | Blocked | GH Copilot CLI not available | Need to install/auth |
| | | | |
| | | | |

---

## Sign-Off

- [ ] Mode 3 tests pass
- [ ] Mode 1 tests pass
- [ ] Mode 2 tests pass
- [ ] Internal Agent Tools tests pass
- [ ] No critical/high issues open
- [ ] Ready for v1.5.0 release
