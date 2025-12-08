using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aura.Foundation.Llm;
using Microsoft.Extensions.Logging;

namespace Aura.Foundation.Tools;

/// <summary>
/// Executes agents with tools using the ReAct (Reasoning + Acting) pattern.
/// Model-agnostic: works with any LLM through multi-turn conversation.
/// </summary>
public interface IReActExecutor
{
    /// <summary>
    /// Execute a task using the ReAct loop with available tools.
    /// </summary>
    Task<ReActResult> ExecuteAsync(
        string task,
        IReadOnlyList<ToolDefinition> availableTools,
        ILlmProvider llm,
        ReActOptions? options = null,
        CancellationToken ct = default);
}

/// <summary>
/// Options for ReAct execution.
/// </summary>
public record ReActOptions
{
    /// <summary>Maximum number of reasoning steps before forcing completion</summary>
    public int MaxSteps { get; init; } = 10;

    /// <summary>LLM model to use</summary>
    public string? Model { get; init; }

    /// <summary>Temperature for LLM calls</summary>
    public double Temperature { get; init; } = 0.2;

    /// <summary>Working directory for tool execution</summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>Additional context to include in the system prompt</summary>
    public string? AdditionalContext { get; init; }

    /// <summary>Whether to require user confirmation for dangerous tools</summary>
    public bool RequireConfirmation { get; init; } = true;

    /// <summary>Callback for user confirmation (returns true to proceed)</summary>
    public Func<ToolDefinition, ToolInput, Task<bool>>? ConfirmationCallback { get; init; }
}

/// <summary>
/// Result of ReAct execution.
/// </summary>
public record ReActResult
{
    /// <summary>Whether the task completed successfully</summary>
    public required bool Success { get; init; }

    /// <summary>Final answer/output from the agent</summary>
    public required string FinalAnswer { get; init; }

    /// <summary>All reasoning steps taken</summary>
    public required IReadOnlyList<ReActStep> Steps { get; init; }

    /// <summary>Error message if failed</summary>
    public string? Error { get; init; }

    /// <summary>Total execution time</summary>
    public TimeSpan TotalDuration { get; init; }

    /// <summary>Total tokens used across all LLM calls</summary>
    public int TotalTokensUsed { get; init; }
}

/// <summary>
/// A single step in the ReAct reasoning loop.
/// </summary>
public record ReActStep
{
    /// <summary>Step number (1-indexed)</summary>
    public required int StepNumber { get; init; }

    /// <summary>Agent's reasoning/thought</summary>
    public required string Thought { get; init; }

    /// <summary>Action taken (tool name or "finish")</summary>
    public required string Action { get; init; }

    /// <summary>Input provided to the action</summary>
    public required string ActionInput { get; init; }

    /// <summary>Result/observation from the action</summary>
    public required string Observation { get; init; }

    /// <summary>Duration of this step</summary>
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Default implementation of ReAct executor.
/// </summary>
public partial class ReActExecutor : IReActExecutor
{
    private readonly IToolRegistry _toolRegistry;
    private readonly ILogger<ReActExecutor> _logger;

    public ReActExecutor(IToolRegistry toolRegistry, ILogger<ReActExecutor> logger)
    {
        _toolRegistry = toolRegistry;
        _logger = logger;
    }

    public async Task<ReActResult> ExecuteAsync(
        string task,
        IReadOnlyList<ToolDefinition> availableTools,
        ILlmProvider llm,
        ReActOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new ReActOptions();
        var steps = new List<ReActStep>();
        var totalTokens = 0;
        var startTime = DateTime.UtcNow;

        var systemPrompt = BuildSystemPrompt(availableTools, options.AdditionalContext);
        var conversationHistory = new StringBuilder();
        conversationHistory.AppendLine($"Task: {task}");
        conversationHistory.AppendLine();

        for (int step = 1; step <= options.MaxSteps; step++)
        {
            ct.ThrowIfCancellationRequested();

            var stepStart = DateTime.UtcNow;
            _logger.LogDebug("ReAct step {Step}/{MaxSteps}", step, options.MaxSteps);

            // Build prompt with conversation history
            var prompt = $"{systemPrompt}\n\n{conversationHistory}\nStep {step}:";

            // Call LLM for next action
            LlmResponse llmResponse;
            try
            {
                var model = options.Model ?? "qwen2.5-coder:7b";
                llmResponse = await llm.GenerateAsync(model, prompt, options.Temperature, ct);
            }
            catch (LlmException ex)
            {
                _logger.LogError(ex, "LLM call failed at step {Step}", step);
                return new ReActResult
                {
                    Success = false,
                    FinalAnswer = "",
                    Steps = steps,
                    Error = ex.Message,
                    TotalDuration = DateTime.UtcNow - startTime,
                    TotalTokensUsed = totalTokens
                };
            }

            totalTokens += llmResponse.TokensUsed;

            // Parse the response
            var parsed = ParseResponse(llmResponse.Content);

            _logger.LogDebug("Thought: {Thought}", parsed.Thought);
            _logger.LogDebug("Action: {Action}", parsed.Action);
            _logger.LogDebug("Action Input: {ActionInput}", parsed.ActionInput);

            string observation;

            // Check if agent is done
            if (parsed.Action.Equals("finish", StringComparison.OrdinalIgnoreCase) ||
                parsed.Action.Equals("final_answer", StringComparison.OrdinalIgnoreCase))
            {
                steps.Add(new ReActStep
                {
                    StepNumber = step,
                    Thought = parsed.Thought,
                    Action = "finish",
                    ActionInput = parsed.ActionInput,
                    Observation = "Task completed",
                    Duration = DateTime.UtcNow - stepStart
                });

                return new ReActResult
                {
                    Success = true,
                    FinalAnswer = parsed.ActionInput,
                    Steps = steps,
                    TotalDuration = DateTime.UtcNow - startTime,
                    TotalTokensUsed = totalTokens
                };
            }

            // Find and execute the tool
            var normalizedAction = NormalizeToolName(parsed.Action);
            var tool = availableTools.FirstOrDefault(t =>
                t.ToolId.Equals(normalizedAction, StringComparison.OrdinalIgnoreCase) ||
                t.Name.Equals(normalizedAction, StringComparison.OrdinalIgnoreCase));

            if (tool is null)
            {
                var validToolIds = string.Join(", ", availableTools.Select(t => t.ToolId));
                observation = $"Error: Tool '{parsed.Action}' not found. Valid tools are: {validToolIds}. Use 'file.modify' (not 'Modify'), 'file.write' (not 'Write'). When done, use Action: finish";
                _logger.LogWarning("Tool not found: {ToolId}", parsed.Action);
            }
            else
            {
                // Parse action input as JSON parameters
                var parameters = ParseActionInput(parsed.ActionInput);

                var toolInput = new ToolInput
                {
                    ToolId = tool.ToolId,
                    WorkingDirectory = options.WorkingDirectory,
                    Parameters = parameters
                };

                // Check confirmation if required
                if (tool.RequiresConfirmation && options.RequireConfirmation)
                {
                    if (options.ConfirmationCallback is not null)
                    {
                        var confirmed = await options.ConfirmationCallback(tool, toolInput);
                        if (!confirmed)
                        {
                            observation = "Tool execution cancelled by user";
                            _logger.LogInformation("Tool {ToolId} cancelled by user", tool.ToolId);
                        }
                        else
                        {
                            observation = await ExecuteToolAsync(tool, toolInput, ct);
                        }
                    }
                    else
                    {
                        // No confirmation callback, skip dangerous tools
                        observation = $"Tool '{tool.ToolId}' requires confirmation but no confirmation callback provided";
                        _logger.LogWarning("Tool {ToolId} requires confirmation", tool.ToolId);
                    }
                }
                else
                {
                    observation = await ExecuteToolAsync(tool, toolInput, ct);
                }
            }

            var reactStep = new ReActStep
            {
                StepNumber = step,
                Thought = parsed.Thought,
                Action = parsed.Action,
                ActionInput = parsed.ActionInput,
                Observation = observation,
                Duration = DateTime.UtcNow - stepStart
            };

            steps.Add(reactStep);

            // Add to conversation history
            conversationHistory.AppendLine($"Thought: {parsed.Thought}");
            conversationHistory.AppendLine($"Action: {parsed.Action}");
            conversationHistory.AppendLine($"Action Input: {parsed.ActionInput}");
            conversationHistory.AppendLine($"Observation: {observation}");
            conversationHistory.AppendLine();
        }

        // Max steps reached
        _logger.LogWarning("ReAct reached max steps ({MaxSteps})", options.MaxSteps);
        return new ReActResult
        {
            Success = false,
            FinalAnswer = "",
            Steps = steps,
            Error = $"Reached maximum steps ({options.MaxSteps}) without completing task",
            TotalDuration = DateTime.UtcNow - startTime,
            TotalTokensUsed = totalTokens
        };
    }

    private async Task<string> ExecuteToolAsync(ToolDefinition tool, ToolInput input, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Executing tool: {ToolId}", tool.ToolId);
            var result = await _toolRegistry.ExecuteAsync(input, ct);

            if (result.Success)
            {
                var output = result.Output is not null
                    ? JsonSerializer.Serialize(result.Output, new JsonSerializerOptions { WriteIndented = true })
                    : "Success (no output)";

                // Truncate very long outputs
                if (output.Length > 10000)
                {
                    output = output[..10000] + "\n... (truncated)";
                }

                return output;
            }
            else
            {
                return $"Error: {result.Error}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool {ToolId} threw exception", tool.ToolId);
            return $"Error: {ex.Message}";
        }
    }

    private static string BuildSystemPrompt(IReadOnlyList<ToolDefinition> tools, string? additionalContext)
    {
        var toolDescriptions = new StringBuilder();
        foreach (var tool in tools)
        {
            toolDescriptions.AppendLine($"- {tool.ToolId}: {tool.Description}");
            if (tool.InputSchema is not null)
            {
                toolDescriptions.AppendLine($"  Input: {tool.InputSchema}");
            }
        }

        var toolIds = string.Join(", ", tools.Select(t => t.ToolId));

        var jsonExamples = $$"""
              EXAMPLES of correct format:

              Example 1 - Using file.read:
              Thought: I need to read the README file to understand the project.
              Action: file.read
              Action Input: {"path": "README.md"}

              Example 2 - Using file.write to CREATE a new file:
              Thought: The file does not exist, so I will create it with the required content.
              Action: file.write
              Action Input: {"path": "CONTRIBUTING.md", "content": "# Contributing\n\nWelcome to the project..."}

              Example 3 - Completing the task (IMPORTANT):
              Thought: I have finished reading and updating the file. The task is complete.
              Action: finish
              Action Input: {"result": "Successfully updated the README with new content."}

              IMPORTANT - HANDLING FILE NOT FOUND:
              If file.read returns "Error: File not found", this means you need to CREATE the file.
              Do NOT give up or finish with an error. Instead, use file.write to create the file.
              Example:
              - Observation: Error: File not found: CONTRIBUTING.md
              - Thought: The file does not exist, so I will create it with the appropriate content.
              - Action: file.write
              - Action Input: {"path": "CONTRIBUTING.md", "content": "..."}

              CRITICAL RULES:
              1. Action MUST be EXACTLY one of: {{toolIds}}, finish
              2. Do NOT invent tool names like "Review", "Manually", "Validate", etc.
              3. Action Input MUST be a valid JSON object - never prose or markdown
              4. When you have completed all necessary tool operations, use "finish"
              5. If you cannot proceed with available tools, use "finish" to explain why
              6. If your task is to CREATE or DRAFT a file, you MUST call file.write before finishing
              7. Do NOT finish with "file will need to be created" - actually CREATE it with file.write

              WRONG (will cause errors):
              - Action: Review  ← ERROR: Not a valid tool. Use "finish" to complete the task
              - Action: Modify  ← ERROR: Not a valid tool. Use "file.modify" instead
              - Action: Manually  ← ERROR: Not a valid tool
              - Action: Validate  ← ERROR: Not a valid tool. Use "finish" to complete the task
              - Action Input: README.md  ← ERROR: Must be JSON object
              
              REMEMBER: The ONLY valid actions are: {{toolIds}}, finish
              If you want to review/validate, just use "finish" with your analysis in the result.
              """;var prompt = $"""
            You are an AI assistant that can use tools to accomplish tasks.
            You should think step by step and use tools when needed.

            Available tools:
            {toolDescriptions}

            CRITICAL: You MUST respond in this EXACT format:
            Thought: [your reasoning about what to do next]
            Action: [tool_id to use, or "finish" when done]
            Action Input: [MUST be valid JSON object for tools]

            {jsonExamples}

            {(additionalContext is not null ? $"\nAdditional context:\n{additionalContext}" : "")}
            """;

        return prompt;
    }

    private static (string Thought, string Action, string ActionInput) ParseResponse(string response)
    {
        var thought = "";
        var action = "";
        var actionInput = "";

        // Parse Thought
        var thoughtMatch = ThoughtRegex().Match(response);
        if (thoughtMatch.Success)
        {
            thought = thoughtMatch.Groups[1].Value.Trim();
        }

        // Parse Action
        var actionMatch = ActionRegex().Match(response);
        if (actionMatch.Success)
        {
            action = actionMatch.Groups[1].Value.Trim();
        }

        // Parse Action Input - can be multiline JSON
        var actionInputMatch = ActionInputRegex().Match(response);
        if (actionInputMatch.Success)
        {
            actionInput = actionInputMatch.Groups[1].Value.Trim();
        }

        // If we couldn't parse structured output, treat the whole thing as a thought
        // and assume the agent wants to finish
        if (string.IsNullOrEmpty(action))
        {
            thought = response;
            action = "finish";
            actionInput = response;
        }

        return (thought, action, actionInput);
    }

    private static IReadOnlyDictionary<string, object?> ParseActionInput(string actionInput)
    {
        if (string.IsNullOrWhiteSpace(actionInput))
        {
            return new Dictionary<string, object?>();
        }

        try
        {
            // Try to parse as JSON
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(actionInput);
            if (dict is not null)
            {
                // Normalize common parameter name variations
                var result = new Dictionary<string, object?>();
                foreach (var kvp in dict)
                {
                    var key = NormalizeParameterName(kvp.Key);
                    result[key] = ConvertJsonElement(kvp.Value);
                }
                return result;
            }
        }
        catch
        {
            // Not JSON, might be a simple string value
        }

        // For non-JSON input, assume it's a file path (most common case for file tools)
        // Provide both "filePath" and "path" for compatibility
        return new Dictionary<string, object?>
        {
            ["filePath"] = actionInput,
            ["path"] = actionInput,
            ["input"] = actionInput
        };
    }

    private static string NormalizeParameterName(string name)
    {
        // Map common parameter name variations to expected names
        // NOTE: Keep "path" as-is since BuiltInTools expects it
        return name.ToLowerInvariant() switch
        {
            "file" or "file_path" => "path",  // Normalize to "path" for BuiltInTools
            "filepath" => "filePath",  // Keep filePath for typed tools
            "content" or "text" or "contents" => "content",
            "old_text" or "oldtext" or "old" or "search" or "find" => "oldText",
            "new_text" or "newtext" or "new" or "replace" or "replacement" => "newText",
            _ => name // Keep original if no mapping
        };
    }

    private static string NormalizeToolName(string action)
    {
        // Map common LLM mistakes to correct tool names
        return action.ToLowerInvariant() switch
        {
            "modify" => "file.modify",
            "write" => "file.write",
            "read" => "file.read",
            "list" => "file.list",
            "exists" => "file.exists",
            "delete" => "file.delete",
            "review" or "validate" or "check" => "finish",  // These should just finish
            _ => action // Keep original if no mapping
        };
    }

    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToArray(),
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value)),
            _ => element.ToString()
        };
    }

    [GeneratedRegex(@"Thought:\s*(.+?)(?=Action:|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ThoughtRegex();

    [GeneratedRegex(@"Action:\s*(\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex ActionRegex();

    [GeneratedRegex(@"Action Input:\s*(.+?)(?=Thought:|Observation:|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ActionInputRegex();
}
