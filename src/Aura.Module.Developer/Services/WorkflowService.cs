// <copyright file="WorkflowService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services;

using System.Diagnostics;
using System.Text.Json;
using Aura.Foundation.Agents;
using Aura.Foundation.Git;
using Aura.Foundation.Prompts;
using Aura.Foundation.Rag;
using Aura.Module.Developer.Data;
using Aura.Module.Developer.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Service for managing development workflows.
/// </summary>
public sealed class WorkflowService : IWorkflowService
{
    private readonly DeveloperDbContext _db;
    private readonly IAgentRegistry _agentRegistry;
    private readonly IPromptRegistry _promptRegistry;
    private readonly IGitWorktreeService _worktreeService;
    private readonly IRagService _ragService;
    private readonly ILogger<WorkflowService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowService"/> class.
    /// </summary>
    public WorkflowService(
        DeveloperDbContext db,
        IAgentRegistry agentRegistry,
        IPromptRegistry promptRegistry,
        IGitWorktreeService worktreeService,
        IRagService ragService,
        ILogger<WorkflowService> logger)
    {
        _db = db;
        _agentRegistry = agentRegistry;
        _promptRegistry = promptRegistry;
        _worktreeService = worktreeService;
        _ragService = ragService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Workflow> CreateAsync(
        string title,
        string? description = null,
        string? repositoryPath = null,
        CancellationToken ct = default)
    {
        // Create a branch name from the title
        var sanitizedTitle = new string(title
            .ToLowerInvariant()
            .Replace(' ', '-')
            .Where(c => char.IsLetterOrDigit(c) || c == '-')
            .ToArray());
        var branchName = $"feature/{sanitizedTitle}-{Guid.NewGuid():N}".Substring(0, Math.Min(63, 8 + sanitizedTitle.Length + 33));

        // Create the workflow
        var workflow = new Workflow
        {
            Id = Guid.NewGuid(),
            RepositoryPath = repositoryPath,
            Title = title,
            Description = description,
            GitBranch = branchName,
            Status = WorkflowStatus.Created,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

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
                workflow.WorkspacePath = worktreeResult.Value.Path;
                _logger.LogInformation("Created worktree at {Path} for workflow {WorkflowId}",
                    workflow.WorkspacePath, workflow.Id);
                
                // Index the worktree for RAG
                await IndexWorktreeForRagAsync(workflow.WorkspacePath, ct);
            }
            else
            {
                _logger.LogWarning("Failed to create worktree: {Error}. Using repository path instead.",
                    worktreeResult.Error);
                workflow.WorkspacePath = repositoryPath;
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
    public async Task<IReadOnlyList<Workflow>> ListAsync(WorkflowStatus? status = null, CancellationToken ct = default)
    {
        var query = _db.Workflows.AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(w => w.Status == status.Value);
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
        if (!string.IsNullOrEmpty(workflow.WorkspacePath) &&
            !string.IsNullOrEmpty(workflow.RepositoryPath) &&
            workflow.WorkspacePath != workflow.RepositoryPath)
        {
            var removeResult = await _worktreeService.RemoveAsync(
                workflow.WorkspacePath,
                force: true,
                ct);

            if (!removeResult.Success)
            {
                _logger.LogWarning("Failed to remove worktree {Path}: {Error}",
                    workflow.WorkspacePath, removeResult.Error);
            }
        }

        _db.Workflows.Remove(workflow);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted workflow {WorkflowId}: {Title}", id, workflow.Title);
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
            // Get the issue-digester agent (still uses digestion capability internally)
            var analyzer = _agentRegistry.GetBestForCapability("digestion");
            if (analyzer is null)
            {
                throw new InvalidOperationException("No agent with 'digestion' capability found");
            }

            // Build the prompt from template
            var prompt = _promptRegistry.Render("workflow-digest", new
            {
                title = workflow.Title,
                description = workflow.Description ?? "No description provided.",
                workspacePath = workflow.WorkspacePath ?? workflow.RepositoryPath ?? "Not specified",
            });

            // Query RAG for relevant code context
            var ragContext = await GetRagContextAsync(
                $"{workflow.Title} {workflow.Description}",
                workflow.WorkspacePath ?? workflow.RepositoryPath,
                ct);

            var context = new AgentContext(prompt, WorkspacePath: workflow.WorkspacePath)
            {
                RagContext = ragContext,
            };
            var output = await analyzer.ExecuteAsync(context, ct);

            workflow.AnalyzedContext = JsonSerializer.Serialize(new
            {
                agentId = analyzer.AgentId,
                analysis = output.Content,
                tokensUsed = output.TokensUsed,
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
            var planner = _agentRegistry.GetBestForCapability("analysis");
            if (planner is null)
            {
                throw new InvalidOperationException("No agent with 'analysis' capability found");
            }

            // Build the planning prompt from template
            var prompt = _promptRegistry.Render("workflow-plan", new
            {
                title = workflow.Title,
                description = workflow.Description ?? "No description provided.",
                digestedContext = workflow.AnalyzedContext ?? "No analysis available.",
            });

            var context = new AgentContext(prompt, WorkspacePath: workflow.WorkspacePath);
            var output = await planner.ExecuteAsync(context, ct);

            // Parse the steps from the response
            var steps = ParseStepsFromResponse(output.Content);

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
                rawResponse = output.Content,
                stepCount = steps.Count,
                tokensUsed = output.TokensUsed,
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

            if (step.Capability == "review")
            {
                // For review capability, gather outputs from previous coding steps
                var codeToReview = new System.Text.StringBuilder();
                var codingSteps = workflow.Steps
                    .Where(s => s.Capability == "coding" && s.Status == StepStatus.Completed && s.Output is not null)
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
                });
            }
            else
            {
                // For non-review steps, include the analyzed context
                var analysis = string.Empty;
                if (workflow.AnalyzedContext is not null)
                {
                    try
                    {
                        var digest = JsonSerializer.Deserialize<JsonElement>(workflow.AnalyzedContext);
                        if (digest.TryGetProperty("analysis", out var analysisElement))
                        {
                            analysis = $"Analysis: {analysisElement.GetString()}";
                        }
                    }
                    catch
                    {
                        analysis = $"Analysis: {workflow.AnalyzedContext}";
                    }
                }

                prompt = _promptRegistry.Render("step-execute", new
                {
                    stepName = step.Name,
                    stepDescription = step.Description ?? "No additional description.",
                    issueTitle = workflow.Title,
                    analysis,
                });
            }
            var context = new AgentContext(prompt, WorkspacePath: workflow.WorkspacePath);
            var output = await agent.ExecuteAsync(context, ct);

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
            });

            workflow.UpdatedAt = DateTimeOffset.UtcNow;

            // Check if all steps are complete
            var allComplete = workflow.Steps.All(s => s.Status == StepStatus.Completed || s.Status == StepStatus.Skipped);
            if (allComplete)
            {
                workflow.Status = WorkflowStatus.Completed;
                workflow.CompletedAt = DateTimeOffset.UtcNow;
            }

            await _db.SaveChangesAsync(ct);

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
            await _db.SaveChangesAsync(ct);

            _logger.LogError(ex, "Failed to execute step {StepId} in workflow {WorkflowId}", stepId, workflowId);
            throw;
        }
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

        workflow.Status = WorkflowStatus.Completed;
        workflow.CompletedAt = DateTimeOffset.UtcNow;
        workflow.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Completed workflow {WorkflowId}", workflowId);
        return workflow;
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
        var chatAgent = _agentRegistry.GetBestForCapability("chat")
            ?? _agentRegistry.GetBestForCapability("general")
            ?? throw new InvalidOperationException("No chat agent available");

        // Build context with current plan
        var stepsDescription = workflow.Steps.Any()
            ? string.Join("\n", workflow.Steps.Select(s => $"- Step {s.Order}: {s.Name} ({s.Capability}) - {s.Status}"))
            : "No steps defined yet.";

        // Different prompts based on workflow status
        var prompt = workflow.Status == WorkflowStatus.Analyzed
            ? BuildAnalyzedStatePrompt(workflow, message, stepsDescription)
            : BuildPlanningStatePrompt(workflow, message, stepsDescription);

        var context = new AgentContext(prompt, WorkspacePath: workflow.WorkspacePath);
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

        return response with
        {
            PlanModified = stepsAdded.Count > 0 || stepsRemoved.Count > 0,
            StepsAdded = stepsAdded,
            StepsRemoved = stepsRemoved,
            AnalysisUpdated = analysisUpdated,
        };
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

    private static string BuildAnalyzedStatePrompt(Workflow workflow, string message, string stepsDescription)
    {
        return $"""
            You are helping refine the analysis of a development workflow. The workflow has been analyzed but the user wants to provide more context or clarification before creating the plan.

            ## Current Workflow
            Title: {workflow.Title}
            Description: {workflow.Description}
            Status: {workflow.Status}
            
            ## Current Analysis Summary
            {ExtractAnalysisSummary(workflow.AnalyzedContext)}

            ## User Message
            {message}

            ## Your Task
            The user is providing ADDITIONAL CONTEXT to improve the analysis. You must respond with:

            ACTION: REANALYZE
            CONTEXT: [Write a 1-2 sentence summary of the new context/requirements the user provided]

            Example response:
            ACTION: REANALYZE
            CONTEXT: User clarified that JWT tokens should include refresh tokens and the implementation should focus on API endpoints only, not UI.

            IMPORTANT: 
            - Do NOT suggest adding steps or use ACTION: ADD_STEP - that comes later after "Create Plan"
            - Do NOT include placeholder text like [step name] 
            - Always respond with ACTION: REANALYZE when the user provides clarification
            - The CONTEXT should summarize what the user said, not repeat instructions
            """;
    }

    private static string BuildPlanningStatePrompt(Workflow workflow, string message, string stepsDescription)
    {
        return $"""
            You are helping with a development workflow. The user wants to modify or discuss the plan.

            ## Current Workflow
            Title: {workflow.Title}
            Status: {workflow.Status}

            ## Current Steps
            {stepsDescription}

            ## User Message
            {message}

            ## Instructions
            If the user wants to add a step, respond with:
            ACTION: ADD_STEP
            NAME: [step name]
            CAPABILITY: [coding|testing|review|documentation|fixing]
            DESCRIPTION: [what the step should do]

            If the user wants to remove a step, respond with:
            ACTION: REMOVE_STEP
            STEP_NUMBER: [number]

            Otherwise, just respond naturally to their question.
            """;
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
}
