# Developer Module Specification

**Status:** Reference Documentation
**Created:** 2026-01-28

The Developer Module (`Aura.Module.Developer`) provides code automation, workflow management, and language-specific tooling for software development tasks.

## Module Registration

```csharp
public sealed class DeveloperModule : IAuraModule
{
    public string ModuleId => "developer";
    public string Name => "Developer Stories";
    public IReadOnlyList<string> Dependencies => [];  // Only Foundation
    
    public void ConfigureServices(IServiceCollection services, IConfiguration config);
    public void RegisterAgents(IAgentRegistry registry, IConfiguration config);
    public void RegisterTools(IToolRegistry toolRegistry, IServiceProvider services);
}
```

---

## 1. Story Management

### 1.1 Story Entity

The core workflow entity:

```csharp
public sealed class Story
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? RepositoryPath { get; set; }
    public StoryStatus Status { get; set; }
    
    // Git integration
    public string? WorktreePath { get; set; }
    public string? GitBranch { get; set; }
    
    // Analysis and planning
    public string? AnalyzedContext { get; set; }  // JSON
    public string? ExecutionPlan { get; set; }    // JSON
    
    // Issue integration
    public string? IssueUrl { get; set; }
    public IssueProvider? IssueProvider { get; set; }
    public int? IssueNumber { get; set; }
    public string? IssueOwner { get; set; }
    public string? IssueRepo { get; set; }
    
    // Automation
    public AutomationMode AutomationMode { get; set; }
    public DispatchTarget DispatchTarget { get; set; }
    
    // Pattern support
    public string? PatternName { get; set; }
    public string? PatternLanguage { get; set; }
    
    // Orchestration
    public int CurrentWave { get; set; }
    public GateMode GateMode { get; set; }
    public int MaxParallelism { get; set; } = 4;
    
    // Steps
    public ICollection<StoryStep> Steps { get; set; }
    
    // Timestamps
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
```

### 1.2 Story Status

```csharp
public enum StoryStatus
{
    Created,          // Just created, no analysis
    Analyzing,        // Running analysis agent
    Analyzed,         // Analysis complete
    Planning,         // Generating steps
    Planned,          // Steps generated
    InProgress,       // Executing steps
    WaitingForGate,   // Paused at quality gate
    NeedsVerification,// All steps done, needs final check
    Completed,        // Verified and done
    Failed,           // Execution failed
    Cancelled,        // User cancelled
}
```

### 1.3 Automation Mode

```csharp
public enum AutomationMode
{
    Assisted,     // User approves each step (default)
    SemiAuto,     // Auto-execute, pause on failure
    Autonomous,   // Full auto, no pauses
}
```

### 1.4 Dispatch Target

```csharp
public enum DispatchTarget
{
    CopilotCli,       // GitHub Copilot CLI for step execution
    InternalAgents,   // Aura's internal agent framework
}
```

### 1.5 Story Step Entity

```csharp
public sealed class StoryStep
{
    public Guid Id { get; set; }
    public Guid StoryId { get; set; }
    public int Order { get; set; }
    public required string Name { get; set; }
    public required string Capability { get; set; }  // e.g., "csharp-coding"
    public string? Description { get; set; }
    public StepStatus Status { get; set; }
    public string? AssignedAgentId { get; set; }
    public int Attempts { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
    public string? Approval { get; set; }  // approved/rejected/skipped
    public string? ReviewFeedback { get; set; }
    
    // Wave execution
    public int Wave { get; set; }  // For parallel execution grouping
    
    // Timestamps
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
```

### 1.6 Step Status

```csharp
public enum StepStatus
{
    Pending,        // Not started
    InProgress,     // Currently executing
    Completed,      // Execution finished, needs review
    Approved,       // User approved
    Rejected,       // User rejected, needs revision
    Skipped,        // User skipped
    Failed,         // Execution error
}
```

---

## 2. Story Service

### 2.1 Interface

```csharp
public interface IStoryService
{
    // CRUD
    Task<Story> CreateAsync(string title, string? description, string? repositoryPath, 
        AutomationMode mode, string? issueUrl, DispatchTarget target, CancellationToken ct);
    Task<Story?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Story?> GetByIdWithStepsAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Story>> ListAsync(StoryStatus? status, string? repositoryPath, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    
    // Lifecycle
    Task<Story> AnalyzeAsync(Guid id, CancellationToken ct);
    Task<Story> PlanAsync(Guid id, CancellationToken ct);
    Task<Story> DecomposeAsync(Guid id, string patternName, string? language, CancellationToken ct);
    Task<StoryStep> ExecuteStepAsync(Guid storyId, Guid stepId, CancellationToken ct);
    Task ExecuteAllAsync(Guid id, CancellationToken ct);
    Task CompleteAsync(Guid id, CancellationToken ct);
    Task CancelAsync(Guid id, CancellationToken ct);
    
    // Step operations
    Task<StoryStep> ApproveStepAsync(Guid storyId, Guid stepId, CancellationToken ct);
    Task<StoryStep> RejectStepAsync(Guid storyId, Guid stepId, string feedback, CancellationToken ct);
    Task<StoryStep> SkipStepAsync(Guid storyId, Guid stepId, string reason, CancellationToken ct);
    Task<StoryStep> ResetStepAsync(Guid storyId, Guid stepId, CancellationToken ct);
    Task<StoryStep> ReassignStepAsync(Guid storyId, Guid stepId, string agentId, CancellationToken ct);
    
    // Issue integration
    Task<Story> CreateFromIssueAsync(string issueUrl, string repositoryPath, CancellationToken ct);
    Task RefreshFromIssueAsync(Guid id, CancellationToken ct);
    Task PostUpdateToIssueAsync(Guid id, string message, CancellationToken ct);
}
```

### 2.2 Git Worktree Integration

When creating a story with a repository path:

1. Generate branch name from story title (e.g., `story/add-user-authentication-abc123`)
2. Create git worktree in sibling directory (e.g., `../repo-worktrees/story-abc123/`)
3. Store worktree path and branch name in story entity
4. All file operations use worktree path

### 2.3 Pattern Decomposition

Stories can be decomposed using patterns:

```csharp
// Load pattern with optional language overlay
var pattern = patternLoader.Load("generate-tests", language: "csharp");

// Generate steps from pattern phases
var steps = pattern.Phases
    .SelectMany(phase => phase.Steps)
    .Select((step, idx) => new StoryStep
    {
        Name = step.Name,
        Capability = step.Capability,
        Description = step.Description,
        Order = idx,
        Wave = step.Wave,
    });
```

---

## 3. Roslyn Integration

### 3.1 Roslyn Workspace Service

Manages MSBuild workspace for semantic analysis:

```csharp
public interface IRoslynWorkspaceService
{
    Task<Compilation?> GetCompilationAsync(string solutionPath, string? projectName, CancellationToken ct);
    Task<IReadOnlyList<ProjectInfo>> ListProjectsAsync(string solutionPath, CancellationToken ct);
    Task<IReadOnlyList<TypeInfo>> ListTypesAsync(string solutionPath, string projectName, CancellationToken ct);
    Task<TypeDetails?> GetTypeDetailsAsync(string solutionPath, string typeName, CancellationToken ct);
    Task<IReadOnlyList<Diagnostic>> ValidateCompilationAsync(string solutionPath, string? projectName, CancellationToken ct);
    void InvalidateCache(string solutionPath);
}
```

### 3.2 Roslyn Refactoring Service

Semantic refactoring operations:

```csharp
public interface IRoslynRefactoringService
{
    // Rename
    Task<RefactoringResult> RenameSymbolAsync(
        string solutionPath, string symbolName, string newName, 
        string? containingType, bool preview, CancellationToken ct);
    
    // Change signature
    Task<RefactoringResult> ChangeSignatureAsync(
        string solutionPath, string methodName, string? containingType,
        IReadOnlyList<ParameterInfo>? addParameters,
        IReadOnlyList<string>? removeParameters,
        bool preview, CancellationToken ct);
    
    // Extract interface
    Task<RefactoringResult> ExtractInterfaceAsync(
        string solutionPath, string className, string interfaceName,
        IReadOnlyList<string> members, bool preview, CancellationToken ct);
    
    // Extract method
    Task<RefactoringResult> ExtractMethodAsync(
        string filePath, int startOffset, int endOffset,
        string newMethodName, bool preview, CancellationToken ct);
    
    // Move type to file
    Task<RefactoringResult> MoveTypeToFileAsync(
        string solutionPath, string typeName, string? targetDirectory,
        bool preview, CancellationToken ct);
    
    // Safe delete
    Task<RefactoringResult> SafeDeleteAsync(
        string solutionPath, string symbolName, string? containingType,
        bool preview, CancellationToken ct);
    
    // Blast radius analysis
    Task<BlastRadiusResult> AnalyzeBlastRadiusAsync(
        string solutionPath, string symbolName, string operation,
        CancellationToken ct);
}
```

### 3.3 Blast Radius Analysis

Before executing refactorings, analyze impact:

```csharp
public record BlastRadiusResult
{
    public required string SymbolName { get; init; }
    public required string Operation { get; init; }
    public required int AffectedFiles { get; init; }
    public required int AffectedReferences { get; init; }
    public required IReadOnlyList<string> RelatedSymbols { get; init; }  // Discovered by naming convention
    public required IReadOnlyList<string> SuggestedSteps { get; init; }
}
```

---

## 4. Test Generation

### 4.1 Test Generation Service

```csharp
public interface ITestGenerationService
{
    Task<TestGenerationResult> GenerateTestsAsync(
        string solutionPath,
        string targetClass,
        TestGenerationOptions options,
        CancellationToken ct);
    
    Task<TestAnalysisResult> AnalyzeTestableMethodsAsync(
        string solutionPath,
        string targetClass,
        CancellationToken ct);
}

public record TestGenerationOptions
{
    public int? MaxTests { get; init; } = 20;
    public TestFocus Focus { get; init; } = TestFocus.All;
    public bool ValidateCompilation { get; init; } = false;
    public string? OutputDirectory { get; init; }
}

public enum TestFocus
{
    All,
    HappyPath,
    EdgeCases,
    ErrorHandling,
}
```

### 4.2 Test Framework Detection

Auto-detect test framework from project references:
- xUnit: `Fact`, `Theory` attributes
- NUnit: `Test`, `TestCase` attributes
- MSTest: `TestMethod` attribute

Auto-detect mocking library:
- NSubstitute: `Substitute.For<T>()`
- Moq: `Mock<T>()`
- FakeItEasy: `A.Fake<T>()`

---

## 5. Python Refactoring

### 5.1 Python Refactoring Service

Uses `rope` library via Python subprocess:

```csharp
public interface IPythonRefactoringService
{
    Task<RefactoringResult> RenameAsync(
        string filePath, int offset, string newName,
        string projectPath, bool preview, CancellationToken ct);
    
    Task<RefactoringResult> ExtractMethodAsync(
        string filePath, int startOffset, int endOffset,
        string newMethodName, string projectPath, bool preview, CancellationToken ct);
    
    Task<RefactoringResult> ExtractVariableAsync(
        string filePath, int startOffset, int endOffset,
        string newVariableName, string projectPath, bool preview, CancellationToken ct);
    
    Task<IReadOnlyList<ReferenceLocation>> FindReferencesAsync(
        string filePath, int offset, string projectPath, CancellationToken ct);
    
    Task<DefinitionLocation?> FindDefinitionAsync(
        string filePath, int offset, string projectPath, CancellationToken ct);
}
```

---

## 6. TypeScript Refactoring

### 6.1 TypeScript Refactoring Service

Uses `ts-morph` library via Node.js subprocess:

```csharp
public interface ITypeScriptRefactoringService
{
    Task<RefactoringResult> RenameAsync(
        string filePath, int offset, string newName,
        string projectPath, bool preview, CancellationToken ct);
    
    Task<IReadOnlyList<ReferenceLocation>> FindReferencesAsync(
        string filePath, int offset, string projectPath, CancellationToken ct);
    
    Task<DefinitionLocation?> FindDefinitionAsync(
        string filePath, int offset, string projectPath, CancellationToken ct);
}
```

---

## 7. Code Generation

### 7.1 Generate Operations

The `aura_generate` tool supports:

| Operation | Description |
|-----------|-------------|
| `create_type` | Create class/interface/record/struct with modifiers |
| `implement_interface` | Add interface methods to a class |
| `constructor` | Generate constructor from member list |
| `property` | Add property with getters/setters |
| `method` | Add method with signature and body |
| `tests` | Generate test class with test methods |

### 7.2 Modern C# Features

Supports C# 9-12 features:
- Record types (positional and nominal)
- Primary constructors
- Init-only properties
- Required members
- File-scoped namespaces
- Generic type parameters with constraints

---

## 8. Verification

### 8.1 Story Verification

Before completing a story, run verification checks:

```csharp
public interface IStoryVerificationService
{
    Task<VerificationResult> VerifyAsync(Story story, CancellationToken ct);
}

public record VerificationResult
{
    public bool Passed { get; init; }
    public bool BuildSucceeded { get; init; }
    public bool TestsPassed { get; init; }
    public bool FormatPassed { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
    public string? BuildOutput { get; init; }
    public string? TestOutput { get; init; }
}
```

### 8.2 Project Verification Detection

Auto-detect verification commands from project type:

```csharp
public interface IProjectVerificationDetector
{
    Task<VerificationCommands> DetectAsync(string repositoryPath, CancellationToken ct);
}

public record VerificationCommands
{
    public string? BuildCommand { get; init; }    // e.g., "dotnet build"
    public string? TestCommand { get; init; }     // e.g., "dotnet test"
    public string? FormatCommand { get; init; }   // e.g., "dotnet format --verify-no-changes"
    public string? LintCommand { get; init; }     // e.g., "eslint ."
}
```

---

## 9. Guardian System

### 9.1 Guardian Definition (YAML)

```yaml
id: ci-guardian
name: CI/CD Guardian
version: 1
description: Monitors CI pipelines and creates stories for failures

triggers:
  - type: schedule
    cron: "*/15 * * * *"
  - type: webhook
    events:
      - workflow_run.completed

detection:
  sources:
    - type: github_actions
      branches: [main, develop]

workflow:
  title: "Fix CI: {failure_summary}"
  suggested_capability: build-fixer
  priority: high
```

### 9.2 Guardian Executor

```csharp
public interface IGuardianExecutor
{
    Task<GuardianResult> ExecuteAsync(GuardianDefinition guardian, CancellationToken ct);
}
```

### 9.3 Guardian Scheduler

Background service that:
- Loads guardians from YAML files
- Schedules execution based on cron expressions
- Creates stories when issues detected

---

## 10. GitHub Integration

### 10.1 GitHub Service

```csharp
public interface IGitHubService
{
    Task<GitHubIssue?> GetIssueAsync(string owner, string repo, int issueNumber, CancellationToken ct);
    Task<GitHubPullRequest> CreatePullRequestAsync(
        string owner, string repo, string title, string body,
        string head, string @base, CancellationToken ct);
    Task PostIssueCommentAsync(string owner, string repo, int issueNumber, string body, CancellationToken ct);
    Task CloseIssueAsync(string owner, string repo, int issueNumber, CancellationToken ct);
}
```

### 10.2 Issue URL Parsing

Parse GitHub issue URLs to extract metadata:

```
https://github.com/{owner}/{repo}/issues/{number}
```

Also supports:
- Azure DevOps work items (future)
- Jira issues (future)

---

## 11. Developer Module Tools

### 11.1 Roslyn Tools

| Tool ID | Purpose |
|---------|---------|
| `roslyn.list_projects` | List projects in solution |
| `roslyn.list_classes` | List types in project |
| `roslyn.get_class_info` | Get type details (members, base types) |
| `roslyn.find_usages` | Find symbol references |
| `roslyn.validate_compilation` | Get compilation diagnostics |
| `roslyn.get_project_references` | Get project dependencies |

### 11.2 Aura Tools (Consolidated)

| Tool ID | Operations |
|---------|------------|
| `aura.refactor` | rename, change_signature, extract_interface, extract_method, extract_variable, safe_delete, move_type_to_file |
| `aura.generate` | create_type, implement_interface, constructor, property, method, tests |
| `aura.validate` | compilation, tests |

### 11.3 Graph Tools

| Tool ID | Purpose |
|---------|---------|
| `graph.find_implementations` | Find interface implementers |
| `graph.find_callers` | Find method call sites |
| `graph.get_type_members` | Get type's members |
| `graph.index_code` | Index codebase to graph |

### 11.4 Language-Specific Tools

**Build tools:**
- `dotnet.build` - Build .NET project
- `dotnet.test` - Run .NET tests
- `rust.build` - `cargo build`
- `go.build` - `go build`
- `python.pytest` - Run pytest

**Build-fix loop:**
- `dotnet.build_until_success` - Iteratively build and fix errors
