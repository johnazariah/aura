using System.Text.Json;
using Aura.Foundation.Tools;
using Aura.Foundation.Tools.BuiltIn;
using FluentAssertions;
using Xunit;

namespace Aura.Foundation.Tests.Tools;

public class CheckTokenBudgetToolTests
{
    [Fact]
    public async Task ExecuteAsync_WhenTokenTrackerNotAvailable_ReturnsNotAvailable()
    {
        // Arrange
        var input = new ToolInput
        {
            ToolId = CheckTokenBudgetTool.ToolId,
            WorkingDirectory = "/test",
            Parameters = new Dictionary<string, object?>(),
            TokenTracker = null
        };

        // Act
        var result = await CheckTokenBudgetTool.ExecuteAsync(input, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        var output = JsonSerializer.Deserialize<JsonElement>(result.Output!.ToString()!);
        output.GetProperty("available").GetBoolean().Should().BeFalse();
        output.GetProperty("message").GetString().Should().Contain("not enabled");
    }

    [Fact]
    public async Task ExecuteAsync_WhenTokenTrackerAvailable_ReturnsBudgetStatus()
    {
        // Arrange
        var tracker = new TokenTracker(10000);
        tracker.Add(3000);

        var input = new ToolInput
        {
            ToolId = CheckTokenBudgetTool.ToolId,
            WorkingDirectory = "/test",
            Parameters = new Dictionary<string, object?>(),
            TokenTracker = tracker
        };

        // Act
        var result = await CheckTokenBudgetTool.ExecuteAsync(input, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        var output = JsonSerializer.Deserialize<JsonElement>(result.Output!.ToString()!);
        output.GetProperty("available").GetBoolean().Should().BeTrue();
        output.GetProperty("used").GetInt32().Should().Be(3000);
        output.GetProperty("remaining").GetInt32().Should().Be(7000);
        output.GetProperty("budget").GetInt32().Should().Be(10000);
        output.GetProperty("percentage").GetDouble().Should().Be(30.0);
        output.GetProperty("isAboveThreshold").GetBoolean().Should().BeFalse();
        output.GetProperty("recommendation").GetString().Should().Contain("sufficient");
    }

    [Theory]
    [InlineData(7500, "CAUTION")]
    [InlineData(8500, "WARNING")]
    [InlineData(9500, "CRITICAL")]
    public async Task ExecuteAsync_AtDifferentUsageLevels_ReturnsAppropriateRecommendation(int used, string expectedKeyword)
    {
        // Arrange
        var tracker = new TokenTracker(10000);
        tracker.Add(used);

        var input = new ToolInput
        {
            ToolId = CheckTokenBudgetTool.ToolId,
            WorkingDirectory = "/test",
            Parameters = new Dictionary<string, object?>(),
            TokenTracker = tracker
        };

        // Act
        var result = await CheckTokenBudgetTool.ExecuteAsync(input, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        var output = JsonSerializer.Deserialize<JsonElement>(result.Output!.ToString()!);
        output.GetProperty("recommendation").GetString().Should().Contain(expectedKeyword);
        output.GetProperty("isAboveThreshold").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void GetDefinition_ReturnsValidToolDefinition()
    {
        // Act
        var definition = CheckTokenBudgetTool.GetDefinition();

        // Assert
        definition.ToolId.Should().Be("check_token_budget");
        definition.Name.Should().Be("Check Token Budget");
        definition.Description.Should().Contain("token budget");
        definition.Handler.Should().NotBeNull();
        definition.InputSchema.Should().Contain("object");
    }
}
