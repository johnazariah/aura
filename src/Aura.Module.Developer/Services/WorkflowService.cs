// <copyright file="WorkflowService.cs" company="Aura">
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
using Aura.Foundation.Prompts;
using Aura.Foundation.Rag;
using Aura.Foundation.Tools;
using Aura.Module.Developer.Data;
using Aura.Module.Developer.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Service for managing development workflows.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="WorkflowService"/> class.
/// </remarks>
public sealed class WorkflowService(
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
    IOptions<DeveloperModuleOptions> options,
    ILogger<WorkflowService> logger) : IWorkflowService
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
    private readonly DeveloperModuleOptions _options = options.Value;
    private readonly ILogger<WorkflowService> _logger = logger;

    /// <inheritdoc/>
    public async Task<Workflow> CreateAsync(
        string title,
        string? description = null,
        string? repositoryPath = null,
        WorkflowMode mode = WorkflowMode.Structured,
        AutomationMode automationMode = AutomationMode.Assisted,
        string? issueUrl = null,
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

        // Create the workflow
        var workflow = new Workflow
        {
            Id = Guid.NewGuid(),
            RepositoryPath = repositoryPath,
            Title = title,
            Description = description,
            GitBranch = branchName,
            Status = WorkflowStatus.Created,
            Mode = mode,
            AutomationMode = automationMode,
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
        }

        _db.Workflows.Add(workflow);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created workflow {WorkflowId}: {Title}", workflow.Id, title);
        return workflow;
    }

    /// <inheritdoc/>
    public async Task<Workflow?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Workflows.FindAsync([id], ct);
    }

    /// <inheritdoc/>
    public async Task<Workflow?> GetByIdWithStepsAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Workflows
            .Include(w => w.Steps.OrderBy(s => s.Order))
            .FirstOrDefaultAsync(w => w.Id == id, ct);
    }

    /// <inheritdoc/>
    public async Task<Workflow?> GetByWorktreePathAsync(string worktreePath, CancellationToken ct = default)
    {
        // Normalize path for cross-platform comparison
        var normalizedPath = Aura.Foundation.Rag.PathNormalizer.Normalize(worktreePath);
        return await _db.Workflows
            .Include(w => w.Steps.OrderBy(s => s.Order))
            .FirstOrDefaultAsync(w => w.WorktreePath != null &&
                EF.Functions.ILike(
                    w.WorktreePath.Replace("\\", "/").ToLower(),
                    normalizedPath), ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Workflow>> ListAsync(WorkflowStatus? status = null, string? repositoryPath = null, CancellationToken ct = default)
    {
        var query = _db.Workflows.AsQueryable();

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
        var workflow = await _db.Workflows
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

        _db.Workflows.Remove(workflow);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted workflow {WorkflowId}: {Title}", id, workflow.Title);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(Workflow workflow, CancellationToken ct = default)
    {
        _db.Workflows.Update(workflow);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Updated workflow {WorkflowId}: {Title}", workflow.Id, workflow.Title);
    }

    /// <inheritdoc/>
    public async Task<Workflow> AnalyzeAsync(Guid workflowId, CancellationToken ct = default)
    {
        var workflow = await _db.Workflows
            .FirstOrDefaultAsync(w => w.Id == workflowId, ct)
            ?? throw new InvalidOperationException($"Workflow {workflowId} not found");

        workflow.Status = WorkflowStatus.Analyzing;
        workflow.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        try
        {
            // Get the issue-enrichment agent
            var analyzer = _agentRegistry.GetBestForCapability(Capabilities.Enrichment);
            if (analyzer is null)
            {
                throw new InvalidOperationException($"No agent with '{Capabilities.Enrichment}' capability found");
            }

            // Build the prompt from template
            var prompt = _promptRegistry.Render("workflow-enrich", new
            {
                title = workflow.Title,
                description = workflow.Description ?? "No description provided.",
                workspacePath = workflow.WorktreePath ?? workflow.RepositoryPath ?? "Not specified",
            });

            // Get codebase context (combines code graph + RAG)
            var promptQueries = _promptRegistry.GetRagQueries("workflow-enrich");
            var ragQueries = promptQueries.Count > 0
                ? promptQueries.ToList()
                : BuildRagQueriesForAnalysis(workflow);

            // Always use RepositoryPath for RAG queries (that's where the index lives)
            // Paths in context will be relative, and the agent's WorkingDirectory is set to the worktree
            var contextOptions = CodebaseContextOptions.ForDocumentation(ragQueries);
            var ragSourcePath = workflow.RepositoryPath ?? workflow.WorktreePath;
            var codebaseContext = ragSourcePath is not null
                ? await _codebaseContextService.GetContextAsync(ragSourcePath, contextOptions, ct)
                : null;
            var ragContext = codebaseContext?.ToPromptContext();

            _logger.LogInformation(
                "Analyze codebase context: {HasContext}, queries: {QueryCount}, hasProjectStructure: {HasProjectStructure}",
                ragContext is not null ? $"{ragContext.Length} chars" : "none",
                ragQueries.Count,
                codebaseContext?.ProjectStructure is not null);

            // Check if tools are defined for this prompt
            var promptToolNames = _promptRegistry.GetTools("workflow-enrich");
            _logger.LogWarning("[ANALYZE-DEBUG] Prompt tool names from registry: [{Tools}]", string.Join(", ", promptToolNames));

            var availableTools = promptToolNames
                .Select(name => _toolRegistry.GetTool(name))
                .Where(t => t is not null)
                .Cast<ToolDefinition>()
                .ToList();

            _logger.LogWarning("[ANALYZE-DEBUG] Available tools resolved: {Count} - [{Tools}]",
                availableTools.Count,
                string.Join(", ", availableTools.Select(t => t.ToolId)));

            string analysisContent;
            int tokensUsed;
            IReadOnlyList<ReActStep>? toolSteps = null;

            if (availableTools.Count > 0)
            {
                // Use ReAct executor for tool-enabled exploration
                _logger.LogInformation(
                    "Analyze has {ToolCount} tools available: {Tools}",
                    availableTools.Count,
                    string.Join(", ", availableTools.Select(t => t.ToolId)));

                var llmProvider = _llmProviderRegistry.GetDefaultProvider()
                    ?? throw new InvalidOperationException("No LLM provider available");

                var taskWithContext = ragContext is not null
                    ? $"{prompt}\n\n## Relevant Context from Knowledge Base\n{ragContext}"
                    : prompt;

                var reactOptions = new ReActOptions
                {
                    WorkingDirectory = workflow.WorktreePath,
                    MaxSteps = 10, // Limit exploration steps
                    Temperature = 0.2,
                    RequireConfirmation = false,
                };

                var reactResult = await _reactExecutor.ExecuteAsync(
                    taskWithContext,
                    availableTools,
                    llmProvider,
                    reactOptions,
                    ct);

                analysisContent = reactResult.FinalAnswer ?? "Analysis could not be completed";
                tokensUsed = reactResult.TotalTokensUsed;
                toolSteps = reactResult.Steps;

                _logger.LogInformation(
                    "Agentic analysis completed: {Success}, steps={StepCount}, tokens={Tokens}",
                    reactResult.Success,
                    reactResult.Steps.Count,
                    reactResult.TotalTokensUsed);
            }
            else
            {
                // Fallback to simple agent execution (no tools)
                var context = new AgentContext(prompt, WorkspacePath: workflow.WorktreePath)
                {
                    RagContext = ragContext,
                };
                var output = await analyzer.ExecuteAsync(context, ct);
                analysisContent = output.Content;
                tokensUsed = output.TokensUsed;
            }

            workflow.AnalyzedContext = JsonSerializer.Serialize(new
            {
                agentId = analyzer.AgentId,
                analysis = analysisContent,
                tokensUsed,
                toolSteps = toolSteps?.Select(s => new { s.StepNumber, s.Action, s.Observation }),
                timestamp = DateTimeOffset.UtcNow,
            });

            workflow.Status = WorkflowStatus.Analyzed;
            workflow.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Analyzed workflow {WorkflowId}", workflowId);
            return workflow;
        }
        catch (Exception ex)
        {
            workflow.Status = WorkflowStatus.Failed;
            workflow.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            _logger.LogError(ex, "Failed to analyze workflow {WorkflowId}", workflowId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<Workflow> PlanAsync(Guid workflowId, CancellationToken ct = default)
    {
        var workflow = await _db.Workflows
            .FirstOrDefaultAsync(w => w.Id == workflowId, ct)
            ?? throw new InvalidOperationException($"Workflow {workflowId} not found");

        if (workflow.Status != WorkflowStatus.Analyzed)
        {
            throw new InvalidOperationException($"Workflow must be analyzed before planning. Current status: {workflow.Status}");
        }

        workflow.Status = WorkflowStatus.Planning;
        workflow.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        try
        {
            // Get the business-analyst agent for planning
            var planner = _agentRegistry.GetBestForCapability(Capabilities.Analysis);
            if (planner is null)
            {
                throw new InvalidOperationException($"No agent with '{Capabilities.Analysis}' capability found");
            }

            // Build the planning prompt from template
            var prompt = _promptRegistry.Render("workflow-plan", new
            {
                title = workflow.Title,
                description = workflow.Description ?? "No description provided.",
                enrichedContext = workflow.AnalyzedContext ?? "No analysis available.",
            });

            // Try to use structured output if the provider supports it
            List<StepDefinition> steps;
            string rawResponse;
            int tokensUsed;

            var providerId = planner.Metadata.Provider ?? "ollama";
            var provider = _llmProviderRegistry.GetProvider(providerId);

            if (provider is not null && CanUseStructuredOutput(provider))
            {
                // Use ChatAsync with schema for reliable JSON parsing
                var messages = new List<Aura.Foundation.Agents.ChatMessage>
                {
                    new(Aura.Foundation.Agents.ChatRole.System, "You are a workflow planning assistant. Create a structured plan with clear steps."),
                    new(Aura.Foundation.Agents.ChatRole.User, prompt),
                };

                var chatOptions = new ChatOptions
                {
                    ResponseSchema = WellKnownSchemas.WorkflowPlan,
                    Temperature = planner.Metadata.Temperature,
                };

                var response = await provider.ChatAsync(planner.Metadata.Model, messages, chatOptions, ct);
                rawResponse = response.Content;
                tokensUsed = response.TokensUsed;
                steps = ParseStepsFromStructuredResponse(rawResponse);

                _logger.LogDebug("Used structured output for workflow planning");
            }
            else
            {
                // Fallback to regular agent execution
                var context = new AgentContext(prompt, WorkspacePath: workflow.WorktreePath);
                var output = await planner.ExecuteAsync(context, ct);
                rawResponse = output.Content;
                tokensUsed = output.TokensUsed;
                steps = ParseStepsFromResponse(rawResponse);
            }

            // Clear existing steps and add new ones
            var existingSteps = await _db.WorkflowSteps
                .Where(s => s.WorkflowId == workflowId)
                .ToListAsync(ct);
            _db.WorkflowSteps.RemoveRange(existingSteps);

            var order = 1;
            foreach (var step in steps)
            {
                var workflowStep = new WorkflowStep
                {
                    Id = Guid.NewGuid(),
                    WorkflowId = workflowId,
                    Order = order++,
                    Name = step.Name,
                    Capability = step.Capability,
                    Language = step.Language,
                    Description = step.Description,
                    Status = StepStatus.Pending,
                };
                _db.WorkflowSteps.Add(workflowStep);
            }

            workflow.ExecutionPlan = JsonSerializer.Serialize(new
            {
                agentId = planner.AgentId,
                rawResponse,
                stepCount = steps.Count,
                tokensUsed,
                timestamp = DateTimeOffset.UtcNow,
            });

            workflow.Status = WorkflowStatus.Planned;
            workflow.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);

            // Reload with steps
            var reloaded = await GetByIdWithStepsAsync(workflowId, ct);

            _logger.LogInformation("Planned workflow {WorkflowId} with {StepCount} steps", workflowId, steps.Count);
            return reloaded!;
        }
        catch (Exception ex)
        {
            workflow.Status = WorkflowStatus.Failed;
            workflow.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            _logger.LogError(ex, "Failed to plan workflow {WorkflowId}", workflowId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<WorkflowStep> ExecuteStepAsync(
        Guid workflowId,
        Guid stepId,
        string? agentIdOverride = null,
        CancellationToken ct = default)
    {
        var workflow = await _db.Workflows
            .Include(w => w.Steps)
            .FirstOrDefaultAsync(w => w.Id == workflowId, ct)
            ?? throw new InvalidOperationException($"Workflow {workflowId} not found");

        var step = workflow.Steps.FirstOrDefault(s => s.Id == stepId)
            ?? throw new InvalidOperationException($"Step {stepId} not found in workflow {workflowId}");

        if (step.Status == StepStatus.Running)
        {
            throw new InvalidOperationException($"Step {stepId} is already running");
        }

        // If re-executing a completed step, save previous output and mark subsequent steps for rework
        var isReExecution = step.Status == StepStatus.Completed;
        if (isReExecution)
        {
            _logger.LogInformation("Re-executing completed step {StepId}, will cascade rework to subsequent steps", stepId);

            // Save previous output for comparison
            step.PreviousOutput = step.Output;
            step.Approval = null;  // Clear approval since we're re-doing it
            step.ApprovalFeedback = null;

            // Mark all subsequent steps as needing rework
            var subsequentSteps = workflow.Steps
                .Where(s => s.Order > step.Order && s.Status == StepStatus.Completed)
                .ToList();

            foreach (var subsequentStep in subsequentSteps)
            {
                subsequentStep.NeedsRework = true;
                _logger.LogInformation("Marked step {StepId} ({StepName}) for rework", subsequentStep.Id, subsequentStep.Name);
            }
        }

        // Clear rework flag since we're now executing
        step.NeedsRework = false;

        // Get the agent for this step
        IAgent? agent;
        if (!string.IsNullOrEmpty(agentIdOverride))
        {
            agent = _agentRegistry.GetAgent(agentIdOverride);
            if (agent is null)
            {
                throw new InvalidOperationException($"Agent '{agentIdOverride}' not found");
            }
        }
        else
        {
            // Pass language to get best matching agent
            agent = _agentRegistry.GetBestForCapability(step.Capability, step.Language);
            if (agent is null)
            {
                throw new InvalidOperationException($"No agent found for capability '{step.Capability}'" +
                    (step.Language is not null ? $" with language '{step.Language}'" : ""));
            }
        }

        // Update status to running
        step.Status = StepStatus.Running;
        step.AssignedAgentId = agent.AgentId;
        step.StartedAt = DateTimeOffset.UtcNow;
        step.Attempts++;
        workflow.Status = WorkflowStatus.Executing;
        workflow.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Build the step execution prompt from template
            string prompt;
            string promptName;
            List<string>? ragQueriesFromPrompt = null;

            if (step.Capability == "review")
            {
                promptName = "step-review";

                // Extract the analysis from enriched context for review steps too
                var analysis = string.Empty;
                if (workflow.AnalyzedContext is not null)
                {
                    try
                    {
                        var enriched = JsonSerializer.Deserialize<JsonElement>(workflow.AnalyzedContext);
                        if (enriched.TryGetProperty("analysis", out var analysisElement))
                        {
                            analysis = analysisElement.GetString() ?? string.Empty;
                        }
                    }
                    catch
                    {
                        // Ignore parse errors
                    }
                }

                // For review capability, gather outputs from previous coding steps
                var codeToReview = new System.Text.StringBuilder();
                // Include both coding and documentation steps in review
                var codingSteps = workflow.Steps
                    .Where(s => (s.Capability == "coding" || s.Capability == "documentation") && s.Status == StepStatus.Completed && s.Output is not null)
                    .OrderBy(s => s.Order)
                    .ToList();

                foreach (var codingStep in codingSteps)
                {
                    try
                    {
                        var stepOutput = JsonSerializer.Deserialize<JsonElement>(codingStep.Output!);
                        if (stepOutput.TryGetProperty("content", out var content))
                        {
                            codeToReview.AppendLine($"### {codingStep.Name}");
                            codeToReview.AppendLine(content.GetString());
                            codeToReview.AppendLine();
                        }
                    }
                    catch
                    {
                        // Skip malformed output
                    }
                }

                prompt = _promptRegistry.Render("step-review", new
                {
                    stepName = step.Name,
                    stepDescription = step.Description ?? "No additional description.",
                    issueTitle = workflow.Title,
                    codeToReview = codeToReview.ToString(),
                    analysis, // Include enriched context for review steps
                });

                // Use RAG queries from prompt template if defined
                var reviewQueries = _promptRegistry.GetRagQueries("step-review");
                ragQueriesFromPrompt = reviewQueries.Count > 0 ? reviewQueries.ToList() : null;
            }
            else
            {
                // For non-review steps, include the analyzed context
                var analysis = string.Empty;
                if (workflow.AnalyzedContext is not null)
                {
                    try
                    {
                        var enriched = JsonSerializer.Deserialize<JsonElement>(workflow.AnalyzedContext);
                        if (enriched.TryGetProperty("analysis", out var analysisElement))
                        {
                            analysis = $"Analysis: {analysisElement.GetString()}";
                        }
                    }
                    catch
                    {
                        analysis = $"Analysis: {workflow.AnalyzedContext}";
                    }
                }

                // Use capability-specific prompts for richer guidance
                promptName = step.Capability?.ToLowerInvariant() switch
                {
                    "documentation" => "step-execute-documentation",
                    _ => "step-execute"
                };

                // Include revision feedback if this is a re-execution after rejection
                var revisionFeedback = step.ApprovalFeedback;

                prompt = _promptRegistry.Render(promptName, new
                {
                    stepName = step.Name,
                    stepDescription = step.Description ?? "No additional description.",
                    issueTitle = workflow.Title,
                    analysis,
                    revisionFeedback,
                });

                // Use RAG queries from prompt template if defined, otherwise use defaults
                var promptQueries = _promptRegistry.GetRagQueries(promptName);
                ragQueriesFromPrompt = promptQueries.Count > 0 ? promptQueries.ToList() : null;
            }

            // Build codebase context (combines code graph + RAG)
            List<string> ragQueries;
            List<string>? fileReferences = null;

            if (ragQueriesFromPrompt is not null)
            {
                ragQueries = ragQueriesFromPrompt;
                // Also extract file references from step even when using prompt queries
                fileReferences = ExtractFileReferences(step.Name)
                    .Concat(ExtractFileReferences(step.Description ?? string.Empty))
                    .Distinct()
                    .ToList();
            }
            else
            {
                var (queries, files) = BuildRagQueriesForStep(step, workflow);
                ragQueries = queries;
                fileReferences = files;
            }

            // Always use RepositoryPath for RAG queries (that's where the index lives)
            // Paths in context will be relative, and the agent's WorkingDirectory is set to the worktree
            // Use ForCoding for coding/testing tasks to prefer code content over docs
            var isCodingTask = step.Capability is "coding" or "testing" or "review";
            var contextOptions = isCodingTask
                ? CodebaseContextOptions.ForCoding(ragQueries, prioritizeFiles: fileReferences)
                : CodebaseContextOptions.ForDocumentation(ragQueries, fileReferences);
            var ragSourcePath = workflow.RepositoryPath ?? workflow.WorktreePath;
            var codebaseContext = ragSourcePath is not null
                ? await _codebaseContextService.GetContextAsync(ragSourcePath, contextOptions, ct)
                : null;
            var ragContext = codebaseContext?.ToPromptContext();

            _logger.LogInformation(
                "Step {StepName} codebase context: {HasContext}, queries: {QueryCount}, prioritizedFiles: {FileCount}, hasProjectStructure: {HasProjectStructure}, preferCode: {PreferCode}",
                step.Name,
                ragContext is not null ? $"{ragContext.Length} chars" : "none",
                ragQueries.Count,
                fileReferences?.Count ?? 0,
                codebaseContext?.ProjectStructure is not null,
                isCodingTask);

            // Check if tools are defined for this prompt
            var promptToolNames = _promptRegistry.GetTools(promptName);
            var availableTools = promptToolNames
                .Select(name => _toolRegistry.GetTool(name))
                .Where(t => t is not null)
                .Cast<ToolDefinition>()
                .ToList();

            AgentOutput output;
            IReadOnlyList<ReActStep>? toolSteps = null;

            if (availableTools.Count > 0)
            {
                // Use ReAct executor for tool-enabled execution
                _logger.LogInformation(
                    "Step {StepName} has {ToolCount} tools available: {Tools}",
                    step.Name,
                    availableTools.Count,
                    string.Join(", ", availableTools.Select(t => t.ToolId)));

                var llmProvider = _llmProviderRegistry.GetDefaultProvider()
                    ?? throw new InvalidOperationException("No LLM provider available");

                // Build the full task with RAG context
                var taskWithContext = ragContext is not null
                    ? $"{prompt}\n\n## Relevant Context from Knowledge Base\n{ragContext}"
                    : prompt;

                var reactOptions = new ReActOptions
                {
                    WorkingDirectory = workflow.WorktreePath,
                    MaxSteps = 15,
                    Temperature = 0.2,
                    RequireConfirmation = false, // Auto-approve for now (human reviews step output)
                };

                // Add overall timeout for step execution (10 minutes max)
                const int StepTimeoutMinutes = 10;
                using var stepTimeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(StepTimeoutMinutes));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, stepTimeoutCts.Token);

                _logger.LogWarning("[STEP-DEBUG] Starting ReAct execution with {Timeout}min timeout", StepTimeoutMinutes);
                var stepExecutionStart = DateTime.UtcNow;

                ReActResult reactResult;
                try
                {
                    reactResult = await _reactExecutor.ExecuteAsync(
                        taskWithContext,
                        availableTools,
                        llmProvider,
                        reactOptions,
                        linkedCts.Token);
                }
                catch (OperationCanceledException) when (stepTimeoutCts.IsCancellationRequested)
                {
                    var elapsed = DateTime.UtcNow - stepExecutionStart;
                    _logger.LogError("[STEP-DEBUG] Step execution TIMED OUT after {Elapsed:F1} minutes", elapsed.TotalMinutes);
                    throw new TimeoutException($"Step execution timed out after {StepTimeoutMinutes} minutes");
                }

                var stepDuration = DateTime.UtcNow - stepExecutionStart;
                _logger.LogWarning("[STEP-DEBUG] ReAct execution completed in {Duration:F1}s", stepDuration.TotalSeconds);

                output = new AgentOutput(
                    Content: reactResult.FinalAnswer,
                    TokensUsed: reactResult.TotalTokensUsed);
                toolSteps = reactResult.Steps;

                _logger.LogInformation(
                    "ReAct execution completed: {Success}, {StepCount} steps, {TokenCount} tokens",
                    reactResult.Success,
                    reactResult.Steps.Count,
                    reactResult.TotalTokensUsed);
            }
            else
            {
                // Standard agent execution without tools
                var context = new AgentContext(prompt, WorkspacePath: workflow.WorktreePath)
                {
                    RagContext = ragContext,
                };
                output = await agent.ExecuteAsync(context, ct);
            }

            stopwatch.Stop();

            step.Status = StepStatus.Completed;
            step.CompletedAt = DateTimeOffset.UtcNow;
            step.Output = JsonSerializer.Serialize(new
            {
                agentId = agent.AgentId,
                content = output.Content,
                artifacts = output.Artifacts,
                tokensUsed = output.TokensUsed,
                durationMs = stopwatch.ElapsedMilliseconds,
                toolSteps = toolSteps?.Select(s => new
                {
                    thought = s.Thought,
                    action = s.Action,
                    actionInput = s.ActionInput,
                    observation = s.Observation,
                }).ToList(),
            });

            workflow.UpdatedAt = DateTimeOffset.UtcNow;

            // Check if all steps are complete AND none need rework
            var allComplete = workflow.Steps.All(s =>
                (s.Status == StepStatus.Completed || s.Status == StepStatus.Skipped) && !s.NeedsRework);

            if (allComplete)
            {
                workflow.Status = WorkflowStatus.Completed;
                workflow.CompletedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                // If there are steps needing rework, keep executing status
                workflow.Status = WorkflowStatus.Executing;
            }

            // Use CancellationToken.None to ensure completion is saved even if HTTP request is cancelled
            await _db.SaveChangesAsync(CancellationToken.None);

            _logger.LogInformation("Executed step {StepId} in workflow {WorkflowId} ({DurationMs}ms)",
                stepId, workflowId, stopwatch.ElapsedMilliseconds);

            return step;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            step.Status = StepStatus.Failed;
            step.CompletedAt = DateTimeOffset.UtcNow;
            step.Error = ex.Message;
            workflow.UpdatedAt = DateTimeOffset.UtcNow;

            // CRITICAL: Use CancellationToken.None here to ensure the status update
            // is saved even when the original token is cancelled. Without this,
            // a cancelled HTTP request would leave the step stuck in "Running" status.
            try
            {
                await _db.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx, "Failed to save step failure status for step {StepId}", stepId);
            }

            _logger.LogError(ex, "Failed to execute step {StepId} in workflow {WorkflowId}", stepId, workflowId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<ExecuteAllResult> ExecuteAllStepsAsync(
        Guid workflowId,
        bool stopOnError = true,
        CancellationToken ct = default)
    {
        var workflow = await _db.Workflows
            .Include(w => w.Steps)
            .FirstOrDefaultAsync(w => w.Id == workflowId, ct)
            ?? throw new InvalidOperationException($"Workflow {workflowId} not found");

        if (workflow.Status is not WorkflowStatus.Planned and not WorkflowStatus.Executing)
        {
            throw new InvalidOperationException(
                $"Workflow must be in 'Planned' or 'Executing' status to execute all steps. Current status: {workflow.Status}");
        }

        var executedSteps = new List<WorkflowStep>();
        var skippedSteps = new List<WorkflowStep>();
        WorkflowStep? failedStep = null;
        string? error = null;
        var stoppedOnError = false;

        // Get pending steps in order
        var pendingSteps = workflow.Steps
            .Where(s => s.Status == StepStatus.Pending || s.NeedsRework)
            .OrderBy(s => s.Order)
            .ToList();

        _logger.LogInformation(
            "ExecuteAllSteps: Processing {PendingCount} pending steps in workflow {WorkflowId} with AutomationMode={AutomationMode}",
            pendingSteps.Count, workflowId, workflow.AutomationMode);

        foreach (var step in pendingSteps)
        {
            ct.ThrowIfCancellationRequested();

            // Check if step can be auto-executed based on automation mode
            var canAutoExecute = CanAutoExecuteStep(workflow.AutomationMode, step);

            if (!canAutoExecute)
            {
                _logger.LogInformation(
                    "Step {StepId} ({StepName}) requires user confirmation, skipping in auto-execute mode",
                    step.Id, step.Name);
                skippedSteps.Add(step);
                continue;
            }

            try
            {
                _logger.LogInformation("Auto-executing step {StepId} ({StepName})", step.Id, step.Name);
                var executedStep = await ExecuteStepAsync(workflowId, step.Id, ct: ct);
                executedSteps.Add(executedStep);

                // Auto-approve in autonomous modes
                if (workflow.AutomationMode is AutomationMode.Autonomous or AutomationMode.FullAutonomous)
                {
                    await ApproveStepAsync(workflowId, step.Id, ct);
                    _logger.LogInformation("Auto-approved step {StepId} ({StepName})", step.Id, step.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Step {StepId} failed during auto-execution", step.Id);
                failedStep = step;
                error = ex.Message;
                stoppedOnError = stopOnError;

                if (stopOnError)
                {
                    break;
                }
            }
        }

        var success = failedStep is null && skippedSteps.Count == 0;

        _logger.LogInformation(
            "ExecuteAllSteps completed: Success={Success}, Executed={ExecutedCount}, Skipped={SkippedCount}, Failed={HasFailed}",
            success, executedSteps.Count, skippedSteps.Count, failedStep is not null);

        return new ExecuteAllResult
        {
            Success = success,
            ExecutedSteps = executedSteps,
            SkippedSteps = skippedSteps,
            FailedStep = failedStep,
            Error = error,
            StoppedOnError = stoppedOnError,
        };
    }

    /// <summary>
    /// Determines if a step can be auto-executed based on the workflow's automation mode.
    /// </summary>
    private static bool CanAutoExecuteStep(AutomationMode automationMode, WorkflowStep step)
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
    public async Task<WorkflowStep> AddStepAsync(
        Guid workflowId,
        string name,
        string capability,
        string? description = null,
        int? afterOrder = null,
        CancellationToken ct = default)
    {
        var workflow = await _db.Workflows
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

        var step = new WorkflowStep
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflowId,
            Order = newOrder,
            Name = name,
            Capability = capability,
            Description = description,
            Status = StepStatus.Pending,
        };

        _db.WorkflowSteps.Add(step);
        workflow.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Added step {StepId} to workflow {WorkflowId} at order {Order}",
            step.Id, workflowId, newOrder);

        return step;
    }

    /// <inheritdoc/>
    public async Task RemoveStepAsync(Guid workflowId, Guid stepId, CancellationToken ct = default)
    {
        var workflow = await _db.Workflows
            .Include(w => w.Steps)
            .FirstOrDefaultAsync(w => w.Id == workflowId, ct)
            ?? throw new InvalidOperationException($"Workflow {workflowId} not found");

        var step = workflow.Steps.FirstOrDefault(s => s.Id == stepId);
        if (step is null)
        {
            return;
        }

        var removedOrder = step.Order;
        _db.WorkflowSteps.Remove(step);

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
    public async Task<Workflow> CompleteAsync(Guid workflowId, CancellationToken ct = default)
    {
        var workflow = await _db.Workflows
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
            var pushResult = await _gitService.PushAsync(workflow.WorktreePath, setUpstream: true, ct);
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

        workflow.Status = WorkflowStatus.Completed;
        workflow.CompletedAt = DateTimeOffset.UtcNow;
        workflow.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Completed workflow {WorkflowId}", workflowId);
        return workflow;
    }

    private static string BuildPullRequestBody(Workflow workflow)
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

    private static string BuildSquashCommitMessage(Workflow workflow)
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
    public async Task<Workflow> CancelAsync(Guid workflowId, CancellationToken ct = default)
    {
        var workflow = await _db.Workflows.FindAsync([workflowId], ct)
            ?? throw new InvalidOperationException($"Workflow {workflowId} not found");

        workflow.Status = WorkflowStatus.Cancelled;
        workflow.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Cancelled workflow {WorkflowId}", workflowId);
        return workflow;
    }

    /// <inheritdoc/>
    public async Task<WorkflowChatResponse> ChatAsync(Guid workflowId, string message, CancellationToken ct = default)
    {
        var workflow = await _db.Workflows
            .Include(w => w.Steps.OrderBy(s => s.Order))
            .FirstOrDefaultAsync(w => w.Id == workflowId, ct)
            ?? throw new InvalidOperationException($"Workflow {workflowId} not found");

        // Get a chat-capable agent
        var chatAgent = _agentRegistry.GetBestForCapability(Capabilities.Chat)
            ?? throw new InvalidOperationException("No chat agent available");

        // Different prompts based on workflow status
        var prompt = workflow.Status == WorkflowStatus.Analyzed
            ? _promptRegistry.Render("workflow-chat-analyzed", new
            {
                workflow = new { title = workflow.Title, description = workflow.Description, status = workflow.Status.ToString() },
                analysisSummary = ExtractAnalysisSummary(workflow.AnalyzedContext),
                message,
            })
            : _promptRegistry.Render("workflow-chat-planning", new
            {
                workflow = new { title = workflow.Title, status = workflow.Status.ToString() },
                steps = workflow.Steps.Select(s => new { s.Order, s.Name, s.Capability, status = s.Status.ToString() }),
                message,
            });

        var context = new AgentContext(prompt, WorkspacePath: workflow.WorktreePath);
        var output = await chatAgent.ExecuteAsync(context, ct);

        var response = new WorkflowChatResponse { Response = output.Content };
        var stepsAdded = new List<WorkflowStep>();
        var stepsRemoved = new List<Guid>();
        var analysisUpdated = false;

        // Parse any actions from the response
        if (output.Content.Contains("ACTION: REANALYZE"))
        {
            // User wants to re-analyze with additional context
            var additionalContext = ParseReanalyzeAction(output.Content);
            if (!string.IsNullOrEmpty(additionalContext))
            {
                // Append the additional context to the workflow description
                workflow.Description = $"{workflow.Description}\n\nAdditional context: {additionalContext}";
                await _db.SaveChangesAsync(ct);

                // Re-run analysis
                await AnalyzeAsync(workflowId, ct);
                analysisUpdated = true;
            }
        }
        else if (output.Content.Contains("ACTION: ADD_STEP"))
        {
            var parsed = ParseAddStepAction(output.Content);
            // Validate that we got real values, not placeholder text
            if (parsed is not null &&
                !parsed.Value.Name.Contains("[") &&
                !parsed.Value.Capability.Contains("[") &&
                IsValidCapability(parsed.Value.Capability))
            {
                var newStep = await AddStepAsync(workflowId, parsed.Value.Name, parsed.Value.Capability, parsed.Value.Description, ct: ct);
                stepsAdded.Add(newStep);
            }
        }
        else if (output.Content.Contains("ACTION: REMOVE_STEP"))
        {
            var stepNumber = ParseRemoveStepAction(output.Content);
            if (stepNumber.HasValue)
            {
                var stepToRemove = workflow.Steps.FirstOrDefault(s => s.Order == stepNumber.Value);
                if (stepToRemove is not null)
                {
                    stepsRemoved.Add(stepToRemove.Id);
                    await RemoveStepAsync(workflowId, stepToRemove.Id, ct);
                }
            }
        }
        else if (output.Content.Contains("ACTION: SPLIT_STEP"))
        {
            var splitResult = ParseSplitStepAction(output.Content);
            if (splitResult is not null && splitResult.Value.NewSteps.Count > 0)
            {
                var stepToSplit = workflow.Steps.FirstOrDefault(s => s.Order == splitResult.Value.StepNumber);
                if (stepToSplit is not null)
                {
                    var insertAfterOrder = stepToSplit.Order - 1; // Insert at the position of the removed step

                    // Remove the original step
                    stepsRemoved.Add(stepToSplit.Id);
                    await RemoveStepAsync(workflowId, stepToSplit.Id, ct);

                    // Add the new steps in order
                    foreach (var newStep in splitResult.Value.NewSteps)
                    {
                        if (!newStep.Name.Contains("[") &&
                            !newStep.Capability.Contains("[") &&
                            IsValidCapability(newStep.Capability))
                        {
                            var addedStep = await AddStepAsync(
                                workflowId,
                                newStep.Name,
                                newStep.Capability,
                                newStep.Description,
                                afterOrder: insertAfterOrder,
                                ct: ct);
                            stepsAdded.Add(addedStep);
                            insertAfterOrder = addedStep.Order; // Next step goes after this one
                        }
                    }

                    _logger.LogInformation(
                        "Split step {StepNumber} into {NewStepCount} steps in workflow {WorkflowId}",
                        splitResult.Value.StepNumber,
                        stepsAdded.Count,
                        workflowId);
                }
            }
        }

        // Persist chat history
        await AppendWorkflowChatHistoryAsync(workflow, message, output.Content, ct);

        return response with
        {
            PlanModified = stepsAdded.Count > 0 || stepsRemoved.Count > 0,
            StepsAdded = stepsAdded,
            StepsRemoved = stepsRemoved,
            AnalysisUpdated = analysisUpdated,
        };
    }

    /// <summary>
    /// Appends a message exchange to the workflow's chat history.
    /// </summary>
    private async Task AppendWorkflowChatHistoryAsync(Workflow workflow, string userMessage, string assistantResponse, CancellationToken ct)
    {
        var history = new List<ChatMessage>();
        if (!string.IsNullOrEmpty(workflow.ChatHistory))
        {
            try
            {
                history = JsonSerializer.Deserialize<List<ChatMessage>>(workflow.ChatHistory) ?? [];
            }
            catch { /* ignore parse errors */ }
        }

        history.Add(new ChatMessage("user", userMessage));
        history.Add(new ChatMessage("assistant", assistantResponse));
        workflow.ChatHistory = JsonSerializer.Serialize(history);

        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<WorkflowStep> ApproveStepAsync(Guid workflowId, Guid stepId, CancellationToken ct = default)
    {
        var workflow = await _db.Workflows
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
    public async Task<WorkflowStep> RejectStepAsync(Guid workflowId, Guid stepId, string? feedback = null, CancellationToken ct = default)
    {
        var workflow = await _db.Workflows
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
    public async Task<WorkflowStep> SkipStepAsync(Guid workflowId, Guid stepId, string? reason = null, CancellationToken ct = default)
    {
        var workflow = await _db.Workflows
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
    public async Task<WorkflowStep> ResetStepAsync(Guid workflowId, Guid stepId, CancellationToken ct = default)
    {
        var workflow = await _db.Workflows
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
        if (workflow.Status is WorkflowStatus.Completed or WorkflowStatus.Failed)
        {
            workflow.Status = WorkflowStatus.Executing;
        }

        workflow.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Step {StepId} reset from {PreviousStatus} to Pending in workflow {WorkflowId}",
            stepId, previousStatus, workflowId);
        return step;
    }

    /// <inheritdoc/>
    public async Task<(WorkflowStep Step, string Response)> ChatWithStepAsync(Guid workflowId, Guid stepId, string message, CancellationToken ct = default)
    {
        var workflow = await _db.Workflows
            .Include(w => w.Steps)
            .FirstOrDefaultAsync(w => w.Id == workflowId, ct)
            ?? throw new InvalidOperationException($"Workflow {workflowId} not found");

        var step = workflow.Steps.FirstOrDefault(s => s.Id == stepId)
            ?? throw new InvalidOperationException($"Step {stepId} not found in workflow");

        // Get the assigned agent or find one by capability
        var agent = step.AssignedAgentId is not null
            ? _agentRegistry.GetAgent(step.AssignedAgentId)
            : _agentRegistry.GetBestForCapability(step.Capability, step.Language);

        if (agent is null)
        {
            throw new InvalidOperationException($"No agent found for step {stepId}");
        }

        // Build context for the chat
        var contextBuilder = new System.Text.StringBuilder();
        contextBuilder.AppendLine($"## Step Context");
        contextBuilder.AppendLine($"**Step Name:** {step.Name}");
        contextBuilder.AppendLine($"**Step Description:** {step.Description}");
        contextBuilder.AppendLine($"**Capability:** {step.Capability}");
        contextBuilder.AppendLine($"**Current Status:** {step.Status}");
        if (!string.IsNullOrEmpty(step.Output))
        {
            contextBuilder.AppendLine();
            contextBuilder.AppendLine("## Previous Output");
            contextBuilder.AppendLine(step.Output);
        }
        contextBuilder.AppendLine();
        contextBuilder.AppendLine("## User Message");
        contextBuilder.AppendLine(message);

        // Get agent response
        var agentContext = new AgentContext(contextBuilder.ToString(), WorkspacePath: workflow.WorktreePath);
        var output = await agent.ExecuteAsync(agentContext, ct);
        var response = output.Content ?? "No response from agent.";

        // Update chat history
        var history = new List<ChatMessage>();
        if (!string.IsNullOrEmpty(step.ChatHistory))
        {
            try
            {
                history = JsonSerializer.Deserialize<List<ChatMessage>>(step.ChatHistory) ?? [];
            }
            catch { /* ignore parse errors */ }
        }
        history.Add(new ChatMessage("user", message));
        history.Add(new ChatMessage("assistant", response));
        step.ChatHistory = JsonSerializer.Serialize(history);

        // If the user is refining the step before execution, update the description
        if (step.Status == StepStatus.Pending && message.Contains("focus", StringComparison.OrdinalIgnoreCase))
        {
            // Extract any refined description from the response
            // This is a simple heuristic - could be improved
            step.Description = $"{step.Description} (Refined: {message})";
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Chat with step {StepId} in workflow {WorkflowId}: {MessagePreview}", stepId, workflowId, message.Length > 50 ? message[..50] + "..." : message);
        return (step, response);
    }

    private record ChatMessage(string Role, string Content);

    /// <summary>
    /// Determines if a provider supports structured output well enough to use for workflow planning.
    /// OpenAI and Azure OpenAI have full schema support; Ollama has basic JSON mode only.
    /// </summary>
    private static bool CanUseStructuredOutput(ILlmProvider provider)
    {
        // Azure OpenAI and OpenAI have full schema enforcement
        var providerId = provider.ProviderId.ToLowerInvariant();
        return providerId is "azureopenai" or "openai";
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

    private async Task<string?> GetRagContextAsync(
        string query,
        string? workspacePath,
        CancellationToken ct)
    {
        try
        {
            var options = new RagQueryOptions
            {
                TopK = 5,
                MinScore = 0.3,
                SourcePathPrefix = workspacePath,
            };

            var results = await _ragService.QueryAsync(query, options, ct);

            if (results.Count == 0)
            {
                _logger.LogDebug("No RAG results found for query: {Query}", query);
                return null;
            }

            _logger.LogInformation(
                "Found {Count} RAG results for query, scores: {Scores}",
                results.Count,
                string.Join(", ", results.Select(r => r.Score.ToString("F2"))));

            var sb = new System.Text.StringBuilder();
            foreach (var result in results)
            {
                sb.AppendLine("---");
                if (!string.IsNullOrEmpty(result.SourcePath))
                {
                    sb.AppendLine($"Source: {result.SourcePath}");
                }
                sb.AppendLine($"Relevance: {result.Score:P0}");
                sb.AppendLine();
                sb.AppendLine(result.Text);
                sb.AppendLine();
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RAG query failed, proceeding without context");
            return null;
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
    }

    /// <summary>
    /// Builds capability-specific RAG queries for a step.
    /// Different capabilities need different types of context.
    /// </summary>
    private static (List<string> Queries, List<string> FileReferences) BuildRagQueriesForStep(WorkflowStep step, Workflow workflow)
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
    private static List<string> BuildRagQueriesForAnalysis(Workflow workflow)
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

    /// <summary>
    /// Queries RAG with multiple queries and combines results.
    /// </summary>
    private async Task<string?> GetRagContextForStepAsync(
        List<string> queries,
        string? workspacePath,
        CancellationToken ct)
    {
        try
        {
            var allResults = new List<(string Query, RagResult Result)>();

            foreach (var query in queries)
            {
                var options = new RagQueryOptions
                {
                    TopK = 5, // Get top 5 per query for richer context
                    MinScore = 0.35, // Slightly lower threshold to capture more relevant content
                    SourcePathPrefix = workspacePath,
                };

                var results = await _ragService.QueryAsync(query, options, ct);
                foreach (var result in results)
                {
                    allResults.Add((query, result));
                }
            }

            if (allResults.Count == 0)
            {
                _logger.LogDebug("No RAG results found for {QueryCount} queries", queries.Count);
                return null;
            }

            // Deduplicate by content (same file/chunk might match multiple queries)
            var uniqueResults = allResults
                .GroupBy(r => r.Result.ContentId + "_" + r.Result.ChunkIndex)
                .Select(g => g.OrderByDescending(r => r.Result.Score).First())
                .OrderByDescending(r => r.Result.Score)
                .Take(20) // Limit to top 20 unique results for richer context
                .ToList();

            _logger.LogInformation(
                "Found {TotalCount} RAG results from {QueryCount} queries, {UniqueCount} unique, top scores: {Scores}",
                allResults.Count,
                queries.Count,
                uniqueResults.Count,
                string.Join(", ", uniqueResults.Take(5).Select(r => r.Result.Score.ToString("F2"))));

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("## Relevant Project Context");
            sb.AppendLine();

            foreach (var (query, result) in uniqueResults)
            {
                sb.AppendLine("---");
                if (!string.IsNullOrEmpty(result.SourcePath))
                {
                    sb.AppendLine($"**File:** {result.SourcePath}");
                }
                sb.AppendLine($"**Relevance:** {result.Score:P0}");
                sb.AppendLine();
                sb.AppendLine(result.Text);
                sb.AppendLine();
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RAG query failed, proceeding without context");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<WorkflowStep> ReassignStepAsync(
        Guid workflowId,
        Guid stepId,
        string agentId,
        CancellationToken ct = default)
    {
        var workflow = await _db.Workflows
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
    public async Task<WorkflowStep> UpdateStepDescriptionAsync(
        Guid workflowId,
        Guid stepId,
        string description,
        CancellationToken ct = default)
    {
        var workflow = await _db.Workflows
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
