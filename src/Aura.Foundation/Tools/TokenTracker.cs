namespace Aura.Foundation.Tools;

/// <summary>
/// Tracks token usage during ReAct execution.
/// Thread-safe for use across async operations.
/// </summary>
public class TokenTracker
{
    private readonly int _budget;
    private int _used;
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenTracker"/> class.
    /// </summary>
    /// <param name="budget">The total token budget for this execution.</param>
    public TokenTracker(int budget)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(budget);
        _budget = budget;
    }

    /// <summary>Gets the total token budget.</summary>
    public int Budget => _budget;

    /// <summary>Gets the number of tokens used so far.</summary>
    public int Used
    {
        get
        {
            lock (_lock)
            {
                return _used;
            }
        }
    }

    /// <summary>Gets the remaining token budget.</summary>
    public int Remaining => Budget - Used;

    /// <summary>Gets the percentage of budget used (0-100).</summary>
    public double UsagePercent => Budget > 0 ? (double)Used / Budget * 100 : 0;

    /// <summary>
    /// Adds tokens to the usage count.
    /// </summary>
    /// <param name="tokens">The number of tokens to add.</param>
    public void Add(int tokens)
    {
        if (tokens < 0)
        {
            return;
        }

        lock (_lock)
        {
            _used += tokens;
        }
    }

    /// <summary>
    /// Checks if usage is above the specified threshold.
    /// </summary>
    /// <param name="thresholdPercent">The threshold percentage (default: 70).</param>
    /// <returns>True if usage exceeds the threshold.</returns>
    public bool IsAboveThreshold(double thresholdPercent = 70.0)
    {
        return UsagePercent >= thresholdPercent;
    }

    /// <summary>
    /// Gets a recommendation based on current usage level.
    /// </summary>
    /// <returns>A recommendation string: "continue", "summarize", "spawn_subagent", or "complete_now".</returns>
    public string GetRecommendation() => UsagePercent switch
    {
        >= 90 => "complete_now",
        >= 70 => "spawn_subagent",
        >= 50 => "summarize",
        _ => "continue"
    };

    /// <summary>
    /// Creates a snapshot of the current token state.
    /// </summary>
    /// <returns>A record containing the current budget state.</returns>
    public TokenBudgetSnapshot GetSnapshot() => new()
    {
        Budget = Budget,
        Used = Used,
        Remaining = Remaining,
        UsagePercent = UsagePercent,
        Recommendation = GetRecommendation()
    };
}

/// <summary>
/// Immutable snapshot of token budget state.
/// </summary>
public record TokenBudgetSnapshot
{
    /// <summary>Gets the total token budget.</summary>
    public required int Budget { get; init; }

    /// <summary>Gets the number of tokens used.</summary>
    public required int Used { get; init; }

    /// <summary>Gets the remaining tokens.</summary>
    public required int Remaining { get; init; }

    /// <summary>Gets the usage percentage (0-100).</summary>
    public required double UsagePercent { get; init; }

    /// <summary>Gets the recommended action.</summary>
    public required string Recommendation { get; init; }
}
