using System.Text.Json;
using Aura.Api.Mcp.Tools;
using Aura.Api.Services;
using Aura.Foundation.Data.Entities;
using Aura.Foundation.Git;
using Aura.Foundation.Rag;
using Aura.Module.Developer.Data.Entities;
using Aura.Module.Developer.GitHub;
using Aura.Module.Developer.Services;
using Aura.Module.Developer.Services.Testing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.Extensions.Logging;
using RefactoringParameterInfo = Aura.Module.Developer.Services.ParameterInfo;

namespace Aura.Api.Mcp;

public sealed partial class McpHandler
{
    /// <summary>
    /// aura_workflow - Manage development workflows.
    /// Routes to: list, get, create, enrich, update_step, complete.
    /// </summary>
    private async Task<object> WorkflowAsync(JsonElement? args, CancellationToken ct)
    {
        var operation = args.GetRequiredString("operation");
        return operation switch
        {
            "list" => await ListStoriesAsync(args, ct),
            "get" => await GetStoryContextAsync(args, ct),
            "get_by_path" => await GetStoryByPathAsync(args, ct),
            "create" => await CreateStoryFromIssueAsync(args, ct),
            "enrich" => await EnrichStoryAsync(args, ct),
            "update_step" => await UpdateStepAsync(args, ct),
            "complete" => await CompleteStoryAsync(args, ct),
            "next_step" => await NextStepAsync(args, ct),
            "start_step" => await StartStepAsync(args, ct),
            "step_context" => await StepContextAsync(args, ct),
            _ => throw new ArgumentException($"Unknown workflow operation: {operation}")
        };
    }

    private async Task<object> ListStoriesAsync(JsonElement? args, CancellationToken ct)
    {
        // List active workflows (stories) - exclude completed/cancelled
        var workflows = await _storyService.ListAsync(ct: ct);
        return workflows.Where(w => w.Status != StoryStatus.Completed && w.Status != StoryStatus.Cancelled).Select(w => new { id = w.Id, title = w.Title, status = w.Status.ToString(), gitBranch = w.GitBranch, worktreePath = w.WorktreePath, repositoryPath = w.RepositoryPath, issueUrl = w.IssueUrl, issueNumber = w.IssueNumber, stepCount = w.Steps.Count, completedSteps = w.Steps.Count(s => s.Status == StepStatus.Completed), createdAt = w.CreatedAt });
    }

    private async Task<object> GetStoryContextAsync(JsonElement? args, CancellationToken ct)
    {
        var storyIdStr = args.GetStringOrDefault("storyId");
        if (!Guid.TryParse(storyIdStr, out var storyId))
        {
            return new
            {
                error = $"Invalid story ID: {storyIdStr}"
            };
        }

        var workflow = await _storyService.GetByIdWithStepsAsync(storyId, ct);
        if (workflow is null)
        {
            return new
            {
                error = $"Story not found: {storyId}"
            };
        }

        // Auto-load pattern content if story has a pattern
        string? patternContent = null;
        if (!string.IsNullOrWhiteSpace(workflow.PatternName))
        {
            patternContent = LoadPatternContent(workflow.PatternName, workflow.PatternLanguage);
        }

        return new
        {
            id = workflow.Id,
            title = workflow.Title,
            description = workflow.Description,
            status = workflow.Status.ToString(),
            issueUrl = workflow.IssueUrl,
            issueProvider = workflow.IssueProvider?.ToString(),
            issueNumber = workflow.IssueNumber,
            issueOwner = workflow.IssueOwner,
            issueRepo = workflow.IssueRepo,
            analyzedContext = workflow.AnalyzedContext,
            gitBranch = workflow.GitBranch,
            worktreePath = workflow.WorktreePath,
            repositoryPath = workflow.RepositoryPath,
            patternName = workflow.PatternName,
            patternLanguage = workflow.PatternLanguage,
            patternContent,
            steps = workflow.Steps.OrderBy(s => s.Order).Select(s => new { id = s.Id, name = s.Name, description = s.Description, status = s.Status.ToString(), order = s.Order }),
            createdAt = workflow.CreatedAt,
            updatedAt = workflow.UpdatedAt
        };
    }

    private async Task<object> GetStoryByPathAsync(JsonElement? args, CancellationToken ct)
    {
        var workspacePath = args.GetStringOrDefault("workspacePath");
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            return new
            {
                hasStory = false,
                message = "workspacePath is required"
            };
        }

        var normalizedPath = Path.GetFullPath(workspacePath);
        // First try exact match on worktree path
        var workflow = await _storyService.GetByWorktreePathAsync(normalizedPath, ct);
        // If not found, check if this is a worktree and try parent repo path
        if (workflow is null)
        {
            var worktreeInfo = GitWorktreeDetector.Detect(normalizedPath);
            if (worktreeInfo?.IsWorktree == true)
            {
                // Try the main repo path instead
                workflow = await _storyService.GetByWorktreePathAsync(worktreeInfo.Value.MainRepoPath, ct);
            }
        }

        if (workflow is null)
        {
            return new
            {
                hasStory = false,
                message = "No active story found for this workspace",
                checkedPath = normalizedPath
            };
        }

        // Auto-load pattern content if story has a pattern
        string? patternContent = null;
        if (!string.IsNullOrWhiteSpace(workflow.PatternName))
        {
            patternContent = LoadPatternContent(workflow.PatternName, workflow.PatternLanguage);
        }

        // Return full story context
        return new
        {
            hasStory = true,
            id = workflow.Id,
            title = workflow.Title,
            description = workflow.Description,
            status = workflow.Status.ToString(),
            issueUrl = workflow.IssueUrl,
            issueProvider = workflow.IssueProvider?.ToString(),
            issueNumber = workflow.IssueNumber,
            issueOwner = workflow.IssueOwner,
            issueRepo = workflow.IssueRepo,
            analyzedContext = workflow.AnalyzedContext,
            gitBranch = workflow.GitBranch,
            worktreePath = workflow.WorktreePath,
            repositoryPath = workflow.RepositoryPath,
            patternName = workflow.PatternName,
            patternLanguage = workflow.PatternLanguage,
            patternContent,
            currentStep = workflow.Steps.Where(s => s.Status == StepStatus.Pending || s.Status == StepStatus.Running).OrderBy(s => s.Order).Select(s => new { id = s.Id, name = s.Name, description = s.Description, order = s.Order }).FirstOrDefault(),
            steps = workflow.Steps.OrderBy(s => s.Order).Select(s => new { id = s.Id, name = s.Name, description = s.Description, status = s.Status.ToString(), order = s.Order }),
            createdAt = workflow.CreatedAt,
            updatedAt = workflow.UpdatedAt
        };
    }

    private async Task<object> CreateStoryFromIssueAsync(JsonElement? args, CancellationToken ct)
    {
        var issueUrl = args.GetStringOrDefault("issueUrl");
        if (string.IsNullOrEmpty(issueUrl))
        {
            return new
            {
                error = "issueUrl is required"
            };
        }

        var parsed = _gitHubService.ParseIssueUrl(issueUrl);
        if (parsed is null)
        {
            return new
            {
                error = "Invalid GitHub issue URL. Expected format: https://github.com/owner/repo/issues/123"
            };
        }

        if (!_gitHubService.IsConfigured)
        {
            return new
            {
                error = "GitHub integration not configured. Set GitHub:Token in appsettings.json"
            };
        }

        string? repositoryPath = null;
        if (args.HasValue && args.Value.TryGetProperty("repositoryPath", out var repoEl))
        {
            repositoryPath = repoEl.GetString();
        }

        try
        {
            // Fetch issue from GitHub
            var issue = await _gitHubService.GetIssueAsync(parsed.Value.Owner, parsed.Value.Repo, parsed.Value.Number, ct);
            // Create workflow/story
            var workflow = await _storyService.CreateAsync(
                issue.Title,
                issue.Body,
                repositoryPath,
                AutomationMode.Assisted, // MCP-created workflows default to assisted mode
                issueUrl,
                preferredExecutor: null,
                openQuestions: null,
                ct);
            // Post a comment to the issue that work has started
            var branch = workflow.GitBranch ?? "unknown";
            await _gitHubService.PostCommentAsync(parsed.Value.Owner, parsed.Value.Repo, parsed.Value.Number, $"Started work in branch `{branch}`", ct);

            // Check if the worktree branch already has commits ahead of the default branch
            string? branchAheadWarning = null;
            if (!string.IsNullOrEmpty(workflow.WorktreePath))
            {
                try
                {
                    var defaultBranchResult = await _gitService.GetDefaultBranchAsync(workflow.WorktreePath, ct);
                    if (defaultBranchResult.Success && defaultBranchResult.Value is not null)
                    {
                        var countResult = await _gitService.CountCommitsSinceAsync(
                            workflow.WorktreePath, defaultBranchResult.Value, ct);
                        if (countResult.Success && countResult.Value > 0)
                        {
                            branchAheadWarning = $"⚠️ Branch '{branch}' already has {countResult.Value} commit(s) ahead of '{defaultBranchResult.Value}'. " +
                                "This branch may contain work from a previous session. Review existing changes before starting new work.";
                            _logger.LogWarning("Branch {Branch} has {Count} commits ahead of {DefaultBranch} — pre-existing work detected",
                                branch, countResult.Value, defaultBranchResult.Value);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to check branch-ahead status for worktree {Path}", workflow.WorktreePath);
                }
            }

            return new
            {
                id = workflow.Id,
                title = workflow.Title,
                description = workflow.Description,
                status = workflow.Status.ToString(),
                gitBranch = workflow.GitBranch,
                worktreePath = workflow.WorktreePath,
                issueUrl = workflow.IssueUrl,
                issueNumber = workflow.IssueNumber,
                createdAt = workflow.CreatedAt,
                warning = branchAheadWarning
            };
        }
        catch (HttpRequestException ex)
        {
            return new
            {
                error = $"Failed to fetch issue from GitHub: {ex.Message}"
            };
        }
    }

    private async Task<object> EnrichStoryAsync(JsonElement? args, CancellationToken ct)
    {
        var storyIdStr = args.GetStringOrDefault("storyId");
        if (!Guid.TryParse(storyIdStr, out var storyId))
        {
            return new
            {
                error = $"Invalid story ID: {storyIdStr}"
            };
        }

        var workflow = await _storyService.GetByIdWithStepsAsync(storyId, ct);
        if (workflow is null)
        {
            return new
            {
                error = $"Story not found: {storyId}"
            };
        }

        // Check for pattern and language parameters
        string? patternName = null;
        string? patternLanguage = null;
        string? patternContent = null;
        if (args.HasValue && args.Value.TryGetProperty("pattern", out var patternEl))
        {
            patternName = patternEl.GetString();
        }

        if (args.HasValue && args.Value.TryGetProperty("language", out var langEl))
        {
            patternLanguage = langEl.GetString();
        }

        // Load pattern content using tiered loading (base + overlay)
        if (!string.IsNullOrWhiteSpace(patternName))
        {
            patternContent = LoadPatternContent(patternName, patternLanguage);
            if (patternContent is null)
            {
                return new
                {
                    error = $"Pattern '{patternName}' not found. Use aura_pattern(operation: 'list') to see available patterns.",
                    storyId
                };
            }
        }

        // If pattern provided but no steps, return pattern content for agent to parse steps from
        JsonElement stepsEl = default;
        var hasSteps = args.HasValue && args.Value.TryGetProperty("steps", out stepsEl) && stepsEl.ValueKind == JsonValueKind.Array;
        if (!string.IsNullOrEmpty(patternContent) && !hasSteps)
        {
            // Store pattern name and language on the workflow for future reference
            if (workflow.PatternName != patternName || workflow.PatternLanguage != patternLanguage)
            {
                workflow.PatternName = patternName;
                workflow.PatternLanguage = patternLanguage;
                await _storyService.UpdateAsync(workflow, ct);
            }

            // Return pattern content - agent should parse steps and call enrich again with steps array
            return new
            {
                storyId,
                patternName,
                patternLanguage,
                patternContent,
                message = "Pattern loaded and bound to story. Parse the steps from the pattern content and call enrich again with the steps array.",
                hint = "Look for numbered steps, checkboxes (- [ ]), or ### Step headers in the pattern markdown."
            };
        }

        if (!hasSteps)
        {
            return new
            {
                error = "Either 'pattern' or 'steps' array is required for enrich operation"
            };
        }

        var addedSteps = new List<object>();
        foreach (var stepEl in stepsEl.EnumerateArray())
        {
            var name = stepEl.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "";
            var capability = stepEl.TryGetProperty("capability", out var capEl) ? capEl.GetString() ?? "" : "";
            var description = stepEl.TryGetProperty("description", out var descEl) ? descEl.GetString() : null;
            string? input = null;
            if (stepEl.TryGetProperty("input", out var inputEl))
            {
                input = inputEl.ValueKind == JsonValueKind.String ? inputEl.GetString() : inputEl.GetRawText();
            }

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(capability))
            {
                continue; // Skip invalid steps
            }

            var step = await _storyService.AddStepAsync(storyId, name, capability, description, input, ct: ct);
            addedSteps.Add(new { id = step.Id, name = step.Name, capability = step.Capability, description = step.Description, order = step.Order, status = step.Status.ToString() });
        }

        // If pattern was provided, save it on the workflow
        if (!string.IsNullOrWhiteSpace(patternName) && workflow.PatternName != patternName)
        {
            workflow.PatternName = patternName;
            await _storyService.UpdateAsync(workflow, ct);
        }

        return new
        {
            storyId,
            stepsAdded = addedSteps.Count,
            patternName,
            steps = addedSteps,
            message = $"Added {addedSteps.Count} steps to story" + (patternName != null ? $" (pattern: {patternName})" : "")
        };
    }

    private async Task<object> UpdateStepAsync(JsonElement? args, CancellationToken ct)
    {
        var storyIdStr = args.GetStringOrDefault("storyId");
        if (!Guid.TryParse(storyIdStr, out var storyId))
        {
            return new
            {
                error = "storyId is required and must be a valid GUID"
            };
        }

        var stepIdStr = args.GetStringOrDefault("stepId");
        if (!Guid.TryParse(stepIdStr, out var stepId))
        {
            return new
            {
                error = "stepId is required and must be a valid GUID"
            };
        }

        var statusStr = args.GetStringOrDefault("status").ToLowerInvariant();
        if (string.IsNullOrEmpty(statusStr))
        {
            return new
            {
                error = "status is required"
            };
        }

        var workflow = await _storyService.GetByIdWithStepsAsync(storyId, ct);
        if (workflow is null)
        {
            return new
            {
                error = $"Story not found: {storyId}"
            };
        }

        var step = workflow.Steps.FirstOrDefault(s => s.Id == stepId);
        if (step is null)
        {
            return new
            {
                error = $"Step not found: {stepId}"
            };
        }

        string? output = null;
        string? error = null;
        string? skipReason = null;
        if (args.HasValue)
        {
            if (args.Value.TryGetProperty("output", out var outputEl))
                output = outputEl.GetString();
            if (args.Value.TryGetProperty("error", out var errorEl))
                error = errorEl.GetString();
            if (args.Value.TryGetProperty("skipReason", out var skipEl))
                skipReason = skipEl.GetString();
        }

        StoryStep updatedStep;
        switch (statusStr)
        {
            case "completed":
                step.Status = StepStatus.Completed;
                step.Output = output;
                step.CompletedAt = DateTimeOffset.UtcNow;
                await _storyService.UpdateStepAsync(step, ct);
                updatedStep = step;
                break;
            case "failed":
                step.Status = StepStatus.Failed;
                step.Error = error ?? "Step marked as failed";
                await _storyService.UpdateStepAsync(step, ct);
                updatedStep = step;
                break;
            case "skipped":
                updatedStep = await _storyService.SkipStepAsync(storyId, stepId, skipReason, ct);
                break;
            case "pending":
                updatedStep = await _storyService.ResetStepAsync(storyId, stepId, ct);
                break;
            default:
                return new
                {
                    error = $"Unknown status: {statusStr}. Valid values: completed, failed, skipped, pending"
                };
        }

        return new
        {
            stepId = updatedStep.Id,
            name = updatedStep.Name,
            status = updatedStep.Status.ToString(),
            output = updatedStep.Output,
            error = updatedStep.Error,
            skipReason = updatedStep.SkipReason,
            message = $"Step status updated to {updatedStep.Status}"
        };
    }

    /// <summary>
    /// Complete a workflow/story: validates all steps are done, squash merges commits, pushes branch, creates draft PR.
    /// </summary>
    private async Task<object> CompleteStoryAsync(JsonElement? args, CancellationToken ct)
    {
        var storyIdStr = args.GetStringOrDefault("storyId");
        if (!Guid.TryParse(storyIdStr, out var storyId))
        {
            return new
            {
                error = "storyId is required and must be a valid GUID"
            };
        }

        // Get GitHub token if provided, fall back to request-scoped token from middleware
        string? githubToken = null;
        if (args?.TryGetProperty("githubToken", out var tokenProp) == true)
        {
            githubToken = tokenProp.GetString();
        }

        githubToken ??= _tokenAccessor.Token;

        try
        {
            var workflow = await _storyService.CompleteAsync(storyId, githubToken, ct);

            // Post a comment to the linked issue with the PR link
            if (workflow.PullRequestUrl is not null
                && workflow.IssueNumber.HasValue
                && !string.IsNullOrEmpty(workflow.IssueOwner)
                && !string.IsNullOrEmpty(workflow.IssueRepo)
                && _gitHubService.IsConfigured)
            {
                try
                {
                    var comment = $"🤖 Pull request created: {workflow.PullRequestUrl}";
                    await _gitHubService.PostCommentAsync(
                        workflow.IssueOwner,
                        workflow.IssueRepo,
                        workflow.IssueNumber.Value,
                        comment,
                        ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to post PR comment to issue #{IssueNumber}", workflow.IssueNumber);
                }
            }

            return new
            {
                storyId = workflow.Id,
                title = workflow.Title,
                status = workflow.Status.ToString(),
                completedAt = workflow.CompletedAt,
                gitBranch = workflow.GitBranch,
                pullRequestUrl = workflow.PullRequestUrl,
                message = "Workflow completed successfully" + (workflow.PullRequestUrl is not null ? $". Draft PR created: {workflow.PullRequestUrl}" : "")
            };
        }
        catch (InvalidOperationException ex)
        {
            return new
            {
                error = ex.Message,
                storyId,
                hint = "Ensure all steps are completed or skipped before completing the workflow."
            };
        }
    }

    /// <summary>
    /// Get the next actionable step for a story (first Pending step in the lowest incomplete wave).
    /// </summary>
    private async Task<object> NextStepAsync(JsonElement? args, CancellationToken ct)
    {
        var storyIdStr = args.GetStringOrDefault("storyId");
        if (!Guid.TryParse(storyIdStr, out var storyId))
        {
            return new { error = "storyId is required and must be a valid GUID" };
        }

        var workflow = await _storyService.GetByIdWithStepsAsync(storyId, ct);
        if (workflow is null)
        {
            return new { error = $"Story not found: {storyId}" };
        }

        var nextStep = workflow.Steps
            .Where(s => s.Status == StepStatus.Pending)
            .OrderBy(s => s.Wave)
            .ThenBy(s => s.Order)
            .FirstOrDefault();

        if (nextStep is null)
        {
            var allDone = workflow.Steps.All(s => s.Status is StepStatus.Completed or StepStatus.Skipped);
            return new
            {
                storyId,
                message = allDone
                    ? "All steps are complete. Use aura_workflow(operation: 'complete') to finalize and create a PR."
                    : "No pending steps found. Some steps may have failed — use update_step to reset them to pending.",
                allStepsComplete = allDone,
                steps = workflow.Steps.OrderBy(s => s.Order).Select(s => new { id = s.Id, name = s.Name, status = s.Status.ToString() })
            };
        }

        return BuildStepContext(workflow, nextStep);
    }

    /// <summary>
    /// Mark a step as Running and return its full context. Combines status update + context retrieval.
    /// </summary>
    private async Task<object> StartStepAsync(JsonElement? args, CancellationToken ct)
    {
        var storyIdStr = args.GetStringOrDefault("storyId");
        if (!Guid.TryParse(storyIdStr, out var storyId))
        {
            return new { error = "storyId is required and must be a valid GUID" };
        }

        var stepIdStr = args.GetStringOrDefault("stepId");
        if (!Guid.TryParse(stepIdStr, out var stepId))
        {
            return new { error = "stepId is required and must be a valid GUID" };
        }

        var workflow = await _storyService.GetByIdWithStepsAsync(storyId, ct);
        if (workflow is null)
        {
            return new { error = $"Story not found: {storyId}" };
        }

        var step = workflow.Steps.FirstOrDefault(s => s.Id == stepId);
        if (step is null)
        {
            return new { error = $"Step not found: {stepId}" };
        }

        if (step.Status is not (StepStatus.Pending or StepStatus.Failed))
        {
            return new
            {
                error = $"Step is {step.Status} — can only start Pending or Failed steps",
                stepId = step.Id,
                currentStatus = step.Status.ToString()
            };
        }

        step.Status = StepStatus.Running;
        step.StartedAt = DateTimeOffset.UtcNow;
        step.Attempts++;
        await _storyService.UpdateStepAsync(step, ct);

        return BuildStepContext(workflow, step);
    }

    /// <summary>
    /// Get rich context for a specific step without changing its status (read-only).
    /// </summary>
    private async Task<object> StepContextAsync(JsonElement? args, CancellationToken ct)
    {
        var storyIdStr = args.GetStringOrDefault("storyId");
        if (!Guid.TryParse(storyIdStr, out var storyId))
        {
            return new { error = "storyId is required and must be a valid GUID" };
        }

        var stepIdStr = args.GetStringOrDefault("stepId");
        if (!Guid.TryParse(stepIdStr, out var stepId))
        {
            return new { error = "stepId is required and must be a valid GUID" };
        }

        var workflow = await _storyService.GetByIdWithStepsAsync(storyId, ct);
        if (workflow is null)
        {
            return new { error = $"Story not found: {storyId}" };
        }

        var step = workflow.Steps.FirstOrDefault(s => s.Id == stepId);
        if (step is null)
        {
            return new { error = $"Step not found: {stepId}" };
        }

        return BuildStepContext(workflow, step);
    }

    /// <summary>
    /// Builds rich step context including worktree paths, prior step outputs, and story analysis.
    /// Shared by next_step, start_step, and step_context operations.
    /// </summary>
    private static object BuildStepContext(Story workflow, StoryStep step)
    {
        var completedSteps = workflow.Steps
            .Where(s => s.Status == StepStatus.Completed)
            .OrderBy(s => s.Order)
            .ToList();

        // Collect outputs from completed steps in earlier waves (or same wave, lower order)
        var priorOutputs = completedSteps
            .Where(s => s.Wave < step.Wave || (s.Wave == step.Wave && s.Order < step.Order))
            .Select(s => new { stepName = s.Name, output = s.Output })
            .ToList();

        var totalSteps = workflow.Steps.Count;
        var completedCount = completedSteps.Count;
        var skippedCount = workflow.Steps.Count(s => s.Status == StepStatus.Skipped);
        var maxWave = workflow.Steps.Max(s => s.Wave);

        // Compute worktree solution path by finding .sln file pattern
        string? worktreeSolutionPath = null;
        if (!string.IsNullOrEmpty(workflow.WorktreePath) && !string.IsNullOrEmpty(workflow.RepositoryPath))
        {
            // Look for .sln files relative to repository, map to worktree
            var repoDir = new DirectoryInfo(workflow.RepositoryPath);
            var slnFiles = repoDir.Exists
                ? repoDir.GetFiles("*.sln", SearchOption.TopDirectoryOnly)
                : [];
            if (slnFiles.Length > 0)
            {
                worktreeSolutionPath = Path.Combine(workflow.WorktreePath, slnFiles[0].Name);
            }
        }

        return new
        {
            stepId = step.Id,
            name = step.Name,
            description = step.Description,
            capability = step.Capability,
            language = step.Language,
            status = step.Status.ToString(),
            wave = step.Wave,
            order = step.Order,
            input = step.Input,
            worktreePath = workflow.WorktreePath,
            worktreeSolutionPath,
            repositoryPath = workflow.RepositoryPath,
            gitBranch = workflow.GitBranch,
            storyId = workflow.Id,
            storyTitle = workflow.Title,
            storyDescription = workflow.Description,
            analysis = workflow.AnalyzedContext,
            priorStepOutputs = priorOutputs,
            progress = $"{completedCount + skippedCount}/{totalSteps} steps done (Wave {step.Wave}/{maxWave})",
            allSteps = workflow.Steps.OrderBy(s => s.Order).Select(s => new
            {
                id = s.Id,
                name = s.Name,
                status = s.Status.ToString(),
                wave = s.Wave,
                order = s.Order
            })
        };
    }
}
