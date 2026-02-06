// <copyright file="ProblemTypes.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Problems;

/// <summary>
/// RFC 7807 Problem Type URIs for the Aura API.
/// </summary>
public static class ProblemTypes
{
    private const string BaseUri = "https://aura.dev/problems/";

    // =========================================================================
    // Resource Not Found
    // =========================================================================

    /// <summary>Generic resource not found.</summary>
    public const string NotFound = BaseUri + "not-found";

    /// <summary>Story not found.</summary>
    public const string StoryNotFound = BaseUri + "story-not-found";

    /// <summary>Step not found.</summary>
    public const string StepNotFound = BaseUri + "step-not-found";

    /// <summary>Workspace not found.</summary>
    public const string WorkspaceNotFound = BaseUri + "workspace-not-found";

    /// <summary>Agent not found.</summary>
    public const string AgentNotFound = BaseUri + "agent-not-found";

    // =========================================================================
    // Validation Errors
    // =========================================================================

    /// <summary>Request validation failed.</summary>
    public const string ValidationFailed = BaseUri + "validation-failed";

    /// <summary>Resource is in an invalid state for the requested operation.</summary>
    public const string InvalidState = BaseUri + "invalid-state";

    /// <summary>Request is malformed or missing required fields.</summary>
    public const string InvalidRequest = BaseUri + "invalid-request";

    /// <summary>Invalid status value provided.</summary>
    public const string InvalidStatus = BaseUri + "invalid-status";

    // =========================================================================
    // Business Logic Errors
    // =========================================================================

    /// <summary>Story is not in a ready state for the operation.</summary>
    public const string StoryNotReady = BaseUri + "story-not-ready";

    /// <summary>Step has already been executed.</summary>
    public const string StepAlreadyExecuted = BaseUri + "step-already-executed";

    /// <summary>Git worktree creation failed.</summary>
    public const string WorktreeCreationFailed = BaseUri + "worktree-creation-failed";

    /// <summary>Code indexing is in progress.</summary>
    public const string IndexingInProgress = BaseUri + "indexing-in-progress";

    /// <summary>Story is not linked to an issue.</summary>
    public const string NotLinkedToIssue = BaseUri + "not-linked-to-issue";

    // =========================================================================
    // External Service Errors
    // =========================================================================

    /// <summary>LLM provider returned an error.</summary>
    public const string LlmProviderError = BaseUri + "llm-provider-error";

    /// <summary>Git operation failed.</summary>
    public const string GitOperationFailed = BaseUri + "git-operation-failed";

    /// <summary>GitHub API error.</summary>
    public const string GitHubError = BaseUri + "github-error";

    /// <summary>GitHub integration not configured.</summary>
    public const string GitHubNotConfigured = BaseUri + "github-not-configured";

    // =========================================================================
    // Rate Limiting / Quotas
    // =========================================================================

    /// <summary>Rate limit exceeded.</summary>
    public const string RateLimited = BaseUri + "rate-limited";

    /// <summary>Token budget exceeded.</summary>
    public const string TokenBudgetExceeded = BaseUri + "token-budget-exceeded";

    // =========================================================================
    // Internal Errors
    // =========================================================================

    /// <summary>Internal server error.</summary>
    public const string InternalError = BaseUri + "internal-error";
}
