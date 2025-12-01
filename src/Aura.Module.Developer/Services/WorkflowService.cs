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
    public async Task<Workflow> CreateFromIssueAsync(Guid issueId, CancellationToken ct = default)
    {
        var issue = await _db.Issues
            .Include(i => i.Workflow)
            .FirstOrDefaultAsync(i => i.Id == issueId, ct)
            ?? throw new InvalidOperationException($"Issue {issueId} not found");

        if (issue.Workflow is not null)
        {
            throw new InvalidOperationException($"Issue {issueId} already has a workflow");
        }

        // Create a branch name from the issue
        var branchName = $"feature/issue-{issue.Id:N}";

        // Create the workflow
        var workflow = new Workflow
        {
            Id = Guid.NewGuid(),
            IssueId = issueId,
            RepositoryPath = issue.RepositoryPath,
            WorkItemId = $"local:{issue.Id}",
            WorkItemTitle = issue.Title,
            WorkItemDescription = issue.Description,
            GitBranch = branchName,
            Status = WorkflowStatus.Created,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        // Try to create a worktree if repository path is set
        if (!string.IsNullOrEmpty(issue.RepositoryPath))
        {
            var worktreeResult = await _worktreeService.CreateAsync(
                issue.RepositoryPath,
                branchName,
                worktreePath: null, // Let it choose default
                baseBranch: null,   // Use current HEAD
                ct);

            if (worktreeResult.Success && worktreeResult.Value is not null)
            {
                workflow.WorkspacePath = worktreeResult.Value.Path;
                _logger.LogInformation("Created worktree at {Path} for workflow {WorkflowId}",
                    workflow.WorkspacePath, workflow.Id);
            }
            else
            {
                _logger.LogWarning("Failed to create worktree: {Error}. Using repository path instead.",
                    worktreeResult.Error);
                workflow.WorkspacePath = issue.RepositoryPath;
            }
        }

        // Update issue status
        issue.Status = IssueStatus.InProgress;
        issue.UpdatedAt = DateTimeOffset.UtcNow;

        _db.Workflows.Add(workflow);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created workflow {WorkflowId} from issue {IssueId}", workflow.Id, issueId);
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
            .Include(w => w.Issue)
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
            .Include(w => w.Issue)
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<Workflow> DigestAsync(Guid workflowId, CancellationToken ct = default)
    {
        var workflow = await _db.Workflows
            .Include(w => w.Issue)
            .FirstOrDefaultAsync(w => w.Id == workflowId, ct)
            ?? throw new InvalidOperationException($"Workflow {workflowId} not found");

        workflow.Status = WorkflowStatus.Digesting;
        workflow.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        try
        {
            // TODO: RAG indexing is slow - move to background job for production
            // For now, skip RAG indexing to keep the silver thread responsive
            // if (!string.IsNullOrEmpty(workflow.WorkspacePath))
            // {
            //     var indexOptions = new RagIndexOptions
            //     {
            //         IncludePatterns = ["*.cs", "*.md", "*.json", "*.csproj"],
            //         ExcludePatterns = ["**/bin/**", "**/obj/**", "**/node_modules/**"],
            //         Recursive = true,
            //     };
            //     await _ragService.IndexDirectoryAsync(workflow.WorkspacePath, indexOptions, ct);
            // }

            // Get the issue-digester agent
            var digester = _agentRegistry.GetBestForCapability("digestion");
            if (digester is null)
            {
                throw new InvalidOperationException("No agent with 'digestion' capability found");
            }

            // Build the prompt from template
            var prompt = _promptRegistry.Render("workflow-digest", new
            {
                title = workflow.WorkItemTitle,
                description = workflow.WorkItemDescription ?? "No description provided.",
                workspacePath = workflow.WorkspacePath ?? workflow.RepositoryPath ?? "Not specified",
            });

            var context = new AgentContext(prompt, WorkspacePath: workflow.WorkspacePath);
            var output = await digester.ExecuteAsync(context, ct);

            workflow.DigestedContext = JsonSerializer.Serialize(new
            {
                agentId = digester.AgentId,
                analysis = output.Content,
                tokensUsed = output.TokensUsed,
                timestamp = DateTimeOffset.UtcNow,
            });

            workflow.Status = WorkflowStatus.Digested;
            workflow.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Digested workflow {WorkflowId}", workflowId);
            return workflow;
        }
        catch (Exception ex)
        {
            workflow.Status = WorkflowStatus.Failed;
            workflow.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            _logger.LogError(ex, "Failed to digest workflow {WorkflowId}", workflowId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<Workflow> PlanAsync(Guid workflowId, CancellationToken ct = default)
    {
        var workflow = await _db.Workflows
            .Include(w => w.Issue)
            .FirstOrDefaultAsync(w => w.Id == workflowId, ct)
            ?? throw new InvalidOperationException($"Workflow {workflowId} not found");

        if (workflow.Status != WorkflowStatus.Digested)
        {
            throw new InvalidOperationException($"Workflow must be digested before planning. Current status: {workflow.Status}");
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
                title = workflow.WorkItemTitle,
                description = workflow.WorkItemDescription ?? "No description provided.",
                digestedContext = workflow.DigestedContext ?? "No analysis available.",
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
                    issueTitle = workflow.WorkItemTitle,
                    codeToReview = codeToReview.ToString(),
                });
            }
            else
            {
                // For non-review steps, include the digested analysis
                var analysis = string.Empty;
                if (workflow.DigestedContext is not null)
                {
                    try
                    {
                        var digest = JsonSerializer.Deserialize<JsonElement>(workflow.DigestedContext);
                        if (digest.TryGetProperty("analysis", out var analysisElement))
                        {
                            analysis = $"Analysis: {analysisElement.GetString()}";
                        }
                    }
                    catch
                    {
                        analysis = $"Analysis: {workflow.DigestedContext}";
                    }
                }

                prompt = _promptRegistry.Render("step-execute", new
                {
                    stepName = step.Name,
                    stepDescription = step.Description ?? "No additional description.",
                    issueTitle = workflow.WorkItemTitle,
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
            .Include(w => w.Issue)
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

        if (workflow.Issue is not null)
        {
            workflow.Issue.Status = IssueStatus.Completed;
            workflow.Issue.UpdatedAt = DateTimeOffset.UtcNow;
        }

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

        var prompt = $"""
            You are helping with a development workflow. The user wants to modify or discuss the plan.

            ## Current Workflow
            Issue: {workflow.WorkItemTitle}
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

        var context = new AgentContext(prompt, WorkspacePath: workflow.WorkspacePath);
        var output = await chatAgent.ExecuteAsync(context, ct);

        var response = new WorkflowChatResponse { Response = output.Content };
        var stepsAdded = new List<WorkflowStep>();
        var stepsRemoved = new List<Guid>();

        // Parse any actions from the response
        if (output.Content.Contains("ACTION: ADD_STEP"))
        {
            var parsed = ParseAddStepAction(output.Content);
            if (parsed is not null)
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

    private sealed record StepDefinition
    {
        public required string Name { get; init; }
        public required string Capability { get; init; }
        public string? Language { get; init; }
        public string? Description { get; init; }
    }
}
