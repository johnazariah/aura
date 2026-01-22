using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aura.Foundation.Agents;
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

    /// <summary>
    /// Whether to use structured JSON output mode when the provider supports it.
    /// When enabled, uses JSON schema enforcement for more reliable parsing.
    /// Default is false for backward compatibility.
    /// </summary>
    public bool UseStructuredOutput { get; init; } = false;

    /// <summary>
    /// Approximate token budget for this execution.
    /// When usage exceeds BudgetWarningThreshold%, agent may consider spawning sub-agents.
    /// Default: 100,000 (typical context window).
    /// </summary>
    public int TokenBudget { get; init; } = 100_000;
    /// <summary>
    /// Threshold percentage at which to warn agent about budget.
    /// Default: 70%.
    /// </summary>
    public double BudgetWarningThreshold { get; init; } = 70.0;

    /// <summary>
    /// Whether to automatically retry on failure.
    /// When enabled, failed executions will be retried with failure context injected.
    /// Default: false.
    /// </summary>
    public bool RetryOnFailure { get; init; } = false;

    /// <summary>
    /// Maximum number of retry attempts when RetryOnFailure is enabled.
    /// Default: 3.
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Condition that determines when to retry.
    /// Default: AllFailures (retry on any failure).
    /// </summary>
    public RetryCondition RetryCondition { get; init; } = RetryCondition.AllFailures;

    /// <summary>
    /// Optional custom prompt template for retry attempts.
    /// If null, uses default retry prompt with error context.
    /// </summary>
    public string? RetryPromptTemplate { get; init; }
}

/// <summary>
/// Conditions under which ReAct execution will retry on failure.
/// </summary>
public enum RetryCondition
{
    /// <summary>Retry on any failure.</summary>
    AllFailures,

    /// <summary>Retry only on build/compilation errors.</summary>
    BuildErrors,

    /// <summary>Retry only on test failures.</summary>
    TestFailures,

    /// <summary>Retry on build errors or test failures.</summary>
    BuildOrTestFailures,

    /// <summary>Never retry automatically.</summary>
    Never,
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
    /// <summary>Cumulative tokens used up to and including this step</summary>
    public int CumulativeTokens { get; init; }
}

/// <summary>
/// Default implementation of ReAct executor.
/// </summary>
public partial class ReActExecutor(IToolRegistry toolRegistry, ILogger<ReActExecutor> logger) : IReActExecutor
{
    private readonly IToolRegistry _toolRegistry = toolRegistry;
    private readonly ILogger<ReActExecutor> _logger = logger;

    public async Task<ReActResult> ExecuteAsync(
        string task,
        IReadOnlyList<ToolDefinition> availableTools,
        ILlmProvider llm,
        ReActOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new ReActOptions();

        // If retry is disabled, execute once
        if (!options.RetryOnFailure)
        {
            return await ExecuteSingleAttemptAsync(task, availableTools, llm, options, ct);
        }

        // Retry loop
        var allSteps = new List<ReActStep>();
        ReActResult? lastResult = null;
        var overallStartTime = DateTime.UtcNow;

        for (int attempt = 0; attempt <= options.MaxRetries; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            var currentTask = task;

            // For retry attempts, inject failure context
            if (attempt > 0 && lastResult != null)
            {
                if (!ShouldRetry(lastResult, options.RetryCondition))
                {
                    _logger.LogInformation("[REACT] Retry condition not met, not retrying");
                    break;
                }

                _logger.LogWarning("[REACT] Retry attempt {Attempt}/{MaxRetries}", attempt, options.MaxRetries);
                currentTask = BuildRetryTask(task, lastResult, options.RetryPromptTemplate);
            }

            lastResult = await ExecuteSingleAttemptAsync(currentTask, availableTools, llm, options, ct);
            allSteps.AddRange(lastResult.Steps);

            if (lastResult.Success)
            {
                return lastResult with
                {
                    Steps = allSteps,
                    TotalDuration = DateTime.UtcNow - overallStartTime
                };
            }
        }

        // All retries exhausted
        return lastResult! with
        {
            Steps = allSteps,
            TotalDuration = DateTime.UtcNow - overallStartTime,
            Error = $"Failed after {options.MaxRetries + 1} attempts. Last error: {lastResult!.Error}"
        };
    }

    private async Task<ReActResult> ExecuteSingleAttemptAsync(
        string task,
        IReadOnlyList<ToolDefinition> availableTools,
        ILlmProvider llm,
        ReActOptions options,
        CancellationToken ct)
    {
        options ??= new ReActOptions();
        var steps = new List<ReActStep>();
        var totalTokens = 0;
        var tokenTracker = new TokenTracker(options.TokenBudget);
        var startTime = DateTime.UtcNow;

        var systemPrompt = BuildSystemPrompt(availableTools, options.AdditionalContext, options.UseStructuredOutput);

        // For structured output mode, we use chat messages
        var chatMessages = new List<ChatMessage>();
        if (options.UseStructuredOutput)
        {
            chatMessages.Add(new ChatMessage(ChatRole.System, systemPrompt));
            chatMessages.Add(new ChatMessage(ChatRole.User, $"Task: {task}"));
        }

        // For legacy mode, we use conversation history string
        var conversationHistory = new StringBuilder();
        conversationHistory.AppendLine($"Task: {task}");
        conversationHistory.AppendLine();

        _logger.LogWarning("[REACT-DEBUG] Starting ReAct loop with MaxSteps={MaxSteps}, UseStructuredOutput={UseStructured}",
            options.MaxSteps, options.UseStructuredOutput);
        var loopStartTime = DateTime.UtcNow;

        for (int step = 1; step <= options.MaxSteps; step++)
        {
            ct.ThrowIfCancellationRequested();

            var stepStart = DateTime.UtcNow;
            var elapsedSinceStart = DateTime.UtcNow - loopStartTime;
            _logger.LogWarning("[REACT-DEBUG] Step {Step}/{MaxSteps} starting at {Elapsed:F1}s elapsed", step, options.MaxSteps, elapsedSinceStart.TotalSeconds);

            // Call LLM for next action
            LlmResponse llmResponse;
            try
            {
                if (options.UseStructuredOutput)
                {
                    // Use ChatAsync with structured output schema
                    var chatOptions = new ChatOptions
                    {
                        ResponseSchema = WellKnownSchemas.ReActResponse,
                        Temperature = options.Temperature
                    };

                    llmResponse = await llm.ChatAsync(options.Model, chatMessages, chatOptions, ct);
                }
                else
                {
                    // Legacy: use GenerateAsync with text prompt
                    var prompt = $"{systemPrompt}\n\n{conversationHistory}\nStep {step}:";
                    llmResponse = await llm.GenerateAsync(options.Model, prompt, options.Temperature, ct);
                }
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
            tokenTracker.Add(llmResponse.TokensUsed);

            // Check token budget threshold
            if (tokenTracker.IsAboveThreshold(options.BudgetWarningThreshold))
            {
                _logger.LogWarning(
                    "[REACT] Token budget {Percent:F1}% used ({Used}/{Budget}). Recommendation: {Recommendation}",
                    tokenTracker.UsagePercent, tokenTracker.Used, tokenTracker.Budget, tokenTracker.GetRecommendation());
            }
            var llmDuration = DateTime.UtcNow - stepStart;
            _logger.LogWarning("[REACT-DEBUG] Step {Step}: LLM call completed in {Duration:F1}s, tokens={Tokens}", step, llmDuration.TotalSeconds, llmResponse.TokensUsed);

            // Parse the response (structured or text-based)
            var parsed = options.UseStructuredOutput
                ? ParseStructuredResponse(llmResponse.Content)
                : ParseResponse(llmResponse.Content);

            _logger.LogWarning("[REACT-DEBUG] Step {Step}: Action={Action}", step, parsed.Action);
            _logger.LogDebug("Thought: {Thought}", parsed.Thought);
            _logger.LogDebug("Action Input: {ActionInput}", parsed.ActionInput);

            string observation;

            // Check if agent is done
            if (parsed.Action.Equals("finish", StringComparison.OrdinalIgnoreCase) ||
                parsed.Action.Equals("final_answer", StringComparison.OrdinalIgnoreCase))
            {
                var totalElapsed = DateTime.UtcNow - loopStartTime;
                _logger.LogWarning("[REACT-DEBUG] Agent finished at step {Step} after {Elapsed:F1}s total", step, totalElapsed.TotalSeconds);
                steps.Add(new ReActStep
                {
                    StepNumber = step,
                    Thought = parsed.Thought,
                    Action = "finish",
                    ActionInput = parsed.ActionInput,
                    Observation = "Task completed",
                    Duration = DateTime.UtcNow - stepStart,
                    CumulativeTokens = tokenTracker.Used
                });

                // Extract the final answer - unwrap JSON if the LLM wrapped it
                var finalAnswer = ExtractFinalAnswer(parsed.ActionInput);

                return new ReActResult
                {
                    Success = true,
                    FinalAnswer = finalAnswer,
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
                Duration = DateTime.UtcNow - stepStart,
                CumulativeTokens = tokenTracker.Used
            };

            steps.Add(reactStep);

            // Add to conversation history
            if (options.UseStructuredOutput)
            {
                // For structured output, add assistant response and observation as messages
                chatMessages.Add(new ChatMessage(ChatRole.Assistant, llmResponse.Content));
                chatMessages.Add(new ChatMessage(ChatRole.User, $"Observation: {observation}"));
            }
            else
            {
                // Legacy text-based history
                conversationHistory.AppendLine($"Thought: {parsed.Thought}");
                conversationHistory.AppendLine($"Action: {parsed.Action}");
                conversationHistory.AppendLine($"Action Input: {parsed.ActionInput}");
                conversationHistory.AppendLine($"Observation: {observation}");
                conversationHistory.AppendLine();
            }
        }

        // Max steps reached
        var totalDuration = DateTime.UtcNow - loopStartTime;
        _logger.LogWarning("[REACT-DEBUG] ReAct reached max steps ({MaxSteps}) after {Elapsed:F1}s total", options.MaxSteps, totalDuration.TotalSeconds);
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
        var toolStart = DateTime.UtcNow;
        try
        {
            _logger.LogWarning("[REACT-DEBUG] Executing tool: {ToolId}", tool.ToolId);
            var result = await _toolRegistry.ExecuteAsync(input, ct);
            var toolDuration = DateTime.UtcNow - toolStart;
            _logger.LogWarning("[REACT-DEBUG] Tool {ToolId} completed in {Duration:F1}s, success={Success}", tool.ToolId, toolDuration.TotalSeconds, result.Success);

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

    private static bool ShouldRetry(ReActResult result, RetryCondition condition)
    {
        if (result.Success)
        {
            return false;
        }

        return condition switch
        {
            RetryCondition.Never => false,
            RetryCondition.AllFailures => true,
            RetryCondition.BuildErrors => ContainsBuildError(result),
            RetryCondition.TestFailures => ContainsTestFailure(result),
            RetryCondition.BuildOrTestFailures => ContainsBuildError(result) || ContainsTestFailure(result),
            _ => true
        };
    }

    private static bool ContainsBuildError(ReActResult result)
    {
        // Check for common build error patterns
        var errorPatterns = new[] { "error CS", "error TS", "BUILD FAILED", "compilation failed" };
        var allObservations = string.Join(" ", result.Steps.Select(s => s.Observation));

        return errorPatterns.Any(pattern =>
            allObservations.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
            (result.Error?.Contains(pattern, StringComparison.OrdinalIgnoreCase) ?? false));
    }

    private static bool ContainsTestFailure(ReActResult result)
    {
        // Check for common test failure patterns
        var errorPatterns = new[] { "test failed", "tests failed", "FAILED:", "Assert." };
        var allObservations = string.Join(" ", result.Steps.Select(s => s.Observation));

        return errorPatterns.Any(pattern =>
            allObservations.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
            (result.Error?.Contains(pattern, StringComparison.OrdinalIgnoreCase) ?? false));
    }

    private static string BuildRetryTask(string originalTask, ReActResult failedResult, string? customTemplate)
    {
        var lastStep = failedResult.Steps.LastOrDefault();
        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(customTemplate))
        {
            // Simple template substitution (could use Handlebars in future)
            return customTemplate
                .Replace("{{error}}", failedResult.Error ?? "Unknown error")
                .Replace("{{lastThought}}", lastStep?.Thought ?? "N/A")
                .Replace("{{lastAction}}", lastStep?.Action ?? "N/A")
                .Replace("{{observation}}", lastStep?.Observation ?? "N/A")
                .Replace("{{originalTask}}", originalTask);
        }

        // Default retry prompt
        sb.AppendLine("## Previous Attempt Failed");
        sb.AppendLine();
        sb.AppendLine($"**Error:** {failedResult.Error ?? "Task did not complete successfully"}");

        if (lastStep != null)
        {
            sb.AppendLine($"**Last thought:** {lastStep.Thought}");
            sb.AppendLine($"**Last action:** {lastStep.Action}");
            sb.AppendLine($"**Observation:** {TruncateForRetry(lastStep.Observation)}");
        }

        sb.AppendLine();
        sb.AppendLine("Please try again with a different approach. Learn from the failure above.");
        sb.AppendLine();
        sb.AppendLine("## Original Task");
        sb.AppendLine();
        sb.AppendLine(originalTask);

        return sb.ToString();
    }

    private static string TruncateForRetry(string text, int maxLength = 500)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
        {
            return text;
        }

        return text.Substring(0, maxLength) + "... (truncated)";
    }

    private static string BuildSystemPrompt(IReadOnlyList<ToolDefinition> tools, string? additionalContext, bool useStructuredOutput = false)
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

        // When using structured output, the schema enforces the format, so we use a simpler prompt
        if (useStructuredOutput)
        {
            return $"""
                You are an AI assistant that can use tools to accomplish tasks.
                Think step by step and use tools when needed.

                Available tools:
                {toolDescriptions}

                Valid action values: {toolIds}, finish
                
                RULES:
                1. "action" must be exactly one of: {toolIds}, finish
                2. "action_input" should be a JSON object with tool parameters (for tools) or a string (for finish)
                3. Use "finish" when the task is complete, with your final answer as action_input
                4. If file.read returns "Error: File not found", use file.write to create the file

                {(additionalContext is not null ? $"\nAdditional context:\n{additionalContext}" : "")}
                """;
        }

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
              Action Input: Your final answer in plain text. Do NOT wrap in JSON. Just write the answer directly.

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
              3. For TOOLS: Action Input MUST be a valid JSON object
              4. For FINISH: Action Input is your plain text answer (NO JSON wrapping)
              5. When you have completed all necessary tool operations, use "finish"
              6. If you cannot proceed with available tools, use "finish" to explain why
              7. If your task is to CREATE or DRAFT a file, you MUST call file.write before finishing
              8. Do NOT finish with "file will need to be created" - actually CREATE it with file.write

              WRONG (will cause errors):
              - Action: Review  ← ERROR: Not a valid tool. Use "finish" to complete the task
              - Action: Modify  ← ERROR: Not a valid tool. Use "file.modify" instead
              - Action: Manually  ← ERROR: Not a valid tool
              - Action: Validate  ← ERROR: Not a valid tool. Use "finish" to complete the task
              - Action Input: README.md  ← ERROR: Must be JSON object for tools
              - Action Input: {"result": "..."}  ← ERROR for finish: Don't wrap in JSON, just write the answer
              
              REMEMBER: The ONLY valid actions are: {{toolIds}}, finish
              If you want to review/validate, just use "finish" with your analysis as plain text.
              """; var prompt = $"""
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

    /// <summary>
    /// Extracts the final answer from the action input.
    /// If the LLM wrapped the answer in JSON like {"result": "..."}, unwrap it.
    /// Otherwise, return the input as-is.
    /// </summary>
    private static string ExtractFinalAnswer(string actionInput)
    {
        if (string.IsNullOrWhiteSpace(actionInput))
            return actionInput;

        var trimmed = actionInput.Trim();

        // If it looks like JSON with a "result" field, try to unwrap it
        if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                if (doc.RootElement.TryGetProperty("result", out var resultElement))
                {
                    return resultElement.GetString() ?? actionInput;
                }
            }
            catch (JsonException)
            {
                // Not valid JSON, return as-is
            }
        }

        return actionInput;
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

    private static (string Thought, string Action, string ActionInput) ParseStructuredResponse(string response)
    {
        // Parse JSON response from structured output mode
        try
        {
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            var thought = root.TryGetProperty("thought", out var thoughtProp)
                ? thoughtProp.GetString() ?? ""
                : "";
            var action = root.TryGetProperty("action", out var actionProp)
                ? actionProp.GetString() ?? ""
                : "";

            // action_input can be an object or a string
            var actionInput = "";
            if (root.TryGetProperty("action_input", out var actionInputProp))
            {
                actionInput = actionInputProp.ValueKind == JsonValueKind.String
                    ? actionInputProp.GetString() ?? ""
                    : actionInputProp.GetRawText();
            }

            // If action is empty, treat as finish
            if (string.IsNullOrEmpty(action))
            {
                action = "finish";
                actionInput = thought;
            }

            return (thought, action, actionInput);
        }
        catch (JsonException)
        {
            // If JSON parsing fails, fall back to text parsing
            return ParseResponse(response);
        }
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
