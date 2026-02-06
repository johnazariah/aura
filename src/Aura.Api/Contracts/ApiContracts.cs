// <copyright file="ApiContracts.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Contracts;

// =============================================================================
// RAG Requests
// =============================================================================

/// <summary>Request to index content.</summary>
public record IndexContentRequest(
    string ContentId,
    string Text,
    string? ContentType = null,
    string? SourcePath = null,
    string? Language = null);

/// <summary>Request to query the RAG index.</summary>
public record RagQueryRequest(
    string Query,
    int? TopK = null,
    double? MinScore = null,
    string? SourcePathPrefix = null);

// =============================================================================
// Tool Requests
// =============================================================================

/// <summary>Request to execute a tool.</summary>
public record ExecuteToolRequest(
    string? WorkingDirectory = null,
    Dictionary<string, object?>? Parameters = null);

/// <summary>Request for ReAct-based task execution.</summary>
public record ReActExecuteRequest(
    string Task,
    string? WorkingDirectory = null,
    string? Provider = null,
    string? Model = null,
    double? Temperature = null,
    int? MaxSteps = null,
    string? Context = null,
    IReadOnlyList<string>? ToolIds = null);

// =============================================================================
// Git Requests
// =============================================================================

/// <summary>Request to create a branch.</summary>
public record CreateBranchRequest(
    string RepoPath,
    string BranchName,
    string? BaseBranch = null);

/// <summary>Request to commit changes.</summary>
public record CommitRequest(
    string RepoPath,
    string Message);

/// <summary>Request to create a worktree.</summary>
public record CreateWorktreeRequest(
    string RepoPath,
    string BranchName,
    string? WorktreePath = null,
    string? BaseBranch = null);

// =============================================================================
// Workspace Requests
// =============================================================================

/// <summary>Request to create/onboard a workspace.</summary>
public record CreateWorkspaceRequest(
    string Path,
    string? Name = null,
    bool? StartIndexing = true,
    WorkspaceIndexingOptions? Options = null);

/// <summary>Options for workspace indexing.</summary>
public record WorkspaceIndexingOptions(
    IReadOnlyList<string>? IncludePatterns = null,
    IReadOnlyList<string>? ExcludePatterns = null);

/// <summary>Request to search within a workspace.</summary>
public record WorkspaceSearchRequest(
    string Query,
    int? TopK = null,
    double? MinScore = null);

// =============================================================================
// Developer Module Requests
// =============================================================================

/// <summary>Request to create a workflow.</summary>
public record CreateStoryRequest(
    string? Title = null,
    string? Description = null,
    string? RepositoryPath = null,
    string? Mode = null,
    string? AutomationMode = null,
    string? IssueUrl = null,
    string? PreferredExecutor = null,
    List<string>? OpenQuestions = null);

/// <summary>Request to add a step to a workflow.</summary>
public record AddStepRequest(
    string Name,
    string Capability,
    string? Description = null,
    int? AfterOrder = null);

/// <summary>Request to update step description.</summary>
public record UpdateStepDescriptionRequest(string Description);

/// <summary>Request to chat within a workflow.</summary>
public record StoryChatRequest(string Message);

/// <summary>Request to finalize a workflow.</summary>
public record FinalizeStoryRequest(
    string? CommitMessage = null,
    bool CreatePullRequest = true,
    string? PrTitle = null,
    string? PrBody = null,
    string? BaseBranch = null,
    bool Draft = true);

// =============================================================================
// Story/Issue Integration Requests
// =============================================================================

/// <summary>Request to create a story from a GitHub issue.</summary>
public record CreateStoryFromIssueRequest(
    string IssueUrl,
    string? Mode = null,
    string? RepositoryPath = null,
    bool CreateWorktree = true);

/// <summary>Request to post an update to the linked issue.</summary>
public record PostUpdateRequest(string Message);

/// <summary>Request to close the linked issue.</summary>
public record CloseIssueRequest(string? Comment = null);

/// <summary>Request to export story artifacts.</summary>
public record ExportStoryRequest(
    string? OutputPath = null,
    string Format = "sdd",
    List<string>? Include = null);

/// <summary>Request to verify a story's changes.</summary>
public record VerifyStoryRequest(
    bool RunBuild = true,
    bool RunTests = true,
    bool RunLint = true,
    bool IncludeCodeReview = false);

/// <summary>Request to decompose a story into parallelizable tasks.</summary>
public record DecomposeStoryRequest(
    int MaxParallelism = 4,
    bool IncludeTests = true);

/// <summary>Response from decomposing a story into steps.</summary>
public record DecomposeStoryResponse(
    Guid StoryId,
    IReadOnlyList<StoryStepDto> Steps,
    int WaveCount);

/// <summary>DTO for a story step in decomposition response.</summary>
public record StoryStepDto(
    Guid Id,
    string Name,
    string? Description,
    int Wave,
    int Order,
    string Status);

// =============================================================================
// Response Models
// =============================================================================

/// <summary>Index health information.</summary>
public record IndexHealthInfo
{
    /// <summary>Gets the index type (rag or codegraph).</summary>
    public required string IndexType { get; init; }

    /// <summary>Gets the status.</summary>
    public required string Status { get; init; }

    /// <summary>Gets when the index was last updated.</summary>
    public DateTimeOffset? IndexedAt { get; init; }

    /// <summary>Gets the indexed commit SHA.</summary>
    public string? IndexedCommitSha { get; init; }

    /// <summary>Gets how many commits behind the index is.</summary>
    public int? CommitsBehind { get; init; }

    /// <summary>Gets whether the index is stale.</summary>
    public bool IsStale { get; init; }

    /// <summary>Gets the item count in the index.</summary>
    public int ItemCount { get; init; }
}
