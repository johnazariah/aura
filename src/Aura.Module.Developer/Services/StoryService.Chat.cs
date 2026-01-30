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
    public async Task<StoryChatResponse> ChatAsync(Guid workflowId, string message, CancellationToken ct = default)
    {
        var workflow = await _db.Stories.Include(w => w.Steps.OrderBy(s => s.Order)).FirstOrDefaultAsync(w => w.Id == workflowId, ct) ?? throw new InvalidOperationException($"Workflow {workflowId} not found");
        // Get a chat-capable agent
        var chatAgent = _agentRegistry.GetBestForCapability(Capabilities.Chat) ?? throw new InvalidOperationException("No chat agent available");
        // Different prompts based on workflow status
        var prompt = workflow.Status == StoryStatus.Analyzed ? _promptRegistry.Render("workflow-chat-analyzed", new { workflow = new { title = workflow.Title, description = workflow.Description, status = workflow.Status.ToString() }, analysisSummary = ExtractAnalysisSummary(workflow.AnalyzedContext), message, }) : _promptRegistry.Render("workflow-chat-planning", new { workflow = new { title = workflow.Title, status = workflow.Status.ToString() }, steps = workflow.Steps.Select(s => new { s.Order, s.Name, s.Capability, status = s.Status.ToString() }), message, });
        var context = new AgentContext(prompt, WorkspacePath: workflow.WorktreePath);
        var output = await chatAgent.ExecuteAsync(context, ct);
        var response = new StoryChatResponse
        {
            Response = output.Content
        };
        var stepsAdded = new List<StoryStep>();
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
            if (parsed is not null && !parsed.Value.Name.Contains("[") && !parsed.Value.Capability.Contains("[") && IsValidCapability(parsed.Value.Capability))
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
                        if (!newStep.Name.Contains("[") && !newStep.Capability.Contains("[") && IsValidCapability(newStep.Capability))
                        {
                            var addedStep = await AddStepAsync(workflowId, newStep.Name, newStep.Capability, newStep.Description, afterOrder: insertAfterOrder, ct: ct);
                            stepsAdded.Add(addedStep);
                            insertAfterOrder = addedStep.Order; // Next step goes after this one
                        }
                    }

                    _logger.LogInformation("Split step {StepNumber} into {NewStepCount} steps in workflow {WorkflowId}", splitResult.Value.StepNumber, stepsAdded.Count, workflowId);
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
    private async Task AppendWorkflowChatHistoryAsync(Story workflow, string userMessage, string assistantResponse, CancellationToken ct)
    {
        var history = new List<ChatMessage>();
        if (!string.IsNullOrEmpty(workflow.ChatHistory))
        {
            try
            {
                history = JsonSerializer.Deserialize<List<ChatMessage>>(workflow.ChatHistory) ?? [];
            }
            catch
            { /* ignore parse errors */
            }
        }

        history.Add(new ChatMessage("user", userMessage));
        history.Add(new ChatMessage("assistant", assistantResponse));
        workflow.ChatHistory = JsonSerializer.Serialize(history);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<(StoryStep Step, string Response)> ChatWithStepAsync(Guid workflowId, Guid stepId, string message, CancellationToken ct = default)
    {
        var workflow = await _db.Stories.Include(w => w.Steps).FirstOrDefaultAsync(w => w.Id == workflowId, ct) ?? throw new InvalidOperationException($"Workflow {workflowId} not found");
        var step = workflow.Steps.FirstOrDefault(s => s.Id == stepId) ?? throw new InvalidOperationException($"Step {stepId} not found in workflow");
        // Get the assigned agent or find one by capability
        var agent = step.AssignedAgentId is not null ? _agentRegistry.GetAgent(step.AssignedAgentId) : _agentRegistry.GetBestForCapability(step.Capability, step.Language);
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
            catch
            { /* ignore parse errors */
            }
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

    private async Task<string?> GetRagContextAsync(string query, string? workspacePath, CancellationToken ct)
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

            _logger.LogInformation("Found {Count} RAG results for query, scores: {Scores}", results.Count, string.Join(", ", results.Select(r => r.Score.ToString("F2"))));
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

    /// <summary>
    /// Queries RAG with multiple queries and combines results.
    /// </summary>
    private async Task<string?> GetRagContextForStepAsync(List<string> queries, string? workspacePath, CancellationToken ct)
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
            var uniqueResults = allResults.GroupBy(r => r.Result.ContentId + "_" + r.Result.ChunkIndex).Select(g => g.OrderByDescending(r => r.Result.Score).First()).OrderByDescending(r => r.Result.Score).Take(20) // Limit to top 20 unique results for richer context
            .ToList();
            _logger.LogInformation("Found {TotalCount} RAG results from {QueryCount} queries, {UniqueCount} unique, top scores: {Scores}", allResults.Count, queries.Count, uniqueResults.Count, string.Join(", ", uniqueResults.Take(5).Select(r => r.Result.Score.ToString("F2"))));
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
}
