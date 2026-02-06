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

namespace Aura.Module.Developer.Services;

public sealed partial class StoryService
{
    /// <inheritdoc/>
    public async Task<StoryRunResult> RunAsync(Guid storyId, string? githubToken = null, CancellationToken ct = default)
    {
        var story = await _db.Stories.Include(w => w.Steps).FirstOrDefaultAsync(w => w.Id == storyId, ct) ?? throw new InvalidOperationException($"Story {storyId} not found");
        // Story must have steps to run
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
        // Determine current wave to execute
        var currentWave = story.CurrentWave == 0 ? 1 : story.CurrentWave;
        // Check if we're waiting for a quality gate
        if (story.Status == StoryStatus.GatePending)
        {
            // Skip quality gate for intermediate waves
            // Intermediate waves may have partial implementations that don't compile
            // (e.g., wave 1 updates interfaces, wave 2 updates implementations)
            // The quality gate will run after the final wave completes
            _logger.LogInformation("Proceeding past gate for wave {Wave}/{TotalWaves} - will validate after final wave", currentWave - 1, waveCount);
            // Proceed to execute the current wave (no gate check needed)
        }

        // Get steps for current wave that are pending
        var waveSteps = steps.Where(s => s.Wave == currentWave && s.Status == StepStatus.Pending).ToList();
        if (waveSteps.Count == 0)
        {
            // Check if all steps are complete
            if (steps.All(s => s.Status == StepStatus.Completed || s.Status == StepStatus.Skipped))
            {
                story.Status = StoryStatus.ReadyToComplete;
                story.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);
                return new StoryRunResult
                {
                    StoryId = storyId,
                    Status = StoryStatus.ReadyToComplete,
                    CurrentWave = waveCount,
                    TotalWaves = waveCount,
                    StartedSteps = [],
                    CompletedSteps = steps.Where(s => s.Status == StepStatus.Completed).ToList(),
                    FailedSteps = [],
                };
            }

            // Move to next wave
            currentWave++;
            if (currentWave > waveCount)
            {
                story.Status = StoryStatus.ReadyToComplete;
                story.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);
                return new StoryRunResult
                {
                    StoryId = storyId,
                    Status = StoryStatus.ReadyToComplete,
                    CurrentWave = waveCount,
                    TotalWaves = waveCount,
                    StartedSteps = [],
                    CompletedSteps = steps.Where(s => s.Status == StepStatus.Completed).ToList(),
                    FailedSteps = [],
                };
            }

            waveSteps = steps.Where(s => s.Wave == currentWave && s.Status == StepStatus.Pending).ToList();
        }

        // Update story status
        story.Status = StoryStatus.Executing;
        story.CurrentWave = currentWave;
        story.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Running wave {Wave}/{TotalWaves} with {StepCount} steps (max parallelism: {MaxParallelism})", currentWave, waveCount, waveSteps.Count, story.MaxParallelism);
        // Get completed steps from prior waves to provide context
        var priorCompletedSteps = steps.Where(s => s.Wave < currentWave && s.Status == StepStatus.Completed).ToList();
        // Resolve executor for this wave (use first step to resolve, all steps in wave use same executor)
        var worktreeName = Path.GetFileName(story.WorktreePath);
        var executor = await _stepExecutorRegistry.ResolveExecutorAsync(waveSteps[0], story, ct) ?? throw new InvalidOperationException("No executor available for story");
        _logger.LogInformation("[{WorktreeName}] Using {Executor} for wave {Wave}", worktreeName, executor.DisplayName, currentWave);
        await executor.ExecuteStepsAsync(waveSteps, story, story.MaxParallelism, priorCompletedSteps, ct);
        await _db.SaveChangesAsync(ct);
        var completedSteps = waveSteps.Where(s => s.Status == StepStatus.Completed).ToList();
        var failedSteps = waveSteps.Where(s => s.Status == StepStatus.Failed).ToList();
        // Determine next status
        if (failedSteps.Count > 0)
        {
            // Some steps failed - mark story as failed
            story.Status = StoryStatus.Failed;
            story.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            return new StoryRunResult
            {
                StoryId = storyId,
                Status = StoryStatus.Failed,
                CurrentWave = currentWave,
                TotalWaves = waveCount,
                StartedSteps = waveSteps,
                CompletedSteps = completedSteps,
                FailedSteps = failedSteps,
                Error = $"{failedSteps.Count} step(s) failed in wave {currentWave}",
            };
        }

        // Wave completed successfully
        if (currentWave < waveCount)
        {
            // More waves to go - skip quality gate for intermediate waves
            // Intermediate waves may have partial implementations that don't compile
            // (e.g., wave 1 updates interfaces, wave 2 updates implementations)
            _logger.LogInformation("Skipping quality gate for intermediate wave {Wave}/{TotalWaves} - will validate after final wave", currentWave, waveCount);
            story.Status = StoryStatus.GatePending;
            story.CurrentWave = currentWave + 1;
            story.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            return new StoryRunResult
            {
                StoryId = storyId,
                Status = StoryStatus.GatePending,
                CurrentWave = currentWave,
                TotalWaves = waveCount,
                StartedSteps = waveSteps,
                CompletedSteps = completedSteps,
                FailedSteps = [],
            };
        }

        // All waves complete - run quality gate before marking ready
        _logger.LogInformation("All waves complete, running final quality gate");
        var gateResult = await _qualityGateService.RunFullGateAsync(story.WorktreePath, currentWave, ct);
        if (!gateResult.Passed)
        {
            if (gateResult.WasCancelled)
            {
                _logger.LogWarning("Quality gate was cancelled after final wave {Wave}. Story remains pending for retry.", currentWave);
                story.Status = StoryStatus.GatePending;
                story.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);
                return new StoryRunResult
                {
                    StoryId = storyId,
                    Status = StoryStatus.GatePending,
                    CurrentWave = currentWave,
                    TotalWaves = waveCount,
                    StartedSteps = waveSteps,
                    CompletedSteps = completedSteps,
                    FailedSteps = [],
                    GateResult = gateResult,
                    Error = "Quality gate was cancelled. Retry when ready.",
                };
            }

            _logger.LogWarning("Quality gate failed after final wave {Wave}: {Error}", currentWave, gateResult.Error);
            story.Status = StoryStatus.GateFailed;
            story.GateResult = JsonSerializer.Serialize(new { gateResult.Passed, gateResult.Error, gateResult.BuildOutput, gateResult.TestOutput });
            story.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            return new StoryRunResult
            {
                StoryId = storyId,
                Status = StoryStatus.GateFailed,
                CurrentWave = currentWave,
                TotalWaves = waveCount,
                StartedSteps = waveSteps,
                CompletedSteps = completedSteps,
                FailedSteps = [],
                GateResult = gateResult,
                Error = $"Quality gate failed: {gateResult.Error}",
            };
        }

        // Quality gate passed - ready for finalization
        story.Status = StoryStatus.ReadyToComplete;
        story.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Story {StoryId} all steps complete, ready for finalization", storyId);
        return new StoryRunResult
        {
            StoryId = storyId,
            Status = StoryStatus.ReadyToComplete,
            CurrentWave = waveCount,
            TotalWaves = waveCount,
            StartedSteps = waveSteps,
            CompletedSteps = steps.Where(s => s.Status == StepStatus.Completed).ToList(),
            FailedSteps = [],
        };
    }

    /// <inheritdoc/>
    public async Task<StoryOrchestratorStatus> GetOrchestratorStatusAsync(Guid storyId, CancellationToken ct = default)
    {
        var story = await _db.Stories.Include(w => w.Steps).FirstOrDefaultAsync(w => w.Id == storyId, ct) ?? throw new InvalidOperationException($"Story {storyId} not found");
        var steps = story.Steps.ToList();
        var waveCount = steps.Count > 0 ? steps.Max(s => s.Wave) : 0;
        return new StoryOrchestratorStatus
        {
            StoryId = storyId,
            Status = story.Status,
            CurrentWave = story.CurrentWave,
            TotalWaves = waveCount,
            Steps = steps,
            MaxParallelism = story.MaxParallelism,
        };
    }

    /// <inheritdoc/>
    public async Task<StoryStep> ExecuteStepAsync(Guid workflowId, Guid stepId, string? agentIdOverride = null, CancellationToken ct = default)
    {
        var workflow = await _db.Stories.Include(w => w.Steps).FirstOrDefaultAsync(w => w.Id == workflowId, ct) ?? throw new InvalidOperationException($"Workflow {workflowId} not found");
        var step = workflow.Steps.FirstOrDefault(s => s.Id == stepId) ?? throw new InvalidOperationException($"Step {stepId} not found in workflow {workflowId}");
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
            step.Approval = null; // Clear approval since we're re-doing it
            step.ApprovalFeedback = null;
            // Mark all subsequent steps as needing rework
            var subsequentSteps = workflow.Steps.Where(s => s.Order > step.Order && s.Status == StepStatus.Completed).ToList();
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
                throw new InvalidOperationException($"No agent found for capability '{step.Capability}'" + (step.Language is not null ? $" with language '{step.Language}'" : ""));
            }
        }

        // Update status to running
        step.Status = StepStatus.Running;
        step.AssignedAgentId = agent.AgentId;
        step.StartedAt = DateTimeOffset.UtcNow;
        step.Attempts++;
        workflow.Status = StoryStatus.Executing;
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
                var codingSteps = workflow.Steps.Where(s => (s.Capability == "coding" || s.Capability == "documentation") && s.Status == StepStatus.Completed && s.Output is not null).OrderBy(s => s.Order).ToList();
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
                prompt = _promptRegistry.Render(promptName, new { stepName = step.Name, stepDescription = step.Description ?? "No additional description.", issueTitle = workflow.Title, analysis, revisionFeedback, });
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
                fileReferences = ExtractFileReferences(step.Name).Concat(ExtractFileReferences(step.Description ?? string.Empty)).Distinct().ToList();
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
            var contextOptions = isCodingTask ? CodebaseContextOptions.ForCoding(ragQueries, prioritizeFiles: fileReferences) : CodebaseContextOptions.ForDocumentation(ragQueries, fileReferences);
            var ragSourcePath = workflow.RepositoryPath ?? workflow.WorktreePath;
            var codebaseContext = ragSourcePath is not null ? await _codebaseContextService.GetContextAsync(ragSourcePath, contextOptions, ct) : null;
            var ragContext = codebaseContext?.ToPromptContext();
            _logger.LogInformation("Step {StepName} codebase context: {HasContext}, queries: {QueryCount}, prioritizedFiles: {FileCount}, hasProjectStructure: {HasProjectStructure}, preferCode: {PreferCode}", step.Name, ragContext is not null ? $"{ragContext.Length} chars" : "none", ragQueries.Count, fileReferences?.Count ?? 0, codebaseContext?.ProjectStructure is not null, isCodingTask);
            // Check if tools are defined for this prompt
            var promptToolNames = _promptRegistry.GetTools(promptName);
            var availableTools = promptToolNames.Select(name => _toolRegistry.GetTool(name)).Where(t => t is not null).Cast<ToolDefinition>().ToList();
            AgentOutput output;
            IReadOnlyList<ReActStep>? toolSteps = null;
            if (availableTools.Count > 0)
            {
                // Use ReAct executor for tool-enabled execution
                _logger.LogInformation("Step {StepName} has {ToolCount} tools available: {Tools}", step.Name, availableTools.Count, string.Join(", ", availableTools.Select(t => t.ToolId)));
                var llmProvider = _llmProviderRegistry.GetDefaultProvider() ?? throw new InvalidOperationException("No LLM provider available");
                // Build the full task with RAG context
                var taskWithContext = ragContext is not null ? $"{prompt}\n\n## Relevant Context from Knowledge Base\n{ragContext}" : prompt;
                // Create validation tracker to enforce code.validate before finish
                var validationTracker = new ValidationTracker();
                var reactOptions = new ReActOptions
                {
                    WorkingDirectory = workflow.WorktreePath,
                    MaxSteps = 15,
                    Temperature = 0.2,
                    RequireConfirmation = false, // Auto-approve for now (human reviews step output)
                    ValidationTracker = validationTracker,
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
                    reactResult = await _reactExecutor.ExecuteAsync(taskWithContext, availableTools, llmProvider, reactOptions, linkedCts.Token);
                }
                catch (OperationCanceledException) when (stepTimeoutCts.IsCancellationRequested)
                {
                    var elapsed = DateTime.UtcNow - stepExecutionStart;
                    _logger.LogError("[STEP-DEBUG] Step execution TIMED OUT after {Elapsed:F1} minutes", elapsed.TotalMinutes);
                    throw new TimeoutException($"Step execution timed out after {StepTimeoutMinutes} minutes");
                }

                var stepDuration = DateTime.UtcNow - stepExecutionStart;
                _logger.LogWarning("[STEP-DEBUG] ReAct execution completed in {Duration:F1}s", stepDuration.TotalSeconds);
                output = new AgentOutput(Content: reactResult.FinalAnswer, TokensUsed: reactResult.TotalTokensUsed);
                toolSteps = reactResult.Steps;
                _logger.LogInformation("ReAct execution completed: {Success}, {StepCount} steps, {TokenCount} tokens", reactResult.Success, reactResult.Steps.Count, reactResult.TotalTokensUsed);
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
            step.Output = JsonSerializer.Serialize(new { agentId = agent.AgentId, content = output.Content, artifacts = output.Artifacts, tokensUsed = output.TokensUsed, durationMs = stopwatch.ElapsedMilliseconds, toolSteps = toolSteps?.Select(s => new { thought = s.Thought, action = s.Action, actionInput = s.ActionInput, observation = s.Observation, }).ToList(), });
            workflow.UpdatedAt = DateTimeOffset.UtcNow;
            // Check if all steps are complete AND none need rework
            var allComplete = workflow.Steps.All(s => (s.Status == StepStatus.Completed || s.Status == StepStatus.Skipped) && !s.NeedsRework);
            if (allComplete)
            {
                // Set to ReadyToComplete - user must call /complete to finalize (squash, push, PR)
                workflow.Status = StoryStatus.ReadyToComplete;
            }
            else
            {
                // If there are steps needing rework, keep executing status
                workflow.Status = StoryStatus.Executing;
            }

            // Use CancellationToken.None to ensure completion is saved even if HTTP request is cancelled
            await _db.SaveChangesAsync(CancellationToken.None);
            _logger.LogInformation("Executed step {StepId} in workflow {WorkflowId} ({DurationMs}ms)", stepId, workflowId, stopwatch.ElapsedMilliseconds);
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
    public async Task<ExecuteAllResult> ExecuteAllStepsAsync(Guid workflowId, bool stopOnError = true, CancellationToken ct = default)
    {
        var workflow = await _db.Stories.Include(w => w.Steps).FirstOrDefaultAsync(w => w.Id == workflowId, ct) ?? throw new InvalidOperationException($"Workflow {workflowId} not found");
        if (workflow.Status is not StoryStatus.Planned and not StoryStatus.Executing)
        {
            throw new InvalidOperationException($"Workflow must be in 'Planned' or 'Executing' status to execute all steps. Current status: {workflow.Status}");
        }

        var executedSteps = new List<StoryStep>();
        var skippedSteps = new List<StoryStep>();
        StoryStep? failedStep = null;
        string? error = null;
        var stoppedOnError = false;
        // Get pending steps in order
        var pendingSteps = workflow.Steps.Where(s => s.Status == StepStatus.Pending || s.NeedsRework).OrderBy(s => s.Order).ToList();
        _logger.LogInformation("ExecuteAllSteps: Processing {PendingCount} pending steps in workflow {WorkflowId} with AutomationMode={AutomationMode}", pendingSteps.Count, workflowId, workflow.AutomationMode);
        foreach (var step in pendingSteps)
        {
            ct.ThrowIfCancellationRequested();
            // Check if step can be auto-executed based on automation mode
            var canAutoExecute = CanAutoExecuteStep(workflow.AutomationMode, step);
            if (!canAutoExecute)
            {
                _logger.LogInformation("Step {StepId} ({StepName}) requires user confirmation, skipping in auto-execute mode", step.Id, step.Name);
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
        _logger.LogInformation("ExecuteAllSteps completed: Success={Success}, Executed={ExecutedCount}, Skipped={SkippedCount}, Failed={HasFailed}", success, executedSteps.Count, skippedSteps.Count, failedStep is not null);
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
}
