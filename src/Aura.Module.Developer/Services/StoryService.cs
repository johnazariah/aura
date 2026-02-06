// <copyright file="StoryService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services;

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aura.Foundation.Agents;
using Aura.Foundation.Git;
using Aura.Foundation.Llm;
using Aura.Foundation.Mcp;
using Aura.Foundation.Prompts;
using Aura.Foundation.Rag;
using Aura.Foundation.Tools;
using Aura.Module.Developer.Data;
using Aura.Module.Developer.Data.Entities;
using Aura.Module.Developer.Services.Verification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Service for managing development workflows.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="StoryService"/> class.
/// </remarks>
public sealed partial class StoryService(
    DeveloperDbContext db,
    IAgentRegistry agentRegistry,
    IPromptRegistry promptRegistry,
    IGitWorktreeService worktreeService,
    IGitService gitService,
    IRagService ragService,
    IBackgroundIndexer backgroundIndexer,
    ICodebaseContextService codebaseContextService,
    IToolRegistry toolRegistry,
    IReActExecutor reactExecutor,
    ILlmProviderRegistry llmProviderRegistry,
    IStoryVerificationService verificationService,
    IStepExecutorRegistry stepExecutorRegistry,
    IQualityGateService qualityGateService,
    IOptions<DeveloperModuleOptions> options,
    ILogger<StoryService> logger) : IStoryService
{
    private readonly DeveloperDbContext _db = db;
    private readonly IAgentRegistry _agentRegistry = agentRegistry;
    private readonly IPromptRegistry _promptRegistry = promptRegistry;
    private readonly IGitWorktreeService _worktreeService = worktreeService;
    private readonly IGitService _gitService = gitService;
    private readonly IRagService _ragService = ragService;
    private readonly IBackgroundIndexer _backgroundIndexer = backgroundIndexer;
    private readonly ICodebaseContextService _codebaseContextService = codebaseContextService;
    private readonly IToolRegistry _toolRegistry = toolRegistry;
    private readonly IReActExecutor _reactExecutor = reactExecutor;
    private readonly ILlmProviderRegistry _llmProviderRegistry = llmProviderRegistry;
    private readonly IStoryVerificationService _verificationService = verificationService;
    private readonly IStepExecutorRegistry _stepExecutorRegistry = stepExecutorRegistry;
    private readonly IQualityGateService _qualityGateService = qualityGateService;
    private readonly DeveloperModuleOptions _options = options.Value;
    private readonly ILogger<StoryService> _logger = logger;

    /// <inheritdoc/>
    public async Task<Story> CreateAsync(
        string title,
        string? description = null,
        string? repositoryPath = null,
        AutomationMode automationMode = AutomationMode.Assisted,
        string? issueUrl = null,
        string? preferredExecutor = null,
        IReadOnlyList<string>? openQuestions = null,
        CancellationToken ct = default)
    {
        // Create a branch name from the title
        var sanitizedTitle = new string(title
            .ToLowerInvariant()
            .Replace(' ', '-')
            .Where(c => char.IsLetterOrDigit(c) || c == '-')
            .ToArray());
        var prefix = _options.BranchPrefix.TrimEnd('/');
        var branchName = $"{prefix}/{sanitizedTitle}-{Guid.NewGuid():N}"[..Math.Min(63, prefix.Length + 1 + sanitizedTitle.Length + 33)];

        // Convert open questions to JSON
        string? openQuestionsJson = null;
        if (openQuestions is { Count: > 0 })
        {
            var questions = openQuestions.Select(q => new OpenQuestion
            {
                Question = q,
                Answered = false,
            }).ToList();
            openQuestionsJson = System.Text.Json.JsonSerializer.Serialize(questions);
        }

        // Create the workflow
        var workflow = new Story
        {
            Id = Guid.NewGuid(),
            RepositoryPath = repositoryPath,
            Title = title,
            Description = description,
            GitBranch = branchName,
            Status = StoryStatus.Created,
            AutomationMode = automationMode,
            PreferredExecutor = preferredExecutor,
            OpenQuestions = openQuestionsJson,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        // Parse issue URL if provided
        if (!string.IsNullOrEmpty(issueUrl))
        {
            var parsed = ParseGitHubIssueUrl(issueUrl);
            if (parsed is not null)
            {
                workflow.IssueUrl = issueUrl;
                workflow.IssueProvider = IssueProvider.GitHub;
                workflow.IssueOwner = parsed.Value.owner;
                workflow.IssueRepo = parsed.Value.repo;
                workflow.IssueNumber = parsed.Value.number;
            }
        }

        // Try to create a worktree if repository path is set
        if (!string.IsNullOrEmpty(repositoryPath))
        {
            var worktreeResult = await _worktreeService.CreateAsync(
                repositoryPath,
                branchName,
                worktreePath: null, // Let it choose default
                baseBranch: null,   // Use current HEAD
                ct);

            if (worktreeResult.Success && worktreeResult.Value is not null)
            {
                workflow.WorktreePath = worktreeResult.Value.Path;
                _logger.LogInformation("Created worktree at {Path} for workflow {WorkflowId}",
                    workflow.WorktreePath, workflow.Id);

                // Check if the repository is already indexed - if so, skip worktree indexing.
                // This is safe because:
                //   1. RAG queries use RepositoryPath (not WorktreePath) as the source filter
                //   2. Agents use direct file tools to read/write in the worktree
                //   3. Step outputs are passed to subsequent steps via the prompt
                var repoStats = await _ragService.GetDirectoryStatsAsync(repositoryPath, ct);
                if (repoStats is not null && repoStats.FileCount > 0)
                {
                    _logger.LogInformation(
                        "Repository {RepoPath} already indexed ({FileCount} files, {ChunkCount} chunks). Skipping worktree indexing.",
                        repositoryPath, repoStats.FileCount, repoStats.ChunkCount);
                }
                else
                {
                    // Repository not indexed - queue worktree for background RAG indexing (non-blocking)
                    var (jobId, isNew) = _backgroundIndexer.QueueDirectory(workflow.WorktreePath, new RagIndexOptions
                    {
                        IncludePatterns = new[] { "*.cs", "*.md", "*.json", "*.yaml", "*.yml", "*.ts", "*.tsx", "*.js", "*.jsx" },
                        ExcludePatterns = new[] { "**/bin/**", "**/obj/**", "**/node_modules/**", "**/.git/**" },
                        Recursive = true,
                    });
                    _logger.LogInformation("{Action} worktree indexing job {JobId} for workflow {WorkflowId}",
                        isNew ? "Queued" : "Reusing", jobId, workflow.Id);
                }
            }
            else
            {
                _logger.LogWarning("Failed to create worktree: {Error}. Using repository path instead.",
                    worktreeResult.Error);
                workflow.WorktreePath = repositoryPath;
            }

            // Set up Copilot/MCP configuration in the worktree
            if (!string.IsNullOrEmpty(workflow.WorktreePath) && workflow.WorktreePath != repositoryPath)
            {
                await SetupWorktreeCopilotConfigAsync(repositoryPath, workflow.WorktreePath, ct);
            }
        }

        _db.Stories.Add(workflow);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created workflow {WorkflowId}: {Title}", workflow.Id, title);
        return workflow;
    }

    /// <inheritdoc/>
    public async Task<Story> CreateFromGuardianAsync(
        GuardianWorkflowRequest request,
        CancellationToken ct = default)
    {
        // Create the workflow with guardian source
        var workflow = await CreateAsync(
            request.Title,
            request.Description,
            request.RepositoryPath,
            AutomationMode.Assisted,
            issueUrl: null,
            preferredExecutor: null,
            openQuestions: null,
            ct);

        // Set guardian-specific fields
        workflow.Source = StorySource.Guardian;
        workflow.SourceGuardianId = request.GuardianId;
        workflow.Priority = request.Priority;
        workflow.SuggestedCapability = request.SuggestedCapability;

        // Store additional context from the guardian if provided
        if (!string.IsNullOrEmpty(request.Context))
        {
            workflow.AnalyzedContext = request.Context;
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Created guardian workflow {WorkflowId} from {GuardianId}: {Title} (Priority: {Priority})",
            workflow.Id,
            request.GuardianId,
            request.Title,
            request.Priority);

        return workflow;
    }

    /// <inheritdoc/>
    public async Task<Story?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Stories.FindAsync([id], ct);
    }

    /// <inheritdoc/>
    public async Task<Story?> GetByIdWithStepsAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Stories
            .Include(w => w.Steps.OrderBy(s => s.Order))
            .FirstOrDefaultAsync(w => w.Id == id, ct);
    }

    /// <inheritdoc/>
    public async Task<Story?> GetByWorktreePathAsync(string worktreePath, CancellationToken ct = default)
    {
        // Normalize path for cross-platform comparison
        var normalizedPath = Aura.Foundation.Rag.PathNormalizer.Normalize(worktreePath);
        return await _db.Stories
            .Include(w => w.Steps.OrderBy(s => s.Order))
            .FirstOrDefaultAsync(w => w.WorktreePath != null &&
                EF.Functions.ILike(
                    w.WorktreePath.Replace("\\", "/").ToLower(),
                    normalizedPath), ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Story>> ListAsync(StoryStatus? status = null, string? repositoryPath = null, CancellationToken ct = default)
    {
        var query = _db.Stories.AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(w => w.Status == status.Value);
        }

        if (!string.IsNullOrEmpty(repositoryPath))
        {
            // Use PathNormalizer for consistent cross-platform path comparison
            var normalizedPath = Aura.Foundation.Rag.PathNormalizer.Normalize(repositoryPath);
            query = query.Where(w => w.RepositoryPath != null &&
                EF.Functions.ILike(
                    w.RepositoryPath.Replace("\\", "/").ToLower(),
                    normalizedPath));
        }

        return await query
            .OrderByDescending(w => w.CreatedAt)
            .Include(w => w.Steps.OrderBy(s => s.Order))
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var workflow = await _db.Stories
            .Include(w => w.Steps)
            .FirstOrDefaultAsync(w => w.Id == id, ct);

        if (workflow is null)
        {
            return;
        }

        // Clean up worktree if it exists
        if (!string.IsNullOrEmpty(workflow.WorktreePath) &&
            !string.IsNullOrEmpty(workflow.RepositoryPath) &&
            workflow.WorktreePath != workflow.RepositoryPath)
        {
            var removeResult = await _worktreeService.RemoveAsync(
                workflow.WorktreePath,
                force: true,
                ct);

            if (!removeResult.Success)
            {
                _logger.LogWarning("Failed to remove worktree {Path}: {Error}",
                    workflow.WorktreePath, removeResult.Error);
            }
        }

        // Clean up the workflow branch if it exists
        if (!string.IsNullOrEmpty(workflow.GitBranch) &&
            !string.IsNullOrEmpty(workflow.RepositoryPath))
        {
            var deleteBranchResult = await _gitService.DeleteBranchAsync(
                workflow.RepositoryPath,
                workflow.GitBranch,
                force: true,
                ct);

            if (!deleteBranchResult.Success)
            {
                _logger.LogWarning("Failed to delete branch {Branch}: {Error}",
                    workflow.GitBranch, deleteBranchResult.Error);
            }
        }

        _db.Stories.Remove(workflow);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted workflow {WorkflowId}: {Title}", id, workflow.Title);
    }

    /// <inheritdoc/>
    public async Task<Story> ResetStatusAsync(Guid workflowId, StoryStatus newStatus, CancellationToken ct = default)
    {
        var workflow = await _db.Stories
            .FirstOrDefaultAsync(w => w.Id == workflowId, ct)
            ?? throw new InvalidOperationException($"Workflow {workflowId} not found");

        var oldStatus = workflow.Status;
        workflow.Status = newStatus;
        workflow.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Reset workflow {WorkflowId} status from {OldStatus} to {NewStatus}",
            workflowId, oldStatus, newStatus);

        return workflow;
    }

    /// <inheritdoc/>
    public async Task<Story> ResetOrchestratorAsync(Guid workflowId, bool resetFailedSteps = false, CancellationToken ct = default)
    {
        var story = await _db.Stories
            .Include(w => w.Steps)
            .FirstOrDefaultAsync(w => w.Id == workflowId, ct)
            ?? throw new InvalidOperationException($"Story {workflowId} not found");

        if (story.Status != StoryStatus.Failed && story.Status != StoryStatus.GateFailed)
        {
            throw new InvalidOperationException($"Can only reset orchestrator when status is Failed or GateFailed. Current status: {story.Status}");
        }

        // Reset to GatePending so /run will retry the quality gate
        story.Status = StoryStatus.GatePending;
        story.GateResult = null;
        story.UpdatedAt = DateTimeOffset.UtcNow;

        // Optionally reset failed steps in the current wave
        if (resetFailedSteps)
        {
            var currentWave = story.CurrentWave == 0 ? 1 : story.CurrentWave;
            var failedSteps = story.Steps
                .Where(s => s.Wave == currentWave && s.Status == StepStatus.Failed)
                .ToList();

            foreach (var step in failedSteps)
            {
                step.Status = StepStatus.Pending;
                step.Error = null;
                step.StartedAt = null;
                step.CompletedAt = null;
            }

            if (failedSteps.Count > 0)
            {
                _logger.LogInformation("Reset {Count} failed steps in wave {Wave} to Pending", failedSteps.Count, currentWave);
            }
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Reset orchestrator for story {StoryId} from {PreviousStatus} to GatePending", workflowId, story.Status);

        return story;
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(Story workflow, CancellationToken ct = default)
    {
        _db.Stories.Update(workflow);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Updated workflow {WorkflowId}: {Title}", workflow.Id, workflow.Title);
    }

    /// <inheritdoc/>
    public async Task UpdateStepAsync(StoryStep step, CancellationToken ct = default)
    {
        _db.Entry(step).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Updated step {StepId}: {Name} -> {Status}", step.Id, step.Name, step.Status);
    }

    private static List<TaskDecompositionDto> ParseTaskDecompositionFromResponse(string response)
    {
        // Extract JSON array from response (handle markdown code blocks)
        var json = response.Trim();
        if (json.StartsWith("```", StringComparison.Ordinal))
        {
            var startIndex = json.IndexOf('[');
            var endIndex = json.LastIndexOf(']');
            if (startIndex >= 0 && endIndex > startIndex)
            {
                json = json[startIndex..(endIndex + 1)];
            }
        }

        try
        {
            var taskDtos = JsonSerializer.Deserialize<List<TaskDecompositionDto>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

            return taskDtos ?? [];
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse decomposition response as JSON: {ex.Message}", ex);
        }
    }

    private record TaskDecompositionDto(
        string? Id,
        string? Title,
        string? Description,
        int Wave,
        string[]? DependsOn);

    /// <inheritdoc/>
    public async IAsyncEnumerable<StoryProgressEvent> RunStreamAsync(
        Guid storyId,
        string? githubToken = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var story = await _db.Stories
            .Include(w => w.Steps)
            .FirstOrDefaultAsync(w => w.Id == storyId, ct)
            ?? throw new InvalidOperationException($"Story {storyId} not found");

        if (!story.Steps.Any())
        {
            throw new InvalidOperationException("Story has no steps. Call PlanAsync or DecomposeAsync first.");
        }

        if (string.IsNullOrEmpty(story.WorktreePath))
        {
            throw new InvalidOperationException("Story has no worktree. Create a worktree first.");
        }

        var steps = story.Steps.ToList();
        var waveCount = steps.Max(s => s.Wave);

        yield return StoryProgressEvent.Started(storyId, waveCount);

        var currentWave = story.CurrentWave == 0 ? 1 : story.CurrentWave;

        // Loop through all waves
        while (currentWave <= waveCount && !ct.IsCancellationRequested)
        {
            // Handle GatePending from previous wave
            if (story.Status == StoryStatus.GatePending)
            {
                // Skip quality gate for intermediate waves
                // Intermediate waves may have partial implementations that don't compile
                // (e.g., wave 1 updates interfaces, wave 2 updates implementations)
                // The quality gate will run after the final wave completes
                _logger.LogInformation("Proceeding past gate for wave {Wave}/{TotalWaves} - will validate after final wave", currentWave - 1, waveCount);
                // Proceed to execute the current wave (no gate check needed)
            }

            // Get pending steps for this wave
            var waveSteps = steps
                .Where(s => s.Wave == currentWave && s.Status == StepStatus.Pending)
                .ToList();

            if (waveSteps.Count == 0)
            {
                // Check if all steps complete
                if (steps.All(s => s.Status == StepStatus.Completed || s.Status == StepStatus.Skipped))
                {
                    story.Status = StoryStatus.ReadyToComplete;
                    story.UpdatedAt = DateTimeOffset.UtcNow;
                    await _db.SaveChangesAsync(ct);

                    yield return StoryProgressEvent.ReadyToComplete(storyId, waveCount);
                    yield break;
                }

                currentWave++;
                continue;
            }

            yield return StoryProgressEvent.WaveStarted(storyId, currentWave, waveCount);

            story.Status = StoryStatus.Executing;
            story.CurrentWave = currentWave;
            story.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Streaming wave {Wave}/{TotalWaves} with {StepCount} steps",
                currentWave, waveCount, waveSteps.Count);

            // Emit step started events for all steps in this wave
            foreach (var step in waveSteps)
            {
                yield return StoryProgressEvent.StepStarted(storyId, step.Id, step.Name, currentWave);
            }

            // Get completed steps from prior waves to provide context
            var priorCompletedSteps = steps
                .Where(s => s.Wave < currentWave && s.Status == StepStatus.Completed)
                .ToList();

            // Resolve executor for this wave (use first step to resolve, all steps in wave use same executor)
            var worktreeName = Path.GetFileName(story.WorktreePath);
            var executor = await _stepExecutorRegistry.ResolveExecutorAsync(waveSteps[0], story, ct)
                ?? throw new InvalidOperationException("No executor available for story");

            _logger.LogInformation(
                "[{WorktreeName}] Using {Executor} for wave {Wave}",
                worktreeName,
                executor.DisplayName,
                currentWave);

            await executor.ExecuteStepsAsync(
                waveSteps,
                story,
                story.MaxParallelism,
                priorCompletedSteps,
                ct);

            // Emit events for step results
            var completedCount = 0;
            var failedCount = 0;

            foreach (var step in waveSteps)
            {
                if (step.Status == StepStatus.Completed)
                {
                    completedCount++;
                    yield return StoryProgressEvent.StepCompleted(storyId, step.Id, step.Name, step.Output);
                }
                else if (step.Status == StepStatus.Failed)
                {
                    failedCount++;
                    yield return StoryProgressEvent.StepFailed(storyId, step.Id, step.Name, step.Error);
                }
            }

            await _db.SaveChangesAsync(ct);

            yield return StoryProgressEvent.WaveCompleted(storyId, currentWave, completedCount, failedCount);

            if (failedCount > 0)
            {
                story.Status = StoryStatus.Failed;
                story.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);

                yield return StoryProgressEvent.Failed(storyId, currentWave, $"{failedCount} step(s) failed in wave {currentWave}");
                yield break;
            }

            // Move to next wave with gate check
            if (currentWave < waveCount)
            {
                _logger.LogInformation("Skipping quality gate for intermediate wave {Wave}/{TotalWaves} - will validate after final wave", currentWave, waveCount);
                story.Status = StoryStatus.GatePending;
                story.CurrentWave = currentWave + 1;
                story.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);
            }

            currentWave++;
        }

        // All waves complete - run quality gate before marking ready
        _logger.LogInformation("All waves complete, running final quality gate");
        yield return StoryProgressEvent.GateStarted(storyId, waveCount);

        var gateResult = await _qualityGateService.RunFullGateAsync(story.WorktreePath, waveCount, ct);
        if (!gateResult.Passed)
        {
            if (gateResult.WasCancelled)
            {
                _logger.LogWarning("Quality gate was cancelled after final wave {Wave}. Story remains pending for retry.", waveCount);
                story.Status = StoryStatus.GatePending;
                story.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);
                yield return StoryProgressEvent.GateFailed(storyId, waveCount, gateResult);
                yield break;
            }

            _logger.LogWarning("Quality gate failed after final wave {Wave}: {Error}", waveCount, gateResult.Error);
            story.Status = StoryStatus.GateFailed;
            story.GateResult = JsonSerializer.Serialize(new { gateResult.Passed, gateResult.Error, gateResult.BuildOutput, gateResult.TestOutput });
            story.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            yield return StoryProgressEvent.GateFailed(storyId, waveCount, gateResult);
            yield break;
        }

        yield return StoryProgressEvent.GatePassed(storyId, waveCount, gateResult);

        // Quality gate passed - ready for finalization
        story.Status = StoryStatus.ReadyToComplete;
        story.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        yield return StoryProgressEvent.ReadyToComplete(storyId, waveCount);
    }

    /// <summary>
    /// Determines if a step can be auto-executed based on the workflow's automation mode.
    /// </summary>
    private static bool CanAutoExecuteStep(AutomationMode automationMode, StoryStep step)
    {
        return automationMode switch
        {
            // Assisted mode: never auto-execute (all steps need approval)
            AutomationMode.Assisted => false,

            // Autonomous mode: auto-execute safe capabilities only
            // "Dangerous" capabilities that modify state require confirmation
            AutomationMode.Autonomous => step.Capability switch
            {
                // Safe capabilities - analysis, planning, review
                "analysis" => true,
                "review" => true,
                "testing" => true,

                // Coding/documentation modify files - require confirmation
                "coding" => false,
                "documentation" => false,
                "fixing" => false,

                // Unknown capabilities default to requiring confirmation
                _ => false,
            },

            // Full autonomous mode: execute everything (YOLO)
            AutomationMode.FullAutonomous => true,

            _ => false,
        };
    }

    /// <inheritdoc/>
    public async Task<StoryStep> AddStepAsync(
        Guid workflowId,
        string name,
        string capability,
        string? description = null,
        string? input = null,
        int? afterOrder = null,
        CancellationToken ct = default)
    {
        var workflow = await _db.Stories
            .Include(w => w.Steps)
            .FirstOrDefaultAsync(w => w.Id == workflowId, ct)
            ?? throw new InvalidOperationException($"Workflow {workflowId} not found");

        // Determine the order for the new step
        int newOrder;
        if (afterOrder.HasValue)
        {
            newOrder = afterOrder.Value + 1;

            // Shift subsequent steps
            foreach (var s in workflow.Steps.Where(s => s.Order >= newOrder))
            {
                s.Order++;
            }
        }
        else
        {
            // Add at the end
            newOrder = workflow.Steps.Any() ? workflow.Steps.Max(s => s.Order) + 1 : 1;
        }

        var step = new StoryStep
        {
            Id = Guid.NewGuid(),
            StoryId = workflowId,
            Order = newOrder,
            Name = name,
            Capability = capability,
            Description = description,
            Input = input,
            Status = StepStatus.Pending,
        };

        _db.StorySteps.Add(step);
        workflow.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Added step {StepId} to workflow {WorkflowId} at order {Order}",
            step.Id, workflowId, newOrder);

        return step;
    }

    /// <inheritdoc/>
    public async Task RemoveStepAsync(Guid workflowId, Guid stepId, CancellationToken ct = default)
    {
        var workflow = await _db.Stories
            .Include(w => w.Steps)
            .FirstOrDefaultAsync(w => w.Id == workflowId, ct)
            ?? throw new InvalidOperationException($"Workflow {workflowId} not found");

        var step = workflow.Steps.FirstOrDefault(s => s.Id == stepId);
        if (step is null)
        {
            return;
        }

        var removedOrder = step.Order;
        _db.StorySteps.Remove(step);

        // Renumber subsequent steps
        foreach (var s in workflow.Steps.Where(s => s.Order > removedOrder))
        {
            s.Order--;
        }

        workflow.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Removed step {StepId} from workflow {WorkflowId}", stepId, workflowId);
    }

    /// <inheritdoc/>
    public async Task<Story> CompleteAsync(Guid workflowId, string? githubToken = null, CancellationToken ct = default)
    {
        var workflow = await _db.Stories
            .Include(w => w.Steps)
            .FirstOrDefaultAsync(w => w.Id == workflowId, ct)
            ?? throw new InvalidOperationException($"Workflow {workflowId} not found");

        // Validate all steps are in a terminal state
        var runningSteps = workflow.Steps.Where(s => s.Status == StepStatus.Running).ToList();
        if (runningSteps.Count > 0)
        {
            var stepNames = string.Join(", ", runningSteps.Select(s => s.Name));
            throw new InvalidOperationException($"Cannot complete workflow: {runningSteps.Count} step(s) still running: {stepNames}");
        }

        var pendingSteps = workflow.Steps.Where(s => s.Status == StepStatus.Pending).ToList();
        if (pendingSteps.Count > 0)
        {
            var stepNames = string.Join(", ", pendingSteps.Select(s => s.Name));
            throw new InvalidOperationException($"Cannot complete workflow: {pendingSteps.Count} step(s) still pending: {stepNames}");
        }

        // Run verification checks before finalizing
        if (!string.IsNullOrEmpty(workflow.WorktreePath))
        {
            var verificationResult = await _verificationService.VerifyAsync(workflow.WorktreePath, ct);
            workflow.VerificationResult = System.Text.Json.JsonSerializer.Serialize(verificationResult);
            workflow.VerificationPassed = verificationResult.Success;

            if (!verificationResult.Success)
            {
                _logger.LogWarning(
                    "Verification failed for workflow {WorkflowId}: {Summary}",
                    workflowId,
                    verificationResult.Summary);

                // Log details of failed steps
                foreach (var failedStep in verificationResult.StepResults.Where(r => !r.Success && r.Required))
                {
                    _logger.LogWarning(
                        "  - {StepType}: {Error}",
                        failedStep.Step.StepType,
                        failedStep.ErrorMessage);
                }
            }
            else
            {
                _logger.LogInformation(
                    "Verification passed for workflow {WorkflowId}: {Summary}",
                    workflowId,
                    verificationResult.Summary);
            }
        }

        // Finalize git changes: commit any uncommitted work, squash into single commit, push, and create PR
        if (!string.IsNullOrEmpty(workflow.WorktreePath) &&
            !string.IsNullOrEmpty(workflow.RepositoryPath) &&
            workflow.WorktreePath != workflow.RepositoryPath)
        {
            // First, commit any uncommitted changes (in case a step didn't commit)
            var hasChanges = await _gitService.HasUncommittedChangesAsync(workflow.WorktreePath, ct);
            if (hasChanges.Success && hasChanges.Value)
            {
                var commitMessage = $"WIP: Final uncommitted changes for {workflow.Title}";
                // Skip hooks for automated workflow commits
                var commitResult = await _gitService.CommitAsync(workflow.WorktreePath, commitMessage, skipHooks: true, ct);
                if (!commitResult.Success)
                {
                    _logger.LogWarning("Failed to commit final changes: {Error}", commitResult.Error);
                }
                else
                {
                    _logger.LogInformation("Committed final changes: {Sha}", commitResult.Value);
                }
            }

            // Get the default branch to squash against
            var defaultBranchResult = await _gitService.GetDefaultBranchAsync(workflow.WorktreePath, ct);
            var baseBranch = defaultBranchResult.Success ? defaultBranchResult.Value! : "main";

            // Squash all workflow commits into a single clean commit
            var squashMessage = BuildSquashCommitMessage(workflow);
            var squashResult = await _gitService.SquashCommitsAsync(
                workflow.WorktreePath,
                baseBranch,
                squashMessage,
                ct);

            if (!squashResult.Success)
            {
                _logger.LogWarning("Failed to squash commits: {Error}. Proceeding with multiple commits.", squashResult.Error);
            }
            else
            {
                _logger.LogInformation("Squashed commits into: {Sha}", squashResult.Value);
            }

            // Push the branch (force push if squashed to update history)
            var pushResult = await _gitService.PushAsync(workflow.WorktreePath, setUpstream: true, githubToken, ct);
            if (!pushResult.Success)
            {
                _logger.LogWarning("Failed to push branch: {Error}", pushResult.Error);
            }
            else
            {
                _logger.LogInformation("Pushed branch {Branch}", workflow.GitBranch);

                // Create draft PR
                var prBody = BuildPullRequestBody(workflow);
                var prResult = await _gitService.CreatePullRequestAsync(
                    workflow.WorktreePath,
                    workflow.Title,
                    prBody,
                    baseBranch: null, // Use default branch
                    draft: true,
                    labels: ["aura-generated"],
                    githubToken,
                    ct);

                if (prResult.Success && prResult.Value is not null)
                {
                    workflow.PullRequestUrl = prResult.Value.Url;
                    _logger.LogInformation("Created PR: {Url}", prResult.Value.Url);
                }
                else
                {
                    _logger.LogWarning("Failed to create PR: {Error}", prResult.Error);
                }
            }
        }

        workflow.Status = StoryStatus.Completed;
        workflow.CompletedAt = DateTimeOffset.UtcNow;
        workflow.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Completed workflow {WorkflowId}", workflowId);
        return workflow;
    }

    private static string BuildPullRequestBody(Story workflow)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Summary");
        sb.AppendLine();
        if (!string.IsNullOrEmpty(workflow.Description))
        {
            sb.AppendLine(workflow.Description);
            sb.AppendLine();
        }

        sb.AppendLine("## Steps Completed");
        sb.AppendLine();
        foreach (var step in workflow.Steps.OrderBy(s => s.Order))
        {
            var statusIcon = step.Status == StepStatus.Completed ? "✅" :
                             step.Status == StepStatus.Skipped ? "⏭️" : "❌";
            sb.AppendLine($"- {statusIcon} {step.Name}");
        }
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine($"*Generated by Aura workflow `{workflow.Id}`*");
        return sb.ToString();
    }

    private static string BuildSquashCommitMessage(Story workflow)
    {
        var sb = new StringBuilder();

        // Title line (conventional commit format)
        sb.AppendLine($"feat: {workflow.Title}");
        sb.AppendLine();

        // Description
        if (!string.IsNullOrEmpty(workflow.Description))
        {
            sb.AppendLine(workflow.Description);
            sb.AppendLine();
        }

        // Steps summary
        sb.AppendLine("Steps completed:");
        foreach (var step in workflow.Steps.OrderBy(s => s.Order))
        {
            if (step.Status == StepStatus.Completed)
            {
                sb.AppendLine($"- {step.Name}");
            }
        }
        sb.AppendLine();

        // Footer
        sb.AppendLine($"Generated by Aura workflow {workflow.Id}");

        return sb.ToString().TrimEnd();
    }

    /// <inheritdoc/>
    public async Task<Story> CancelAsync(Guid workflowId, CancellationToken ct = default)
    {
        var workflow = await _db.Stories.FindAsync([workflowId], ct)
            ?? throw new InvalidOperationException($"Workflow {workflowId} not found");

        workflow.Status = StoryStatus.Cancelled;
        workflow.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Cancelled workflow {WorkflowId}", workflowId);
        return workflow;
    }

    /// <inheritdoc/>
    public async Task<StoryStep> ApproveStepAsync(Guid workflowId, Guid stepId, CancellationToken ct = default)
    {
        var workflow = await _db.Stories
            .Include(w => w.Steps)
            .FirstOrDefaultAsync(w => w.Id == workflowId, ct)
            ?? throw new InvalidOperationException($"Workflow {workflowId} not found");

        var step = workflow.Steps.FirstOrDefault(s => s.Id == stepId)
            ?? throw new InvalidOperationException($"Step {stepId} not found in workflow");

        if (step.Status != StepStatus.Completed)
        {
            throw new InvalidOperationException($"Cannot approve step in status {step.Status}");
        }

        step.Approval = StepApproval.Approved;
        step.ApprovalFeedback = null;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Step {StepId} approved in workflow {WorkflowId}", stepId, workflowId);
        return step;
    }

    /// <inheritdoc/>
    public async Task<StoryStep> RejectStepAsync(Guid workflowId, Guid stepId, string? feedback = null, CancellationToken ct = default)
    {
        var workflow = await _db.Stories
            .Include(w => w.Steps)
            .FirstOrDefaultAsync(w => w.Id == workflowId, ct)
            ?? throw new InvalidOperationException($"Workflow {workflowId} not found");

        var step = workflow.Steps.FirstOrDefault(s => s.Id == stepId)
            ?? throw new InvalidOperationException($"Step {stepId} not found in workflow");

        if (step.Status != StepStatus.Completed)
        {
            throw new InvalidOperationException($"Cannot reject step in status {step.Status}");
        }

        step.Approval = StepApproval.Rejected;
        step.ApprovalFeedback = feedback;
        // Reset to pending so it can be re-executed
        step.Status = StepStatus.Pending;
        step.Output = null;
        step.Attempts = 0;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Step {StepId} rejected in workflow {WorkflowId}: {Feedback}", stepId, workflowId, feedback ?? "(no feedback)");
        return step;
    }

    /// <inheritdoc/>
    public async Task<StoryStep> SkipStepAsync(Guid workflowId, Guid stepId, string? reason = null, CancellationToken ct = default)
    {
        var workflow = await _db.Stories
            .Include(w => w.Steps)
            .FirstOrDefaultAsync(w => w.Id == workflowId, ct)
            ?? throw new InvalidOperationException($"Workflow {workflowId} not found");

        var step = workflow.Steps.FirstOrDefault(s => s.Id == stepId)
            ?? throw new InvalidOperationException($"Step {stepId} not found in workflow");

        if (step.Status is StepStatus.Running)
        {
            throw new InvalidOperationException("Cannot skip a running step");
        }

        step.Status = StepStatus.Skipped;
        step.SkipReason = reason;
        step.CompletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Step {StepId} skipped in workflow {WorkflowId}: {Reason}", stepId, workflowId, reason ?? "(no reason)");
        return step;
    }

    /// <inheritdoc/>
    public async Task<StoryStep> ResetStepAsync(Guid workflowId, Guid stepId, CancellationToken ct = default)
    {
        var workflow = await _db.Stories
            .Include(w => w.Steps)
            .FirstOrDefaultAsync(w => w.Id == workflowId, ct)
            ?? throw new InvalidOperationException($"Workflow {workflowId} not found");

        var step = workflow.Steps.FirstOrDefault(s => s.Id == stepId)
            ?? throw new InvalidOperationException($"Step {stepId} not found in workflow");

        if (step.Status == StepStatus.Running)
        {
            throw new InvalidOperationException("Cannot reset a running step");
        }

        var previousStatus = step.Status;
        step.Status = StepStatus.Pending;
        step.Output = null;
        step.Error = null;
        step.CompletedAt = null;
        step.Attempts = 0;
        step.Approval = null;
        step.ApprovalFeedback = null;
        step.NeedsRework = false;

        // If workflow was completed or failed, set it back to executing
        if (workflow.Status is StoryStatus.Completed or StoryStatus.Failed)
        {
            workflow.Status = StoryStatus.Executing;
        }

        workflow.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Step {StepId} reset from {PreviousStatus} to Pending in workflow {WorkflowId}",
            stepId, previousStatus, workflowId);
        return step;
    }

    private record ChatMessage(string Role, string Content);

    /// <summary>
    /// Sets up Copilot/MCP configuration in a worktree to enable Aura tools.
    /// Creates .vscode/settings.json and copies copilot-instructions.md.
    /// </summary>
    private async Task SetupWorktreeCopilotConfigAsync(string repositoryPath, string worktreePath, CancellationToken ct)
    {
        try
        {
            // Create .vscode directory if it doesn't exist
            var vscodeDir = Path.Combine(worktreePath, ".vscode");
            if (!Directory.Exists(vscodeDir))
            {
                Directory.CreateDirectory(vscodeDir);
            }

            // Create settings.json with MCP autostart
            var settingsPath = Path.Combine(vscodeDir, "settings.json");
            if (!File.Exists(settingsPath))
            {
                var settings = """
                    {
                        "chat.mcp.autostart": "always"
                    }
                    """;
                await File.WriteAllTextAsync(settingsPath, settings, ct);
                _logger.LogDebug("Created .vscode/settings.json with MCP autostart in worktree");
            }

            // Copy copilot-instructions.md if it exists in the source repo
            var sourceInstructionsPath = Path.Combine(repositoryPath, ".github", "copilot-instructions.md");
            if (File.Exists(sourceInstructionsPath))
            {
                var destGithubDir = Path.Combine(worktreePath, ".github");
                if (!Directory.Exists(destGithubDir))
                {
                    Directory.CreateDirectory(destGithubDir);
                }

                var destInstructionsPath = Path.Combine(destGithubDir, "copilot-instructions.md");

                // Read the source instructions and append Aura-specific guidance
                var instructions = await File.ReadAllTextAsync(sourceInstructionsPath, ct);

                // Check if Aura tools guidance is already present
                if (!instructions.Contains("aura_", StringComparison.OrdinalIgnoreCase))
                {
                    instructions += "\n\n" + McpToolDocumentation.GetCopilotInstructionsMarkdown(_promptRegistry.PromptsDirectory);
                }

                await File.WriteAllTextAsync(destInstructionsPath, instructions, ct);
                _logger.LogDebug("Copied copilot-instructions.md to worktree with Aura tools guidance");
            }
            else
            {
                // Source repo doesn't have copilot-instructions.md - create one with Aura tools guidance
                var destGithubDir = Path.Combine(worktreePath, ".github");
                if (!Directory.Exists(destGithubDir))
                {
                    Directory.CreateDirectory(destGithubDir);
                }

                var destInstructionsPath = Path.Combine(destGithubDir, "copilot-instructions.md");
                if (!File.Exists(destInstructionsPath))
                {
                    var instructions = "# Copilot Instructions\n\n" + McpToolDocumentation.GetCopilotInstructionsMarkdown(_promptRegistry.PromptsDirectory);
                    await File.WriteAllTextAsync(destInstructionsPath, instructions, ct);
                    _logger.LogDebug("Created copilot-instructions.md in worktree with Aura tools guidance");
                }
            }
        }
        catch (Exception ex)
        {
            // Non-fatal - log and continue
            _logger.LogWarning(ex, "Failed to set up Copilot config in worktree {Path}", worktreePath);
        }
    }

    /// <summary>
    /// Determines if a provider supports structured output well enough to use for workflow planning.
    /// OpenAI and Azure OpenAI have full schema support; Ollama has basic JSON mode only.
    /// </summary>
    private static bool CanUseStructuredOutput(ILlmProvider provider)
    {
        // Azure OpenAI and OpenAI have full schema enforcement
        var providerId = provider.ProviderId.ToLowerInvariant();
        return providerId is LlmProviders.AzureOpenAI or LlmProviders.OpenAI;
    }

    /// <summary>
    /// Parses workflow steps from a structured JSON response (from providers with schema support).
    /// </summary>
    private static List<StepDefinition> ParseStepsFromStructuredResponse(string response)
    {
        var steps = new List<StepDefinition>();

        try
        {
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            if (root.TryGetProperty("steps", out var stepsArray) && stepsArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var stepEl in stepsArray.EnumerateArray())
                {
                    var name = stepEl.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "Unnamed step" : "Unnamed step";
                    var capability = stepEl.TryGetProperty("capability", out var capProp) ? capProp.GetString() ?? "coding" : "coding";
                    var description = stepEl.TryGetProperty("description", out var descProp) ? descProp.GetString() : null;
                    var language = stepEl.TryGetProperty("language", out var langProp) ? langProp.GetString() : null;

                    steps.Add(new StepDefinition
                    {
                        Name = name,
                        Capability = capability.ToLowerInvariant(),
                        Description = description,
                        Language = language,
                    });
                }
            }
        }
        catch (JsonException)
        {
            // Fall back to text parsing if JSON parsing fails
            return ParseStepsFromResponse(response);
        }

        // If no steps were parsed, fall back to text parsing
        if (steps.Count == 0)
        {
            return ParseStepsFromResponse(response);
        }

        return steps;
    }

    private static List<StepDefinition> ParseStepsFromResponse(string response)
    {
        var steps = new List<StepDefinition>();

        try
        {
            // Try to find JSON array in the response
            var startIndex = response.IndexOf('[');
            var endIndex = response.LastIndexOf(']');

            if (startIndex >= 0 && endIndex > startIndex)
            {
                var json = response[startIndex..(endIndex + 1)];
                var parsed = JsonSerializer.Deserialize<List<StepDefinition>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                });

                if (parsed is not null)
                {
                    steps.AddRange(parsed);
                }
            }
        }
        catch
        {
            // If parsing fails, create a single generic step
            steps.Add(new StepDefinition
            {
                Name = "Implement feature",
                Capability = "coding",
                Description = response,
            });
        }

        return steps;
    }

    private static (string Name, string Capability, string? Description)? ParseAddStepAction(string response)
    {
        try
        {
            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            string? name = null;
            string? capability = null;
            string? description = null;

            foreach (var line in lines)
            {
                if (line.StartsWith("NAME:", StringComparison.OrdinalIgnoreCase))
                {
                    name = line["NAME:".Length..].Trim();
                }
                else if (line.StartsWith("CAPABILITY:", StringComparison.OrdinalIgnoreCase))
                {
                    capability = line["CAPABILITY:".Length..].Trim();
                }
                else if (line.StartsWith("DESCRIPTION:", StringComparison.OrdinalIgnoreCase))
                {
                    description = line["DESCRIPTION:".Length..].Trim();
                }
            }

            if (name is not null && capability is not null)
            {
                return (name, capability, description);
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return null;
    }

    private static int? ParseRemoveStepAction(string response)
    {
        try
        {
            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.StartsWith("STEP_NUMBER:", StringComparison.OrdinalIgnoreCase))
                {
                    var numberStr = line["STEP_NUMBER:".Length..].Trim();
                    if (int.TryParse(numberStr, out var number))
                    {
                        return number;
                    }
                }
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return null;
    }

    private static (int StepNumber, List<(string Name, string Capability, string? Description)> NewSteps)? ParseSplitStepAction(string response)
    {
        try
        {
            var lines = response.Split('\n');
            int? stepNumber = null;
            var newSteps = new List<(string Name, string Capability, string? Description)>();

            string? currentName = null;
            string? currentCapability = null;
            string? currentDescription = null;
            var inNewSteps = false;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();

                if (line.StartsWith("STEP_NUMBER:", StringComparison.OrdinalIgnoreCase))
                {
                    var numberStr = line["STEP_NUMBER:".Length..].Trim();
                    if (int.TryParse(numberStr, out var number))
                    {
                        stepNumber = number;
                    }
                }
                else if (line.StartsWith("NEW_STEPS:", StringComparison.OrdinalIgnoreCase))
                {
                    inNewSteps = true;
                }
                else if (inNewSteps)
                {
                    // Parse YAML-like list items
                    if (line.StartsWith("- NAME:", StringComparison.OrdinalIgnoreCase) ||
                        line.StartsWith("-NAME:", StringComparison.OrdinalIgnoreCase))
                    {
                        // Save previous step if we have one
                        if (currentName is not null && currentCapability is not null)
                        {
                            newSteps.Add((currentName, currentCapability, currentDescription));
                        }

                        currentName = line.Contains("NAME:")
                            ? line[(line.IndexOf("NAME:", StringComparison.OrdinalIgnoreCase) + 5)..].Trim()
                            : null;
                        currentCapability = null;
                        currentDescription = null;
                    }
                    else if (line.StartsWith("CAPABILITY:", StringComparison.OrdinalIgnoreCase))
                    {
                        currentCapability = line["CAPABILITY:".Length..].Trim();
                    }
                    else if (line.StartsWith("DESCRIPTION:", StringComparison.OrdinalIgnoreCase))
                    {
                        currentDescription = line["DESCRIPTION:".Length..].Trim();
                    }
                }
            }

            // Don't forget the last step
            if (currentName is not null && currentCapability is not null)
            {
                newSteps.Add((currentName, currentCapability, currentDescription));
            }

            if (stepNumber.HasValue && newSteps.Count > 0)
            {
                return (stepNumber.Value, newSteps);
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return null;
    }

    private async Task IndexWorktreeForRagAsync(string workspacePath, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Indexing worktree {Path} for RAG...", workspacePath);

            var options = new RagIndexOptions
            {
                IncludePatterns = new[] { "*.cs", "*.md", "*.json", "*.yaml", "*.yml", "*.ts", "*.tsx", "*.js", "*.jsx" },
                ExcludePatterns = new[] { "**/bin/**", "**/obj/**", "**/node_modules/**", "**/.git/**" },
                Recursive = true,
            };

            var indexedCount = await _ragService.IndexDirectoryAsync(workspacePath, options, ct);

            _logger.LogInformation(
                "Indexed {Count} files from {Path} for RAG",
                indexedCount,
                workspacePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to index worktree for RAG, continuing without indexing");
        }
    }

    private static bool IsValidCapability(string capability)
    {
        var validCapabilities = new[] { "coding", "testing", "review", "documentation", "fixing" };
        return validCapabilities.Contains(capability.ToLowerInvariant().Trim());
    }

    private static string ExtractAnalysisSummary(string? analyzedContext)
    {
        if (string.IsNullOrEmpty(analyzedContext))
            return "No analysis available yet.";

        try
        {
            // Try to parse as JSON and extract the analysis field
            using var doc = System.Text.Json.JsonDocument.Parse(analyzedContext);
            if (doc.RootElement.TryGetProperty("analysis", out var analysis))
            {
                var text = analysis.GetString() ?? "";
                // Return first 500 chars to keep prompt reasonable
                return text.Length > 500 ? text[..500] + "..." : text;
            }
        }
        catch
        {
            // Not JSON, return as-is but truncated
        }

        return analyzedContext.Length > 500 ? analyzedContext[..500] + "..." : analyzedContext;
    }

    private static string? ParseReanalyzeAction(string response)
    {
        try
        {
            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.StartsWith("CONTEXT:", StringComparison.OrdinalIgnoreCase))
                {
                    return line["CONTEXT:".Length..].Trim();
                }
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return null;
    }

    private sealed record StepDefinition
    {
        public required string Name { get; init; }
        public required string Capability { get; init; }
        public string? Language { get; init; }
        public string? Description { get; init; }
        public int Wave { get; init; } = 1;
    }

    /// <summary>
    /// Builds capability-specific RAG queries for a step.
    /// Different capabilities need different types of context.
    /// </summary>
    private static (List<string> Queries, List<string> FileReferences) BuildRagQueriesForStep(StoryStep step, Story workflow)
    {
        var queries = new List<string>();
        var fileReferences = new List<string>();

        // Extract explicit file references from step name and description
        fileReferences.AddRange(ExtractFileReferences(step.Name));
        if (!string.IsNullOrEmpty(step.Description))
        {
            fileReferences.AddRange(ExtractFileReferences(step.Description));
        }

        // Add file references as high-priority queries (semantic search on file names)
        foreach (var fileRef in fileReferences.Distinct())
        {
            queries.Add(fileRef);
        }

        // Always include a query about project structure and build
        queries.Add("project structure build setup dotnet csproj solution");

        // Add capability-specific queries
        switch (step.Capability?.ToLowerInvariant())
        {
            case "documentation":
                queries.Add("README documentation getting started installation");
                queries.Add("project description purpose features overview");
                queries.Add("architecture design structure packages components");
                queries.Add("dependencies packages libraries imports references relationship");
                queries.Add("build compile configuration prerequisites setup");
                queries.Add("performance optimization caching patterns characteristics");
                queries.Add("API reference usage examples code samples");
                queries.Add("contributing guidelines versioning release");
                if (!string.IsNullOrEmpty(step.Description))
                {
                    queries.Add(step.Description);
                }
                break;

            case "coding":
                queries.Add($"{step.Name} implementation class interface");
                if (!string.IsNullOrEmpty(step.Description))
                {
                    queries.Add(step.Description);
                }
                // Add language-specific query if known
                if (!string.IsNullOrEmpty(step.Language))
                {
                    queries.Add($"{step.Language} code pattern example");
                }
                break;

            case "review":
                queries.Add("code style conventions best practices");
                queries.Add($"{step.Name} quality review");
                break;

            case "fixing":
                queries.Add("error handling exception build test");
                if (!string.IsNullOrEmpty(step.Description))
                {
                    queries.Add(step.Description);
                }
                break;

            case "testing":
                queries.Add("test unit integration xunit nunit");
                queries.Add($"{step.Name} test case");
                break;

            default:
                // Generic fallback
                queries.Add($"{step.Name} {step.Description ?? ""} {workflow.Title}");
                break;
        }

        return (queries, fileReferences.Distinct().ToList());
    }

    /// <summary>
    /// Extracts file references from text (e.g., "README.md", "src/Program.cs").
    /// </summary>
    private static List<string> ExtractFileReferences(string text)
    {
        var files = new List<string>();
        if (string.IsNullOrEmpty(text))
        {
            return files;
        }

        // Pattern to match file names with common extensions
        var pattern = @"\b[\w\-\.\/\\]+\.(md|cs|json|yaml|yml|xml|proj|props|targets|csproj|sln|ts|tsx|js|jsx|py|rs|fs|go|txt|config)\b";
        var matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase);

        foreach (Match match in matches)
        {
            var fileName = match.Value;
            // Normalize path separators and get just the file name
            fileName = fileName.Replace('\\', '/');
            files.Add(fileName);

            // Also add just the file name without path for broader matching
            var justFileName = Path.GetFileName(fileName);
            if (!string.IsNullOrEmpty(justFileName) && justFileName != fileName)
            {
                files.Add(justFileName);
            }
        }

        return files;
    }

    /// <summary>
    /// Builds RAG queries for workflow analysis to understand the codebase.
    /// </summary>
    private static List<string> BuildRagQueriesForAnalysis(Story workflow)
    {
        var queries = new List<string>
        {
            // Core project understanding
            "README project overview description purpose what is this",
            "project structure architecture design components modules",
            "main entry point program startup initialization",
            "build configuration csproj project dependencies packages",

            // Technical details
            "public class interface API contract service",
            "namespace using imports references",
            "configuration settings options appsettings",

            // Documentation
            "documentation comments summary description usage",
            "examples sample code demonstration how to use",
        };

        // Add workflow-specific query
        if (!string.IsNullOrEmpty(workflow.Description))
        {
            queries.Add(workflow.Description);
        }

        return queries;
    }

    /// <inheritdoc />
    public async Task<StoryStep> ReassignStepAsync(
        Guid workflowId,
        Guid stepId,
        string agentId,
        CancellationToken ct = default)
    {
        var workflow = await _db.Stories
            .Include(w => w.Steps)
            .FirstOrDefaultAsync(w => w.Id == workflowId, ct)
            ?? throw new KeyNotFoundException($"Workflow {workflowId} not found");

        var step = workflow.Steps.FirstOrDefault(s => s.Id == stepId)
            ?? throw new KeyNotFoundException($"Step {stepId} not found in workflow {workflowId}");

        // Validate the agent exists
        var agent = _agentRegistry.GetAgent(agentId);
        if (agent is null)
        {
            throw new ArgumentException($"Agent '{agentId}' not found");
        }

        step.AssignedAgentId = agentId;

        // If step was completed, mark it as needing re-execution
        if (step.Status == StepStatus.Completed)
        {
            step.NeedsRework = true;
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Reassigned step {StepId} to agent {AgentId}", stepId, agentId);
        return step;
    }

    /// <inheritdoc />
    public async Task<StoryStep> UpdateStepDescriptionAsync(
        Guid workflowId,
        Guid stepId,
        string description,
        CancellationToken ct = default)
    {
        var workflow = await _db.Stories
            .Include(w => w.Steps)
            .FirstOrDefaultAsync(w => w.Id == workflowId, ct)
            ?? throw new KeyNotFoundException($"Workflow {workflowId} not found");

        var step = workflow.Steps.FirstOrDefault(s => s.Id == stepId)
            ?? throw new KeyNotFoundException($"Step {stepId} not found in workflow {workflowId}");

        step.Description = description;

        // If step was completed, mark it as needing re-execution since description changed
        if (step.Status == StepStatus.Completed)
        {
            step.NeedsRework = true;
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Updated description for step {StepId}", stepId);
        return step;
    }

    /// <summary>
    /// Parses a GitHub issue URL into its components.
    /// </summary>
    /// <param name="url">The issue URL (e.g., "https://github.com/owner/repo/issues/123").</param>
    /// <returns>The parsed components, or null if the URL is invalid.</returns>
    private static (string owner, string repo, int number)? ParseGitHubIssueUrl(string url)
    {
        var match = Regex.Match(url, @"github\.com/([^/]+)/([^/]+)/issues/(\d+)", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return null;
        }

        return (match.Groups[1].Value, match.Groups[2].Value, int.Parse(match.Groups[3].Value));
    }
}
