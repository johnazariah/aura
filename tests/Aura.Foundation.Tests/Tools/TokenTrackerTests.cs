using Xunit;
using FluentAssertions;
using Aura.Foundation.Tools;

namespace Aura.Foundation.Tests.Tools;

public class TokenTrackerTests
{
    [Fact]
    public void Constructor_WithValidBudget_CreateTracker()
    {
        // Arrange & Act
        var tracker = new TokenTracker(100_000);

        // Assert
        tracker.Budget.Should().Be(100_000);
        tracker.Used.Should().Be(0);
        tracker.Remaining.Should().Be(100_000);
        tracker.UsagePercent.Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_WithInvalidBudget_Throws(int budget)
    {
        // Act & Assert
        var act = () => new TokenTracker(budget);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Add_WithPositiveTokens_IncreasesUsed()
    {
        // Arrange
        var tracker = new TokenTracker(1000);

        // Act
        tracker.Add(100);
        tracker.Add(200);

        // Assert
        tracker.Used.Should().Be(300);
        tracker.Remaining.Should().Be(700);
        tracker.UsagePercent.Should().Be(30);
    }

    [Fact]
    public void Add_WithNegativeTokens_IsIgnored()
    {
        // Arrange
        var tracker = new TokenTracker(1000);
        tracker.Add(100);

        // Act
        tracker.Add(-50);

        // Assert
        tracker.Used.Should().Be(100);
    }

    [Theory]
    [InlineData(0, 70, false)]
    [InlineData(50, 70, false)]
    [InlineData(69, 70, false)]
    [InlineData(70, 70, true)]
    [InlineData(71, 70, true)]
    [InlineData(100, 70, true)]
    public void IsAboveThreshold_AtVariousLevels_ReturnsCorrectResult(
        int usedPercent,
        double threshold,
        bool expected)
    {
        // Arrange
        var tracker = new TokenTracker(100);
        tracker.Add(usedPercent);

        // Act
        var result = tracker.IsAboveThreshold(threshold);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(0, "continue")]
    [InlineData(49, "continue")]
    [InlineData(50, "summarize")]
    [InlineData(69, "summarize")]
    [InlineData(70, "spawn_subagent")]
    [InlineData(89, "spawn_subagent")]
    [InlineData(90, "complete_now")]
    [InlineData(100, "complete_now")]
    public void GetRecommendation_AtVariousLevels_ReturnsCorrectAction(
        int usedPercent,
        string expected)
    {
        // Arrange
        var tracker = new TokenTracker(100);
        tracker.Add(usedPercent);

        // Act
        var result = tracker.GetRecommendation();

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void GetSnapshot_ReturnsCorrectState()
    {
        // Arrange
        var tracker = new TokenTracker(1000);
        tracker.Add(700);

        // Act
        var snapshot = tracker.GetSnapshot();

        // Assert
        snapshot.Budget.Should().Be(1000);
        snapshot.Used.Should().Be(700);
        snapshot.Remaining.Should().Be(300);
        snapshot.UsagePercent.Should().Be(70);
        snapshot.Recommendation.Should().Be("spawn_subagent");
    }

    [Fact]
    public async Task ThreadSafety_ConcurrentAdds_CorrectTotal()
    {
        // Arrange
        var tracker = new TokenTracker(1_000_000);
        var tasks = new List<Task>();

        // Act - Add 1 token 10,000 times across 10 threads
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 1000; j++)
                {
                    tracker.Add(1);
                }
            }));
        }
        await Task.WhenAll(tasks);

        // Assert
        tracker.Used.Should().Be(10_000);
    }
}

