// <copyright file="Workflow.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Data.Entities;

/// <summary>
/// The root entity representing a unit of work.
/// This is the single entity for the Developer module's workflow automation.
/// </summary>
public sealed class Story
{
    /// <summary>Gets or sets the unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the workflow title.</summary>
    public required string Title { get; set; }

    /// <summary>Gets or sets the workflow description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the repository path.</summary>
    public string? RepositoryPath { get; set; }

    /// <summary>Gets or sets the workflow status.</summary>
    public StoryStatus Status { get; set; } = StoryStatus.Created;

    /// <summary>Gets or sets the worktree path (isolated git worktree for this workflow).</summary>
    public string? WorktreePath { get; set; }

    /// <summary>Gets or sets the git branch (e.g., "feature/workflow-123")..</summary>
    public string? GitBranch { get; set; }

    /// <summary>Gets or sets the analyzed context as JSON (from analysis agent).</summary>
    public string? AnalyzedContext { get; set; }

    /// <summary>Gets or sets the execution plan as JSON (from planning agent).</summary>
    public string? ExecutionPlan { get; set; }

    /// <summary>Gets or sets when the workflow was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets when the workflow was last updated.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets when the workflow was completed.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Gets or sets the URL of the pull request created for this workflow.</summary>
    public string? PullRequestUrl { get; set; }

    /// <summary>Gets or sets the workflow steps.</summary>
    public ICollection<StoryStep> Steps { get; set; } = [];

    // === Issue Integration (Story Model) ===

    /// <summary>Gets or sets the external issue URL (e.g., "https://github.com/org/repo/issues/123").</summary>
    public string? IssueUrl { get; set; }

    /// <summary>Gets or sets the issue provider type.</summary>
    public IssueProvider? IssueProvider { get; set; }

    /// <summary>Gets or sets the issue number (extracted from URL for API calls).</summary>
    public int? IssueNumber { get; set; }

    /// <summary>Gets or sets the repository owner (extracted from URL).</summary>
    public string? IssueOwner { get; set; }

    /// <summary>Gets or sets the repository name (extracted from URL).</summary>
    public string? IssueRepo { get; set; }

    // === Automation ===

    /// <summary>Gets or sets the automation mode for step execution.</summary>
    public AutomationMode AutomationMode { get; set; } = AutomationMode.Assisted;

    // === Source ===

    /// <summary>Gets or sets how this workflow was created.</summary>
    public StorySource Source { get; set; } = StorySource.User;

    /// <summary>Gets or sets the guardian ID if created by a guardian.</summary>
    public string? SourceGuardianId { get; set; }

    /// <summary>Gets or sets the pattern name if this workflow follows a pattern (e.g., "generate-tests").</summary>
    public string? PatternName { get; set; }

    /// <summary>Gets or sets the pattern language for language-specific overlays (e.g., "csharp", "python").</summary>
    public string? PatternLanguage { get; set; }

    /// <summary>Gets or sets the priority for UI sorting.</summary>
    public StoryPriority Priority { get; set; } = StoryPriority.Medium;

    /// <summary>Gets or sets the suggested specialist/agent capability for this workflow.</summary>
    public string? SuggestedCapability { get; set; }

    // === Chat ===

    /// <summary>Gets or sets the workflow-level chat history as JSON array of messages.</summary>
    public string? ChatHistory { get; set; }

    // === Verification ===

    /// <summary>Gets or sets whether verification passed before completion.</summary>
    public bool? VerificationPassed { get; set; }

    /// <summary>Gets or sets the verification result as JSON.</summary>
    public string? VerificationResult { get; set; }

    // === Orchestration (Wave Execution) ===

    /// <summary>Gets or sets the current execution wave (0 = not started, 1+ = running wave N).</summary>
    public int CurrentWave { get; set; }

    /// <summary>Gets or sets the gate mode (how to pause at quality gates).</summary>
    public GateMode GateMode { get; set; } = GateMode.AutoProceed;

    /// <summary>Gets or sets the last gate result as JSON (type, passed, errors).</summary>
    public string? GateResult { get; set; }

    /// <summary>Gets or sets the maximum number of parallel agents to use.</summary>
    public int MaxParallelism { get; set; } = 4;

    /// <summary>Gets or sets the dispatch target for task execution.</summary>
    public DispatchTarget DispatchTarget { get; set; } = DispatchTarget.CopilotCli;

    // === Legacy Orchestration (to be removed after migration to Steps with Wave) ===

    /// <summary>Gets or sets the decomposed tasks as JSON array. Will be replaced by Steps with Wave.</summary>
    public string? TasksJson { get; set; }

    /// <summary>Gets or sets the legacy orchestrator status. Will be unified with Status.</summary>
    public OrchestratorStatus OrchestratorStatus { get; set; } = OrchestratorStatus.NotDecomposed;
}

/// <summary>
/// The issue provider for external issue tracking.
/// </summary>
public enum IssueProvider
{
    /// <summary>GitHub Issues.</summary>
    GitHub,

    /// <summary>Azure DevOps Work Items.</summary>
    AzureDevOps,
}

/// <summary>
/// The automation mode for step execution.
/// Controls how much user approval is required during workflow execution.
/// </summary>
public enum AutomationMode
{
    /// <summary>
    /// User must approve each step before execution (default, safest).
    /// </summary>
    Assisted,

    /// <summary>
    /// Auto-approve steps that don't require confirmation.
    /// Steps with requiresConfirmation=true still require user approval.
    /// </summary>
    Autonomous,

    /// <summary>
    /// Auto-approve ALL steps including dangerous operations (YOLO mode).
    /// Use with caution - no human-in-the-loop safety checks.
    /// </summary>
    FullAutonomous,
}

/// <summary>
/// How a workflow was created.
/// </summary>
public enum StorySource
{
    /// <summary>Created by user via UI or API.</summary>
    User,

    /// <summary>Created automatically by a guardian.</summary>
    Guardian,

    /// <summary>Created by other system automation.</summary>
    System,
}

/// <summary>
/// Priority level for workflow sorting and attention.
/// </summary>
public enum StoryPriority
{
    /// <summary>Low priority - can wait.</summary>
    Low,

    /// <summary>Medium priority - normal work.</summary>
    Medium,

    /// <summary>High priority - should address soon.</summary>
    High,

    /// <summary>Critical priority - blocking issue.</summary>
    Critical,
}

/// <summary>
/// The status of a workflow.
/// </summary>
public enum StoryStatus
{
    /// <summary>Just created, not yet analyzed.</summary>
    Created,

    /// <summary>Analyzing the requirements.</summary>
    Analyzing,

    /// <summary>Analysis complete, ready for planning.</summary>
    Analyzed,

    /// <summary>Creating execution plan.</summary>
    Planning,

    /// <summary>Plan created, ready for execution.</summary>
    Planned,

    /// <summary>Steps being executed.</summary>
    Executing,

    /// <summary>Build/test gate is running between waves.</summary>
    GatePending,

    /// <summary>Gate failed, waiting for user action (fix wave or cancel).</summary>
    GateFailed,

    /// <summary>All steps completed successfully.</summary>
    Completed,

    /// <summary>Unrecoverable error.</summary>
    Failed,

    /// <summary>User cancelled.</summary>
    Cancelled,
}

/// <summary>
/// Controls how the orchestrator pauses at quality gates.
/// </summary>
public enum GateMode
{
    /// <summary>Auto-proceed when gate passes, only pause on failure.</summary>
    AutoProceed,

    /// <summary>Pause at every gate for human validation.</summary>
    PauseAlways,
}

/// <summary>
/// The dispatch target for parallel task execution.
/// </summary>
public enum DispatchTarget
{
    /// <summary>
    /// Use GitHub Copilot CLI agents (spawns external process).
    /// Leverages Claude via Copilot with access to Aura MCP tools.
    /// </summary>
    CopilotCli,

    /// <summary>
    /// Use Aura's internal ReAct agents (in-process).
    /// Uses configured LLM provider with Aura's tool registry.
    /// </summary>
    InternalAgents,
}

/// <summary>
/// Legacy orchestrator status. Will be unified with StoryStatus.
/// </summary>
public enum OrchestratorStatus
{
    /// <summary>Story created but not decomposed into tasks.</summary>
    NotDecomposed,

    /// <summary>Story decomposed into tasks, ready to run.</summary>
    Decomposed,

    /// <summary>Currently executing tasks in parallel.</summary>
    Running,

    /// <summary>Waiting for quality gate (build/test) between waves.</summary>
    WaitingForGate,

    /// <summary>All tasks completed successfully.</summary>
    Completed,

    /// <summary>Unrecoverable failure occurred.</summary>
    Failed,
}
