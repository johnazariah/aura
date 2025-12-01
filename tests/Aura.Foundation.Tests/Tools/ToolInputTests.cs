using Aura.Foundation.Tools;
using Xunit;

namespace Aura.Foundation.Tests.Tools;

public class ToolInputTests
{
    [Fact]
    public void GetParameter_ReturnsTypedValue()
    {
        // Arrange
        var input = new ToolInput
        {
            ToolId = "test",
            Parameters = new Dictionary<string, object?>
            {
                ["name"] = "test-value",
                ["count"] = 42,
                ["enabled"] = true
            }
        };

        // Act & Assert
        Assert.Equal("test-value", input.GetParameter<string>("name"));
        Assert.Equal(42, input.GetParameter<int>("count"));
        Assert.True(input.GetParameter<bool>("enabled"));
    }

    [Fact]
    public void GetParameter_ReturnsDefaultForMissing()
    {
        // Arrange
        var input = new ToolInput { ToolId = "test" };

        // Act & Assert
        Assert.Null(input.GetParameter<string>("missing"));
        Assert.Equal(0, input.GetParameter<int>("missing"));
        Assert.Equal("default", input.GetParameter("missing", "default"));
    }

    [Fact]
    public void GetParameter_ConvertsTypes()
    {
        // Arrange
        var input = new ToolInput
        {
            ToolId = "test",
            Parameters = new Dictionary<string, object?>
            {
                ["number"] = "123"
            }
        };

        // Act & Assert
        Assert.Equal(123, input.GetParameter<int>("number"));
    }

    [Fact]
    public void GetRequiredParameter_ThrowsForMissing()
    {
        // Arrange
        var input = new ToolInput { ToolId = "test" };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => input.GetRequiredParameter<string>("required"));
    }

    [Fact]
    public void GetRequiredParameter_ReturnsValue()
    {
        // Arrange
        var input = new ToolInput
        {
            ToolId = "test",
            Parameters = new Dictionary<string, object?>
            {
                ["path"] = "/some/path"
            }
        };

        // Act & Assert
        Assert.Equal("/some/path", input.GetRequiredParameter<string>("path"));
    }
}
