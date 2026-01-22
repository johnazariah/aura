using Aura.Foundation.Agents;
using Aura.Foundation.Llm;
using Aura.Foundation.Tools;
using Aura.Foundation.Tools.BuiltIn;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Aura.Foundation.Tests.Tools;

public class SpawnSubAgentToolTests
{
    private readonly IAgentRegistry _agentRegistry;
    private readonly IReActExecutor _reactExecutor;
    private readonly IToolRegistry _toolRegistry;
    private readonly ILlmProviderRegistry _llmProviderRegistry;
    private readonly ILogger<SpawnSubAgentTool> _logger;
    private readonly SpawnSubAgentTool _sut;

    public SpawnSubAgentToolTests()
    {
        _agentRegistry = Substitute.For<IAgentRegistry>();
        _reactExecutor = Substitute.For<IReActExecutor>();
        _toolRegistry = Substitute.For<IToolRegistry>();
        _llmProviderRegistry = Substitute.For<ILlmProviderRegistry>();
        _logger = Substitute.For<ILogger<SpawnSubAgentTool>>();

        _sut = new SpawnSubAgentTool(
            _agentRegistry,
            _reactExecutor,
            _toolRegistry,
            _llmProviderRegistry,
            _logger);
    }

    [Fact]
    public void ToolId_Should_Be_spawn_subagent()
    {
        _sut.ToolId.Should().Be("spawn_subagent");
    }

    [Fact]
    public void Name_Should_Be_Spawn_SubAgent()
    {
        _sut.Name.Should().Be("Spawn Sub-Agent");
    }

    [Fact]
    public void Categories_Should_Include_Agent_And_Execution()
    {
        _sut.Categories.Should().Contain("agent");
        _sut.Categories.Should().Contain("execution");
    }

    [Fact]
    public void RequiresConfirmation_Should_Be_False()
    {
        _sut.RequiresConfirmation.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_When_AgentNotFound_Returns_Failure()
    {
        // Arrange
        var input = new SpawnSubAgentInput
        {
            Agent = "non-existent-agent",
            Task = "Do something"
        };
        _agentRegistry.GetAgent("non-existent-agent").Returns((IAgent?)null);

        // Act
        var result = await _sut.ExecuteAsync(input, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task ExecuteAsync_When_NoLlmProvider_Returns_Failure()
    {
        // Arrange
        var mockAgent = CreateMockAgent("test-agent");
        _agentRegistry.GetAgent("test-agent").Returns(mockAgent);
        _llmProviderRegistry.GetDefaultProvider().Returns((ILlmProvider?)null);

        var input = new SpawnSubAgentInput
        {
            Agent = "test-agent",
            Task = "Do something"
        };

        // Act
        var result = await _sut.ExecuteAsync(input, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("No default LLM provider");
    }

    [Fact]
    public async Task ExecuteAsync_When_Successful_Returns_Summary()
    {
        // Arrange
        var mockAgent = CreateMockAgent("test-agent");
        var mockProvider = Substitute.For<ILlmProvider>();
        var tools = new List<ToolDefinition>();

        _agentRegistry.GetAgent("test-agent").Returns(mockAgent);
        _llmProviderRegistry.GetDefaultProvider().Returns(mockProvider);
        _toolRegistry.GetAllTools().Returns(tools);

        var reactResult = new ReActResult
        {
            Success = true,
            FinalAnswer = "Task completed successfully",
            Steps = new List<ReActStep>
            {
                new()
                {
                    StepNumber = 1,
                    Thought = "I need to do something",
                    Action = "file.read",
                    ActionInput = "test.txt",
                    Observation = "File contents",
                    CumulativeTokens = 500
                }
            }
        };

        _reactExecutor.ExecuteAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ToolDefinition>>(),
            Arg.Any<ILlmProvider>(),
            Arg.Any<ReActOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(reactResult));

        var input = new SpawnSubAgentInput
        {
            Agent = "test-agent",
            Task = "Complete the task",
            MaxSteps = 5
        };

        // Act
        var result = await _sut.ExecuteAsync(input, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().NotBeNull();
        result.Output!.Success.Should().BeTrue();
        result.Output.Summary.Should().Be("Task completed successfully");
        result.Output.StepsUsed.Should().Be(1);
        result.Output.TokensUsed.Should().Be(500);
    }

    [Fact]
    public async Task ExecuteAsync_Uses_MaxSteps_From_Input()
    {
        // Arrange
        var mockAgent = CreateMockAgent("test-agent");
        var mockProvider = Substitute.For<ILlmProvider>();

        _agentRegistry.GetAgent("test-agent").Returns(mockAgent);
        _llmProviderRegistry.GetDefaultProvider().Returns(mockProvider);
        _toolRegistry.GetAllTools().Returns(new List<ToolDefinition>());

        ReActOptions? capturedOptions = null;
        _reactExecutor.ExecuteAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ToolDefinition>>(),
            Arg.Any<ILlmProvider>(),
            Arg.Do<ReActOptions>(opt => capturedOptions = opt),
            Arg.Any<CancellationToken>())
            .Returns(new ReActResult
            {
                Success = true,
                FinalAnswer = "Done",
                Steps = new List<ReActStep>()
            });

        var input = new SpawnSubAgentInput
        {
            Agent = "test-agent",
            Task = "Test task",
            MaxSteps = 15
        };

        // Act
        await _sut.ExecuteAsync(input, CancellationToken.None);

        // Assert
        capturedOptions.Should().NotBeNull();
        capturedOptions!.MaxSteps.Should().Be(15);
    }

    [Fact]
    public async Task ExecuteAsync_Passes_WorkingDirectory()
    {
        // Arrange
        var mockAgent = CreateMockAgent("test-agent");
        var mockProvider = Substitute.For<ILlmProvider>();

        _agentRegistry.GetAgent("test-agent").Returns(mockAgent);
        _llmProviderRegistry.GetDefaultProvider().Returns(mockProvider);
        _toolRegistry.GetAllTools().Returns(new List<ToolDefinition>());

        ReActOptions? capturedOptions = null;
        _reactExecutor.ExecuteAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ToolDefinition>>(),
            Arg.Any<ILlmProvider>(),
            Arg.Do<ReActOptions>(opt => capturedOptions = opt),
            Arg.Any<CancellationToken>())
            .Returns(new ReActResult
            {
                Success = true,
                FinalAnswer = "Done",
                Steps = new List<ReActStep>()
            });

        var input = new SpawnSubAgentInput
        {
            Agent = "test-agent",
            Task = "Test task",
            WorkingDirectory = "/path/to/workspace"
        };

        // Act
        await _sut.ExecuteAsync(input, CancellationToken.None);

        // Assert
        capturedOptions.Should().NotBeNull();
        capturedOptions!.WorkingDirectory.Should().Be("/path/to/workspace");
    }

    [Fact]
    public async Task ExecuteAsync_Includes_Context_In_TaskPrompt()
    {
        // Arrange
        var mockAgent = CreateMockAgent("test-agent");
        var mockProvider = Substitute.For<ILlmProvider>();

        _agentRegistry.GetAgent("test-agent").Returns(mockAgent);
        _llmProviderRegistry.GetDefaultProvider().Returns(mockProvider);
        _toolRegistry.GetAllTools().Returns(new List<ToolDefinition>());

        string? capturedTask = null;
        _reactExecutor.ExecuteAsync(
            Arg.Do<string>(t => capturedTask = t),
            Arg.Any<IReadOnlyList<ToolDefinition>>(),
            Arg.Any<ILlmProvider>(),
            Arg.Any<ReActOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(new ReActResult
            {
                Success = true,
                FinalAnswer = "Done",
                Steps = new List<ReActStep>()
            });

        var input = new SpawnSubAgentInput
        {
            Agent = "test-agent",
            Task = "Complete the task",
            Context = "Here is important context"
        };

        // Act
        await _sut.ExecuteAsync(input, CancellationToken.None);

        // Assert
        capturedTask.Should().NotBeNull();
        capturedTask.Should().Contain("Here is important context");
        capturedTask.Should().Contain("Complete the task");
    }

    [Fact]
    public async Task ExecuteAsync_When_ReActFails_Returns_FailureOutput()
    {
        // Arrange
        var mockAgent = CreateMockAgent("test-agent");
        var mockProvider = Substitute.For<ILlmProvider>();

        _agentRegistry.GetAgent("test-agent").Returns(mockAgent);
        _llmProviderRegistry.GetDefaultProvider().Returns(mockProvider);
        _toolRegistry.GetAllTools().Returns(new List<ToolDefinition>());

        _reactExecutor.ExecuteAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ToolDefinition>>(),
            Arg.Any<ILlmProvider>(),
            Arg.Any<ReActOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(new ReActResult
            {
                Success = false,
                FinalAnswer = "",
                Steps = new List<ReActStep>(),
                Error = "Something went wrong"
            });

        var input = new SpawnSubAgentInput
        {
            Agent = "test-agent",
            Task = "Test task"
        };

        // Act
        var result = await _sut.ExecuteAsync(input, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue(); // Tool itself succeeded
        result.Output!.Success.Should().BeFalse(); // But sub-agent failed
        result.Output.Error.Should().Be("Something went wrong");
    }

    private static IAgent CreateMockAgent(string agentId)
    {
        var agent = Substitute.For<IAgent>();
        agent.AgentId.Returns(agentId);
        agent.Metadata.Returns(new AgentMetadata(
            Name: "Test Agent",
            Description: "A test agent",
            Capabilities: new[] { "testing" }));
        return agent;
    }
}
