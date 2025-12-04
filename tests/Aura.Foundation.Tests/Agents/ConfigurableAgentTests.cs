// <copyright file="ConfigurableAgentTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Tests.Agents;

using Aura.Foundation.Agents;
using Aura.Foundation.Llm;
using FluentAssertions;
using HandlebarsDotNet;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

/// <summary>
/// Tests for <see cref="ConfigurableAgent"/>.
/// </summary>
public class ConfigurableAgentTests
{
    private readonly ILlmProviderRegistry _providerRegistry;
    private readonly ILlmProvider _provider;
    private readonly IHandlebars _handlebars;
    private readonly ILogger<ConfigurableAgent> _logger;

    public ConfigurableAgentTests()
    {
        _providerRegistry = Substitute.For<ILlmProviderRegistry>();
        _provider = Substitute.For<ILlmProvider>();
        _logger = Substitute.For<ILogger<ConfigurableAgent>>();

        // Create real Handlebars instance for template testing
        _handlebars = Handlebars.Create(new HandlebarsConfiguration
        {
            ThrowOnUnresolvedBindingExpression = false,
            NoEscape = true,
        });

        _provider.ProviderId.Returns("ollama");
        _providerRegistry.TryGetProvider("ollama", out Arg.Any<ILlmProvider?>())
            .Returns(callInfo =>
            {
                callInfo[1] = _provider;
                return true;
            });
    }

    [Fact]
    public void AgentId_ReturnsDefinitionAgentId()
    {
        // Arrange
        var definition = CreateDefinition("test-agent");
        var agent = new ConfigurableAgent(definition, _providerRegistry, _handlebars, _logger);

        // Act & Assert
        agent.AgentId.Should().Be("test-agent");
    }

    [Fact]
    public void Metadata_ReturnsDefinitionMetadata()
    {
        // Arrange
        var definition = CreateDefinition("test-agent", name: "Test Agent", description: "Test");
        var agent = new ConfigurableAgent(definition, _providerRegistry, _handlebars, _logger);

        // Act
        var metadata = agent.Metadata;

        // Assert
        metadata.Name.Should().Be("Test Agent");
        metadata.Description.Should().Be("Test");
        metadata.Provider.Should().Be("ollama");
    }

    [Fact]
    public async Task ExecuteAsync_CallsLlmProvider()
    {
        // Arrange
        var definition = CreateDefinition("test-agent");
        var agent = new ConfigurableAgent(definition, _providerRegistry, _handlebars, _logger);
        var context = AgentContext.FromPrompt("Hello, world!");

        _provider.ChatAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ChatMessage>>(),
            Arg.Any<double>(),
            Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("Hello!", TokensUsed: 10));

        // Act
        var result = await agent.ExecuteAsync(context);

        // Assert
        result.Content.Should().Be("Hello!");
        result.TokensUsed.Should().Be(10);
    }

    [Fact]
    public async Task ExecuteAsync_IncludesSystemPromptInMessages()
    {
        // Arrange
        var definition = CreateDefinition("test-agent", systemPrompt: "You are a helpful assistant.");
        var agent = new ConfigurableAgent(definition, _providerRegistry, _handlebars, _logger);
        var context = AgentContext.FromPrompt("Hello!");

        IReadOnlyList<ChatMessage>? capturedMessages = null;
        _provider.ChatAsync(
            Arg.Any<string>(),
            Arg.Do<IReadOnlyList<ChatMessage>>(m => capturedMessages = m),
            Arg.Any<double>(),
            Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("Hi!"));

        // Act
        await agent.ExecuteAsync(context);

        // Assert
        capturedMessages.Should().NotBeNull();
        capturedMessages.Should().HaveCount(2); // System + User
        capturedMessages![0].Role.Should().Be(ChatRole.System);
        capturedMessages[0].Content.Should().Be("You are a helpful assistant.");
        capturedMessages[1].Role.Should().Be(ChatRole.User);
        capturedMessages[1].Content.Should().Be("Hello!");
    }

    [Fact]
    public async Task ExecuteAsync_SubstitutesTemplateVariables()
    {
        // Arrange
        var definition = CreateDefinition(
            "test-agent",
            systemPrompt: "Workspace: {{context.WorkspacePath}}");
        var agent = new ConfigurableAgent(definition, _providerRegistry, _handlebars, _logger);
        var context = AgentContext.FromPromptAndWorkspace("Do something", @"C:\work\project");

        IReadOnlyList<ChatMessage>? capturedMessages = null;
        _provider.ChatAsync(
            Arg.Any<string>(),
            Arg.Do<IReadOnlyList<ChatMessage>>(m => capturedMessages = m),
            Arg.Any<double>(),
            Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("Done!"));

        // Act
        await agent.ExecuteAsync(context);

        // Assert
        capturedMessages.Should().NotBeNull();
        capturedMessages![0].Content.Should().Be(@"Workspace: C:\work\project");
    }

    [Fact]
    public async Task ExecuteAsync_HandlesConditionalWithRagContext()
    {
        // Arrange
        var definition = CreateDefinition(
            "test-agent",
            systemPrompt: "{{#if context.RagContext}}RAG: {{context.RagContext}}{{/if}}End");
        var agent = new ConfigurableAgent(definition, _providerRegistry, _handlebars, _logger);
        var context = new AgentContext("Query", WorkspacePath: null)
        {
            RagContext = "File content here",
        };

        IReadOnlyList<ChatMessage>? capturedMessages = null;
        _provider.ChatAsync(
            Arg.Any<string>(),
            Arg.Do<IReadOnlyList<ChatMessage>>(m => capturedMessages = m),
            Arg.Any<double>(),
            Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("Done!"));

        // Act
        await agent.ExecuteAsync(context);

        // Assert
        capturedMessages.Should().NotBeNull();
        capturedMessages![0].Content.Should().Be("RAG: File content hereEnd");
    }

    [Fact]
    public async Task ExecuteAsync_ConditionalOmitsBlockWhenRagContextEmpty()
    {
        // Arrange
        var definition = CreateDefinition(
            "test-agent",
            systemPrompt: "Start{{#if context.RagContext}}RAG: {{context.RagContext}}{{/if}}End");
        var agent = new ConfigurableAgent(definition, _providerRegistry, _handlebars, _logger);
        var context = AgentContext.FromPrompt("Query"); // No RAG context

        IReadOnlyList<ChatMessage>? capturedMessages = null;
        _provider.ChatAsync(
            Arg.Any<string>(),
            Arg.Do<IReadOnlyList<ChatMessage>>(m => capturedMessages = m),
            Arg.Any<double>(),
            Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("Done!"));

        // Act
        await agent.ExecuteAsync(context);

        // Assert
        capturedMessages.Should().NotBeNull();
        capturedMessages![0].Content.Should().Be("StartEnd");
    }

    [Fact]
    public async Task ExecuteAsync_AppendsRagContextWhenNotInTemplate()
    {
        // Arrange - template doesn't mention RagContext
        var definition = CreateDefinition(
            "test-agent",
            systemPrompt: "You are a helpful assistant.");
        var agent = new ConfigurableAgent(definition, _providerRegistry, _handlebars, _logger);
        var context = new AgentContext("Query", WorkspacePath: null)
        {
            RagContext = "Important file content",
        };

        IReadOnlyList<ChatMessage>? capturedMessages = null;
        _provider.ChatAsync(
            Arg.Any<string>(),
            Arg.Do<IReadOnlyList<ChatMessage>>(m => capturedMessages = m),
            Arg.Any<double>(),
            Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("Done!"));

        // Act
        await agent.ExecuteAsync(context);

        // Assert
        capturedMessages.Should().NotBeNull();
        capturedMessages![0].Content.Should().Contain("You are a helpful assistant.");
        capturedMessages[0].Content.Should().Contain("Relevant Context from Knowledge Base");
        capturedMessages[0].Content.Should().Contain("Important file content");
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsWhenProviderNotFound()
    {
        // Arrange
        var definition = CreateDefinition("test-agent", provider: "unknown-provider");
        var agent = new ConfigurableAgent(definition, _providerRegistry, _handlebars, _logger);
        var context = AgentContext.FromPrompt("Hello!");

        _providerRegistry.TryGetProvider("unknown-provider", out Arg.Any<ILlmProvider?>())
            .Returns(false);
        _providerRegistry.GetDefaultProvider().Returns((ILlmProvider?)null);

        // Act & Assert
        var act = async () => await agent.ExecuteAsync(context);
        await act.Should().ThrowAsync<AgentException>()
            .Where(e => e.Code == AgentErrorCode.ProviderUnavailable);
    }

    [Fact]
    public async Task ExecuteAsync_FallsBackToDefaultProvider()
    {
        // Arrange
        var definition = CreateDefinition("test-agent", provider: "unknown-provider");
        var agent = new ConfigurableAgent(definition, _providerRegistry, _handlebars, _logger);
        var context = AgentContext.FromPrompt("Hello!");

        var defaultProvider = Substitute.For<ILlmProvider>();
        defaultProvider.ProviderId.Returns("default");
        defaultProvider.ChatAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ChatMessage>>(),
            Arg.Any<double>(),
            Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("Fallback!"));

        _providerRegistry.TryGetProvider("unknown-provider", out Arg.Any<ILlmProvider?>())
            .Returns(false);
        _providerRegistry.GetDefaultProvider().Returns(defaultProvider);

        // Act
        var result = await agent.ExecuteAsync(context);

        // Assert
        result.Content.Should().Be("Fallback!");
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsOnLlmFailure()
    {
        // Arrange
        var definition = CreateDefinition("test-agent");
        var agent = new ConfigurableAgent(definition, _providerRegistry, _handlebars, _logger);
        var context = AgentContext.FromPrompt("Hello!");

        _provider.ChatAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ChatMessage>>(),
            Arg.Any<double>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(LlmException.GenerationFailed("Model crashed"));

        // Act & Assert
        var act = async () => await agent.ExecuteAsync(context);
        await act.Should().ThrowAsync<AgentException>()
            .Where(e => e.Code == AgentErrorCode.ExecutionFailed);
    }

    [Fact]
    public async Task ExecuteAsync_PropagatesCancellation()
    {
        // Arrange
        var definition = CreateDefinition("test-agent");
        var agent = new ConfigurableAgent(definition, _providerRegistry, _handlebars, _logger);
        var context = AgentContext.FromPrompt("Hello!");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _provider.ChatAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ChatMessage>>(),
            Arg.Any<double>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        var act = async () => await agent.ExecuteAsync(context, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecuteAsync_IncludesConversationHistory()
    {
        // Arrange
        var definition = CreateDefinition("test-agent");
        var agent = new ConfigurableAgent(definition, _providerRegistry, _handlebars, _logger);
        var history = new List<ChatMessage>
        {
            new(ChatRole.User, "First message"),
            new(ChatRole.Assistant, "First response"),
        };
        var context = new AgentContext("Second message", history);

        IReadOnlyList<ChatMessage>? capturedMessages = null;
        _provider.ChatAsync(
            Arg.Any<string>(),
            Arg.Do<IReadOnlyList<ChatMessage>>(m => capturedMessages = m),
            Arg.Any<double>(),
            Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("Response!"));

        // Act
        await agent.ExecuteAsync(context);

        // Assert
        capturedMessages.Should().NotBeNull();
        capturedMessages.Should().HaveCount(4); // System + 2 history + User
        capturedMessages![1].Role.Should().Be(ChatRole.User);
        capturedMessages[1].Content.Should().Be("First message");
        capturedMessages[2].Role.Should().Be(ChatRole.Assistant);
        capturedMessages[2].Content.Should().Be("First response");
    }

    [Fact]
    public async Task ExecuteAsync_UsesCorrectTemperature()
    {
        // Arrange
        var definition = CreateDefinition("test-agent", temperature: 0.9);
        var agent = new ConfigurableAgent(definition, _providerRegistry, _handlebars, _logger);
        var context = AgentContext.FromPrompt("Hello!");

        double capturedTemperature = 0;
        _provider.ChatAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ChatMessage>>(),
            Arg.Do<double>(t => capturedTemperature = t),
            Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("Hi!"));

        // Act
        await agent.ExecuteAsync(context);

        // Assert
        capturedTemperature.Should().Be(0.9);
    }

    private static AgentDefinition CreateDefinition(
        string agentId,
        string name = "Test Agent",
        string description = "Test description",
        string provider = "ollama",
        string model = "qwen2.5-coder:7b",
        double temperature = 0.7,
        string systemPrompt = "You are a test agent.")
    {
        return new AgentDefinition(
            AgentId: agentId,
            Name: name,
            Description: description,
            Provider: provider,
            Model: model,
            Temperature: temperature,
            SystemPrompt: systemPrompt,
            Capabilities: [],
            Priority: AgentDefinition.DefaultPriority,
            Languages: [],
            Tags: [],
            Tools: []);
    }
}
