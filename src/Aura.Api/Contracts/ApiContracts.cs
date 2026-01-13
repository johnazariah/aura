// <copyright file="ApiContracts.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Contracts;

// =============================================================================
// Agent Requests
// =============================================================================

/// <summary>Request to execute an agent.</summary>
public record ExecuteAgentRequest(string Prompt, string? WorkspacePath = null);

/// <summary>Request for streaming chat.</summary>
public record StreamChatRequest(
    string? Message,
    IReadOnlyList<StreamChatMessage>? History = null);

/// <summary>A message in chat history.</summary>
public record StreamChatMessage(string? Role, string? Content);

/// <summary>Request for RAG-enriched execution.</summary>
public record ExecuteWithRagRequest(
    string Prompt,
    string? WorkspacePath = null,
    bool? UseRag = null,
    bool? UseCodeGraph = null,
    int? TopK = null);

/// <summary>Request for agentic execution with tools.</summary>
public record ExecuteAgenticRequest(
    string Prompt,
    string? WorkspacePath = null,
    bool? UseRag = null,
    bool? UseCodeGraph = null,
    int? MaxSteps = null);

// =============================================================================
// Conversation Requests
// =============================================================================

/// <summary>Request to create a conversation.</summary>
public record CreateConversationRequest(string AgentId, string? Title = null, string? WorkspacePath = null);

/// <summary>Request to add a message to a conversation.</summary>
public record AddMessageRequest(string Content);

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

// =============================================================================
// Developer Module Requests
// =============================================================================

/// <summary>Request to create a workflow.</summary>
public record CreateWorkflowRequest(
    string? Title = null,
    string? Description = null,
    string? RepositoryPath = null);

/// <summary>Request to execute a workflow step.</summary>
public record ExecuteStepRequest(string? AgentId = null);

/// <summary>Request to add a step to a workflow.</summary>
public record AddStepRequest(
    string Name,
    string Capability,
    string? Description = null,
    int? AfterOrder = null);

/// <summary>Request to reject a step.</summary>
public record RejectStepRequest(string? Feedback = null);

/// <summary>Request to skip a step.</summary>
public record SkipStepRequest(string? Reason = null);

/// <summary>Request to chat with a step.</summary>
public record StepChatRequest(string Message);

/// <summary>Request to reassign a step.</summary>
public record ReassignStepRequest(string AgentId);

/// <summary>Request to update step description.</summary>
public record UpdateStepDescriptionRequest(string Description);

/// <summary>Request to chat within a workflow.</summary>
public record WorkflowChatRequest(string Message);

/// <summary>Request to finalize a workflow.</summary>
public record FinalizeWorkflowRequest(
    string? CommitMessage = null,
    bool CreatePullRequest = true,
    string? PrTitle = null,
    string? PrBody = null,
    string? BaseBranch = null,
    bool Draft = true);

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
