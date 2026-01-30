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
    public async Task<Story> AnalyzeAsync(Guid workflowId, CancellationToken ct = default)
    {
        var workflow = await _db.Stories.FirstOrDefaultAsync(w => w.Id == workflowId, ct) ?? throw new InvalidOperationException($"Workflow {workflowId} not found");
        workflow.Status = StoryStatus.Analyzing;
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
            var prompt = _promptRegistry.Render("workflow-enrich", new { title = workflow.Title, description = workflow.Description ?? "No description provided.", workspacePath = workflow.WorktreePath ?? workflow.RepositoryPath ?? "Not specified", });
            // Get codebase context (combines code graph + RAG)
            var promptQueries = _promptRegistry.GetRagQueries("workflow-enrich");
            var ragQueries = promptQueries.Count > 0 ? promptQueries.ToList() : BuildRagQueriesForAnalysis(workflow);
            // Always use RepositoryPath for RAG queries (that's where the index lives)
            // Paths in context will be relative, and the agent's WorkingDirectory is set to the worktree
            var contextOptions = CodebaseContextOptions.ForDocumentation(ragQueries);
            var ragSourcePath = workflow.RepositoryPath ?? workflow.WorktreePath;
            var codebaseContext = ragSourcePath is not null ? await _codebaseContextService.GetContextAsync(ragSourcePath, contextOptions, ct) : null;
            var ragContext = codebaseContext?.ToPromptContext();
            _logger.LogInformation("Analyze codebase context: {HasContext}, queries: {QueryCount}, hasProjectStructure: {HasProjectStructure}", ragContext is not null ? $"{ragContext.Length} chars" : "none", ragQueries.Count, codebaseContext?.ProjectStructure is not null);
            // Check if tools are defined for this prompt
            var promptToolNames = _promptRegistry.GetTools("workflow-enrich");
            _logger.LogWarning("[ANALYZE-DEBUG] Prompt tool names from registry: [{Tools}]", string.Join(", ", promptToolNames));
            var availableTools = promptToolNames.Select(name => _toolRegistry.GetTool(name)).Where(t => t is not null).Cast<ToolDefinition>().ToList();
            _logger.LogWarning("[ANALYZE-DEBUG] Available tools resolved: {Count} - [{Tools}]", availableTools.Count, string.Join(", ", availableTools.Select(t => t.ToolId)));
            string analysisContent;
            int tokensUsed;
            IReadOnlyList<ReActStep>? toolSteps = null;
            if (availableTools.Count > 0)
            {
                // Use ReAct executor for tool-enabled exploration
                _logger.LogInformation("Analyze has {ToolCount} tools available: {Tools}", availableTools.Count, string.Join(", ", availableTools.Select(t => t.ToolId)));
                var llmProvider = _llmProviderRegistry.GetDefaultProvider() ?? throw new InvalidOperationException("No LLM provider available");
                var taskWithContext = ragContext is not null ? $"{prompt}\n\n## Relevant Context from Knowledge Base\n{ragContext}" : prompt;
                var reactOptions = new ReActOptions
                {
                    WorkingDirectory = workflow.WorktreePath,
                    MaxSteps = 10, // Limit exploration steps
                    Temperature = 0.2,
                    RequireConfirmation = false,
                };
                var reactResult = await _reactExecutor.ExecuteAsync(taskWithContext, availableTools, llmProvider, reactOptions, ct);
                analysisContent = reactResult.FinalAnswer ?? "Analysis could not be completed";
                tokensUsed = reactResult.TotalTokensUsed;
                toolSteps = reactResult.Steps;
                _logger.LogInformation("Agentic analysis completed: {Success}, steps={StepCount}, tokens={Tokens}", reactResult.Success, reactResult.Steps.Count, reactResult.TotalTokensUsed);
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

            workflow.AnalyzedContext = JsonSerializer.Serialize(new { agentId = analyzer.AgentId, analysis = analysisContent, tokensUsed, toolSteps = toolSteps?.Select(s => new { s.StepNumber, s.Action, s.Observation }), timestamp = DateTimeOffset.UtcNow, });
            workflow.Status = StoryStatus.Analyzed;
            workflow.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Analyzed workflow {WorkflowId}", workflowId);
            return workflow;
        }
        catch (Exception ex)
        {
            workflow.Status = StoryStatus.Failed;
            workflow.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            _logger.LogError(ex, "Failed to analyze workflow {WorkflowId}", workflowId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<Story> PlanAsync(Guid workflowId, CancellationToken ct = default)
    {
        var workflow = await _db.Stories.FirstOrDefaultAsync(w => w.Id == workflowId, ct) ?? throw new InvalidOperationException($"Workflow {workflowId} not found");
        if (workflow.Status != StoryStatus.Analyzed)
        {
            throw new InvalidOperationException($"Workflow must be analyzed before planning. Current status: {workflow.Status}");
        }

        workflow.Status = StoryStatus.Planning;
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
            var prompt = _promptRegistry.Render("workflow-plan", new { title = workflow.Title, description = workflow.Description ?? "No description provided.", enrichedContext = workflow.AnalyzedContext ?? "No analysis available.", });
            // Try to use structured output if the provider supports it
            List<StepDefinition> steps;
            string rawResponse;
            int tokensUsed;
            // Use agent's provider, or fall back to configured default
            var provider = planner.Metadata.Provider is not null ? _llmProviderRegistry.GetProvider(planner.Metadata.Provider) : _llmProviderRegistry.GetDefaultProvider();
            if (provider is null)
            {
                throw new InvalidOperationException($"No LLM provider available. Agent provider: {planner.Metadata.Provider ?? "(not set)"}");
            }

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
            var existingSteps = await _db.StorySteps.Where(s => s.StoryId == workflowId).ToListAsync(ct);
            _db.StorySteps.RemoveRange(existingSteps);
            var order = 1;
            foreach (var step in steps)
            {
                var workflowStep = new StoryStep
                {
                    Id = Guid.NewGuid(),
                    StoryId = workflowId,
                    Order = order++,
                    Name = step.Name,
                    Capability = step.Capability,
                    Language = step.Language,
                    Description = step.Description,
                    Status = StepStatus.Pending,
                };
                _db.StorySteps.Add(workflowStep);
            }

            workflow.ExecutionPlan = JsonSerializer.Serialize(new { agentId = planner.AgentId, rawResponse, stepCount = steps.Count, tokensUsed, timestamp = DateTimeOffset.UtcNow, });
            workflow.Status = StoryStatus.Planned;
            workflow.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            // Reload with steps
            var reloaded = await GetByIdWithStepsAsync(workflowId, ct);
            _logger.LogInformation("Planned workflow {WorkflowId} with {StepCount} steps", workflowId, steps.Count);
            return reloaded!;
        }
        catch (Exception ex)
        {
            workflow.Status = StoryStatus.Failed;
            workflow.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            _logger.LogError(ex, "Failed to plan workflow {WorkflowId}", workflowId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<StoryDecomposeResult> DecomposeAsync(Guid storyId, int maxParallelism = 4, bool includeTests = true, CancellationToken ct = default)
    {
        var story = await _db.Stories.Include(w => w.Steps).FirstOrDefaultAsync(w => w.Id == storyId, ct) ?? throw new InvalidOperationException($"Story {storyId} not found");
        // Story must be analyzed or planned before decomposition
        if (story.Status != StoryStatus.Analyzed && story.Status != StoryStatus.Planned)
        {
            throw new InvalidOperationException($"Story must be analyzed or planned before decomposition. Current status: {story.Status}");
        }

        // Get an analysis agent for decomposition
        var agent = _agentRegistry.GetBestForCapability(Capabilities.Analysis);
        if (agent is null)
        {
            throw new InvalidOperationException($"No agent with '{Capabilities.Analysis}' capability found");
        }

        // Build the decomposition prompt using the template
        var prompt = _promptRegistry.Render("story-decompose", new { title = story.Title, description = story.Description ?? "No description provided.", analyzedContext = story.AnalyzedContext, maxParallelism, includeTests, });
        // Execute with LLM
        var provider = agent.Metadata.Provider is not null ? _llmProviderRegistry.GetProvider(agent.Metadata.Provider) : _llmProviderRegistry.GetDefaultProvider();
        if (provider is null)
        {
            throw new InvalidOperationException($"No LLM provider available. Agent provider: {agent.Metadata.Provider ?? "(not set)"}");
        }

        var messages = new List<Aura.Foundation.Agents.ChatMessage>
        {
            new(Aura.Foundation.Agents.ChatRole.System, "You are a task decomposition expert. Return only valid JSON."),
            new(Aura.Foundation.Agents.ChatRole.User, prompt),
        };
        var chatOptions = new ChatOptions
        {
            Temperature = 0.2, // Low temperature for consistent structure
        };
        var response = await provider.ChatAsync(agent.Metadata.Model, messages, chatOptions, ct);
        var rawResponse = response.Content;
        // Parse the JSON response into task DTOs
        var taskDtos = ParseTaskDecompositionFromResponse(rawResponse);
        if (taskDtos.Count == 0)
        {
            throw new InvalidOperationException("Decomposition produced no tasks. Raw response: " + rawResponse);
        }

        // Clear existing steps and create new ones from decomposition
        var existingSteps = await _db.StorySteps.Where(s => s.StoryId == storyId).ToListAsync(ct);
        _db.StorySteps.RemoveRange(existingSteps);
        var order = 1;
        var steps = new List<StoryStep>();
        foreach (var dto in taskDtos)
        {
            var step = new StoryStep
            {
                Id = Guid.NewGuid(),
                StoryId = storyId,
                Order = order++,
                Wave = dto.Wave,
                Name = dto.Title ?? "Untitled task",
                Description = dto.Description,
                Capability = Capabilities.Coding, // Default for decomposed tasks
                Status = StepStatus.Pending,
            };
            _db.StorySteps.Add(step);
            steps.Add(step);
        }

        // Calculate wave count
        var waveCount = taskDtos.Max(t => t.Wave);
        // Update story - ready to run
        story.Status = StoryStatus.Planned;
        story.MaxParallelism = maxParallelism;
        story.CurrentWave = 0;
        story.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Decomposed story {StoryId} into {StepCount} steps across {WaveCount} waves", storyId, steps.Count, waveCount);
        return new StoryDecomposeResult
        {
            StoryId = storyId,
            Steps = steps,
            WaveCount = waveCount,
            Story = story,
        };
    }
}
