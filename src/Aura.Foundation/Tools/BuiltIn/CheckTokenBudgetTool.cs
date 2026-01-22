using System.Text.Json;

namespace Aura.Foundation.Tools.BuiltIn;

/// <summary>
/// A read-only tool that returns the current token budget status.
/// Allows agents to check their remaining context window capacity
/// and make informed decisions about spawning sub-agents.
/// </summary>
public static class CheckTokenBudgetTool
{
    public const string ToolId = "check_token_budget";

    /// <summary>
    /// Gets the tool definition for registration.
    /// </summary>
    public static ToolDefinition GetDefinition() => new()
    {
        ToolId = ToolId,
        Name = "Check Token Budget",
        Description = "Check your current token budget status. Returns used tokens, remaining tokens, and whether you should consider spawning a sub-agent for complex subtasks.",
        InputSchema = """
            {
                "type": "object",
                "properties": {},
                "required": []
            }
            """,
        Handler = ExecuteAsync
    };

    /// <summary>
    /// Execute the budget check. Returns budget status from TokenTracker in context.
    /// </summary>
    public static Task<ToolResult> ExecuteAsync(ToolInput input, CancellationToken ct)
    {
        // Get TokenTracker from context
        var tracker = input.TokenTracker;

        if (tracker is null)
        {
            var notAvailable = new
            {
                available = false,
                message = "Token budget tracking is not enabled for this execution."
            };
            return Task.FromResult(ToolResult.Ok(JsonSerializer.Serialize(notAvailable)));
        }

        var recommendation = GetRecommendation(tracker);
        var isAboveThreshold = tracker.IsAboveThreshold();

        var status = new
        {
            available = true,
            used = tracker.Used,
            remaining = tracker.Remaining,
            budget = tracker.Budget,
            percentage = Math.Round(tracker.UsagePercent, 1),
            isAboveThreshold,
            recommendation
        };

        return Task.FromResult(ToolResult.Ok(JsonSerializer.Serialize(status)));
    }

    private static string GetRecommendation(TokenTracker tracker)
    {
        var percent = tracker.UsagePercent;

        if (percent < 70)
        {
            return "You have sufficient context remaining. Continue with your current approach.";
        }

        if (percent > 90)
        {
            return "CRITICAL: Context nearly exhausted (>90%). Immediately spawn a sub-agent for any remaining complex tasks or wrap up with a summary.";
        }

        if (percent > 80)
        {
            return "WARNING: Context is running low (>80%). Consider spawning a sub-agent for any remaining complex subtasks.";
        }

        return "CAUTION: Approaching context limit (>70%). Plan to spawn sub-agents for complex remaining work.";
    }
}
