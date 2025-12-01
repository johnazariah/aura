using Aura.Foundation.Tools;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Aura.Foundation.Tests.Tools;

public class ToolRegistryTests
{
    private readonly ILogger<ToolRegistry> _logger;
    private readonly ToolRegistry _registry;

    public ToolRegistryTests()
    {
        _logger = Substitute.For<ILogger<ToolRegistry>>();
        _registry = new ToolRegistry(_logger);
    }

    [Fact]
    public void RegisterTool_AddsTool()
    {
        // Arrange
        var tool = CreateTestTool("test.tool");

        // Act
        _registry.RegisterTool(tool);

        // Assert
        Assert.True(_registry.HasTool("test.tool"));
        Assert.Single(_registry.GetAllTools());
    }

    [Fact]
    public void GetTool_ReturnsRegisteredTool()
    {
        // Arrange
        var tool = CreateTestTool("my.tool");
        _registry.RegisterTool(tool);

        // Act
        var retrieved = _registry.GetTool("my.tool");

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("my.tool", retrieved.ToolId);
    }

    [Fact]
    public void GetTool_IsCaseInsensitive()
    {
        // Arrange
        var tool = CreateTestTool("File.Read");
        _registry.RegisterTool(tool);

        // Act
        var retrieved = _registry.GetTool("FILE.READ");

        // Assert
        Assert.NotNull(retrieved);
    }

    [Fact]
    public void GetTool_ReturnsNullForUnknown()
    {
        // Act
        var retrieved = _registry.GetTool("nonexistent");

        // Assert
        Assert.Null(retrieved);
    }

    [Fact]
    public void UnregisterTool_RemovesTool()
    {
        // Arrange
        var tool = CreateTestTool("removable");
        _registry.RegisterTool(tool);
        Assert.True(_registry.HasTool("removable"));

        // Act
        var removed = _registry.UnregisterTool("removable");

        // Assert
        Assert.True(removed);
        Assert.False(_registry.HasTool("removable"));
    }

    [Fact]
    public void GetByCategory_ReturnsMatchingTools()
    {
        // Arrange
        _registry.RegisterTool(CreateTestTool("file.read", ["file", "io"]));
        _registry.RegisterTool(CreateTestTool("file.write", ["file", "io"]));
        _registry.RegisterTool(CreateTestTool("shell.execute", ["shell"]));

        // Act
        var fileTools = _registry.GetByCategory("file");
        var shellTools = _registry.GetByCategory("shell");

        // Assert
        Assert.Equal(2, fileTools.Count);
        Assert.Single(shellTools);
    }

    [Fact]
    public async Task ExecuteAsync_InvokesHandler()
    {
        // Arrange
        var handlerCalled = false;
        var tool = new ToolDefinition
        {
            ToolId = "test.execute",
            Name = "Test",
            Description = "Test tool",
            Handler = (input, ct) =>
            {
                handlerCalled = true;
                return Task.FromResult(ToolResult.Ok("executed"));
            }
        };
        _registry.RegisterTool(tool);

        // Act
        var result = await _registry.ExecuteAsync(new ToolInput { ToolId = "test.execute" });

        // Assert
        Assert.True(handlerCalled);
        Assert.True(result.Success);
        Assert.Equal("executed", result.Output);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsErrorForUnknownTool()
    {
        // Act
        var result = await _registry.ExecuteAsync(new ToolInput { ToolId = "unknown" });

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_CatchesExceptions()
    {
        // Arrange
        var tool = new ToolDefinition
        {
            ToolId = "test.throw",
            Name = "Throw",
            Description = "Throws",
            Handler = (_, _) => throw new InvalidOperationException("Test error")
        };
        _registry.RegisterTool(tool);

        // Act
        var result = await _registry.ExecuteAsync(new ToolInput { ToolId = "test.throw" });

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Test error", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_SetsDuration()
    {
        // Arrange
        var tool = new ToolDefinition
        {
            ToolId = "test.slow",
            Name = "Slow",
            Description = "Slow tool",
            Handler = async (_, ct) =>
            {
                await Task.Delay(50, ct);
                return ToolResult.Ok();
            }
        };
        _registry.RegisterTool(tool);

        // Act
        var result = await _registry.ExecuteAsync(new ToolInput { ToolId = "test.slow" });

        // Assert
        Assert.True(result.Success);
        Assert.True(result.Duration.TotalMilliseconds >= 40); // Allow some variance
    }

    private static ToolDefinition CreateTestTool(string toolId, IReadOnlyList<string>? categories = null) => new()
    {
        ToolId = toolId,
        Name = toolId,
        Description = $"Test tool {toolId}",
        Categories = categories ?? [],
        Handler = (_, _) => Task.FromResult(ToolResult.Ok())
    };
}
