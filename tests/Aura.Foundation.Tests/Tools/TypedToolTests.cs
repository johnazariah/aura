using Aura.Foundation.Tools;
using Aura.Foundation.Tools.BuiltIn;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Aura.Foundation.Tests.Tools;

public class TypedToolTests
{
    private readonly ILogger<ToolRegistry> _logger;
    private readonly ToolRegistry _registry;

    public TypedToolTests()
    {
        _logger = Substitute.For<ILogger<ToolRegistry>>();
        _registry = new ToolRegistry(_logger);
    }

    [Fact]
    public void EchoTool_HasCorrectMetadata()
    {
        // Arrange
        var tool = new EchoTool();

        // Assert
        Assert.Equal("echo", tool.ToolId);
        Assert.Equal("Echo", tool.Name);
        Assert.Contains("Echoes back", tool.Description);
        Assert.False(tool.RequiresConfirmation);
    }

    [Fact]
    public async Task EchoTool_EchosMessage()
    {
        // Arrange
        var tool = new EchoTool();
        var input = new EchoInput
        {
            Message = "Hello, World!"
        };

        // Act
        var result = await tool.ExecuteAsync(input, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Output);
        Assert.Equal("Hello, World!", result.Output.EchoedMessage);
        Assert.Equal(13, result.Output.OriginalLength);
    }

    [Fact]
    public async Task EchoTool_WithPrefix_AddsPrefix()
    {
        // Arrange
        var tool = new EchoTool();
        var input = new EchoInput
        {
            Message = "Test",
            Prefix = "PREFIX"
        };

        // Act
        var result = await tool.ExecuteAsync(input, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Output);
        Assert.Equal("PREFIX: Test", result.Output.EchoedMessage);
    }

    [Fact]
    public void RegisterTypedTool_AddsToRegistry()
    {
        // Arrange
        var tool = new EchoTool();

        // Act
        _registry.RegisterTool<EchoInput, EchoOutput>(tool);

        // Assert
        Assert.True(_registry.HasTool("echo"));
        var retrieved = _registry.GetTool("echo");
        Assert.NotNull(retrieved);
    }

    [Fact]
    public async Task RegisterTypedTool_ExecutesThroughRegistry()
    {
        // Arrange
        var tool = new EchoTool();
        _registry.RegisterTool<EchoInput, EchoOutput>(tool);

        var input = new ToolInput
        {
            ToolId = "echo",
            Parameters = new Dictionary<string, object?>
            {
                ["message"] = "Registry test"
            }
        };

        // Act
        var result = await _registry.ExecuteAsync(input);

        // Assert
        Assert.True(result.Success);
        var output = Assert.IsType<EchoOutput>(result.Output);
        Assert.Equal("Registry test", output.EchoedMessage);
    }

    [Fact]
    public void GetToolDescriptionsForPrompt_IncludesTypedTools()
    {
        // Arrange
        var tool = new EchoTool();
        _registry.RegisterTool<EchoInput, EchoOutput>(tool);

        // Act
        var descriptions = _registry.GetToolDescriptionsForPrompt();

        // Assert
        Assert.Contains("echo", descriptions);
        Assert.Contains("Echoes back", descriptions);
    }

    [Fact]
    public void ToolResult_Ok_CreatesSuccessResult()
    {
        // Arrange & Act
        var output = new EchoOutput { EchoedMessage = "test", OriginalLength = 4 };
        var result = ToolResult<EchoOutput>.Ok(output);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Output);
        Assert.Equal("test", result.Output.EchoedMessage);
        Assert.Null(result.Error);
    }

    [Fact]
    public void ToolResult_Fail_CreatesFailureResult()
    {
        // Act
        var result = ToolResult<EchoOutput>.Fail("error message");

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Output);
        Assert.Equal("error message", result.Error);
    }

    [Fact]
    public void ToToolDefinition_CreatesValidDefinition()
    {
        // Arrange
        var tool = new EchoTool();

        // Act
        var definition = tool.ToToolDefinition();

        // Assert
        Assert.Equal("echo", definition.ToolId);
        Assert.Equal("Echo", definition.Name);
        Assert.Contains("Echoes back", definition.Description);
        Assert.False(definition.RequiresConfirmation);
        Assert.NotNull(definition.Handler);
    }

    [Fact]
    public async Task ToToolDefinition_HandlerWorksWithDictionary()
    {
        // Arrange
        var tool = new EchoTool();
        var definition = tool.ToToolDefinition();
        var input = new ToolInput
        {
            ToolId = "echo",
            Parameters = new Dictionary<string, object?>
            {
                ["message"] = "Through handler",
                ["prefix"] = "TEST"
            }
        };

        // Act
        var result = await definition.Handler!(input, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        var output = Assert.IsType<EchoOutput>(result.Output);
        Assert.Equal("TEST: Through handler", output.EchoedMessage);
    }

    [Fact]
    public async Task ToToolDefinition_InjectsWorkingDirectoryIntoParameters()
    {
        // Arrange
        var tool = new WorkingDirTool();
        var definition = tool.ToToolDefinition();
        var input = new ToolInput
        {
            ToolId = "workingdir.test",
            WorkingDirectory = @"C:\work\target-repo",  // This should be injected
            Parameters = new Dictionary<string, object?>
            {
                ["name"] = "TestProject"
                // Note: NOT providing workingDirectory in parameters
            }
        };

        // Act
        var result = await definition.Handler!(input, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        var output = Assert.IsType<WorkingDirOutput>(result.Output);
        Assert.Equal(@"C:\work\target-repo", output.ResolvedPath);
        Assert.Equal("TestProject", output.Name);
    }

    [Fact]
    public async Task ToToolDefinition_AlwaysUsesInjectedWorkingDirectory()
    {
        // Arrange
        var tool = new WorkingDirTool();
        var definition = tool.ToToolDefinition();
        var input = new ToolInput
        {
            ToolId = "workingdir.test",
            WorkingDirectory = @"C:\work\injected-path",  // This is injected by the system
            Parameters = new Dictionary<string, object?>
            {
                ["name"] = "TestProject",
                ["workingDirectory"] = @"C:\work\explicit-path"  // LLM tried to override - should be ignored
            }
        };

        // Act
        var result = await definition.Handler!(input, CancellationToken.None);

        // Assert - injected WorkingDirectory always wins (LLM cannot control working directory)
        Assert.True(result.Success);
        var output = Assert.IsType<WorkingDirOutput>(result.Output);
        Assert.Equal(@"C:\work\injected-path", output.ResolvedPath);  // Should use injected, not LLM-provided
    }
}

/// <summary>
/// Test input with WorkingDirectory parameter.
/// </summary>
public record WorkingDirInput
{
    public required string Name { get; init; }
    public string? WorkingDirectory { get; init; }
}

/// <summary>
/// Test output for WorkingDirectory tool.
/// </summary>
public record WorkingDirOutput
{
    public required string Name { get; init; }
    public required string ResolvedPath { get; init; }
}

/// <summary>
/// Test tool that uses WorkingDirectory in its input.
/// </summary>
public class WorkingDirTool : TypedToolBase<WorkingDirInput, WorkingDirOutput>
{
    public override string ToolId => "workingdir.test";
    public override string Name => "WorkingDir Test";
    public override string Description => "Test tool that uses working directory";

    public override Task<ToolResult<WorkingDirOutput>> ExecuteAsync(
        WorkingDirInput input,
        CancellationToken ct = default)
    {
        var output = new WorkingDirOutput
        {
            Name = input.Name,
            ResolvedPath = input.WorkingDirectory ?? "NOT_INJECTED"
        };
        return Task.FromResult(ToolResult<WorkingDirOutput>.Ok(output));
    }
}
