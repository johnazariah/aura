// <copyright file="AgentReflectionServiceTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Tests.Agents;

using Aura.Foundation.Agents;
using Aura.Foundation.Llm;
using Aura.Foundation.Prompts;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

/// <summary>
/// Tests for <see cref="AgentReflectionService"/>.
/// </summary>
public class AgentReflectionServiceTests
{
    private readonly IPromptRegistry _promptRegistry;
    private readonly ILlmProviderRegistry _providerRegistry;
    private readonly ILlmProvider _provider;
    private readonly ILogger<AgentReflectionService> _logger;
    private readonly AgentReflectionService _service;

    public AgentReflectionServiceTests()
    {
        _promptRegistry = Substitute.For<IPromptRegistry>();
        _providerRegistry = Substitute.For<ILlmProviderRegistry>();
        _provider = Substitute.For<ILlmProvider>();
        _logger = Substitute.For<ILogger<AgentReflectionService>>();

        // Setup provider registry to return our mock provider
        ILlmProvider? outProvider = _provider;
        _providerRegistry.TryGetProvider(Arg.Any<string>(), out Arg.Any<ILlmProvider?>())
            .Returns(x =>
            {
                x[1] = outProvider;
                return true;
            });

        // Setup prompt registry to return a rendered prompt
        _promptRegistry.Render(Arg.Any<string>(), Arg.Any<object>())
            .Returns("Rendered reflection prompt");

        _service = new AgentReflectionService(_promptRegistry, _providerRegistry, _logger);
    }

    [Fact]
    public async Task ReflectAsync_WhenReflectionDisabled_ReturnsOriginalResponse()
    {
        // Arrange
        var metadata = CreateMetadata(reflection: false);

        // Act
        var result = await _service.ReflectAsync("task", "response", metadata);

        // Assert
        result.Content.Should().Be("response");
        result.WasModified.Should().BeFalse();
        result.TokensUsed.Should().Be(0);

        // LLM should not have been called
        await _provider.DidNotReceive().GenerateAsync(
            Arg.Any<string?>(),
            Arg.Any<string>(),
            Arg.Any<double>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReflectAsync_WhenApproved_ReturnsOriginalResponse()
    {
        // Arrange
        var metadata = CreateMetadata(reflection: true);

        _provider.GenerateAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("APPROVED", TokensUsed: 50));

        // Act
        var result = await _service.ReflectAsync("task", "original response", metadata);

        // Assert
        result.Content.Should().Be("original response");
        result.WasModified.Should().BeFalse();
        result.TokensUsed.Should().Be(50);
    }

    [Fact]
    public async Task ReflectAsync_WhenApprovedWithWhitespace_ReturnsOriginalResponse()
    {
        // Arrange
        var metadata = CreateMetadata(reflection: true);

        _provider.GenerateAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("  APPROVED  \n", TokensUsed: 50));

        // Act
        var result = await _service.ReflectAsync("task", "original response", metadata);

        // Assert
        result.Content.Should().Be("original response");
        result.WasModified.Should().BeFalse();
    }

    [Fact]
    public async Task ReflectAsync_WhenModified_ReturnsCorrectedResponse()
    {
        // Arrange
        var metadata = CreateMetadata(reflection: true);

        _provider.GenerateAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("Here is the corrected response.", TokensUsed: 100));

        // Act
        var result = await _service.ReflectAsync("task", "original response with error", metadata);

        // Assert
        result.Content.Should().Be("Here is the corrected response.");
        result.WasModified.Should().BeTrue();
        result.TokensUsed.Should().Be(100);
    }

    [Fact]
    public async Task ReflectAsync_UsesCustomReflectionPrompt()
    {
        // Arrange
        var metadata = CreateMetadata(reflection: true, reflectionPrompt: "custom-reflection");

        _provider.GenerateAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("APPROVED", TokensUsed: 50));

        // Act
        await _service.ReflectAsync("task", "response", metadata);

        // Assert
        _promptRegistry.Received(1).Render("custom-reflection", Arg.Any<object>());
    }

    [Fact]
    public async Task ReflectAsync_UsesCustomReflectionModel()
    {
        // Arrange
        var metadata = CreateMetadata(
            reflection: true,
            model: "gpt-4o",
            reflectionModel: "gpt-4o-mini");

        _provider.GenerateAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("APPROVED", TokensUsed: 30));

        // Act
        await _service.ReflectAsync("task", "response", metadata);

        // Assert
        await _provider.Received(1).GenerateAsync(
            "gpt-4o-mini",
            Arg.Any<string>(),
            Arg.Any<double>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReflectAsync_WhenProviderFails_ReturnsOriginalResponse()
    {
        // Arrange
        var metadata = CreateMetadata(reflection: true);

        _provider.GenerateAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns<LlmResponse>(_ => throw new LlmException(LlmErrorCode.GenerationFailed, "Provider error"));

        // Act
        var result = await _service.ReflectAsync("task", "original response", metadata);

        // Assert
        result.Content.Should().Be("original response");
        result.WasModified.Should().BeFalse();
        result.TokensUsed.Should().Be(0);
    }

    [Fact]
    public async Task ReflectAsync_UsesLowTemperature()
    {
        // Arrange
        var metadata = CreateMetadata(reflection: true);

        _provider.GenerateAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("APPROVED", TokensUsed: 50));

        // Act
        await _service.ReflectAsync("task", "response", metadata);

        // Assert
        await _provider.Received(1).GenerateAsync(
            Arg.Any<string?>(),
            Arg.Any<string>(),
            0.3,
            Arg.Any<CancellationToken>());
    }

    private static AgentMetadata CreateMetadata(
        bool reflection = false,
        string? reflectionPrompt = null,
        string? reflectionModel = null,
        string? model = null,
        string? provider = LlmProviders.Ollama)
    {
        return new AgentMetadata(
            Name: "Test Agent",
            Description: "Test description",
            Capabilities: ["coding"],
            Provider: provider,
            Model: model,
            Reflection: reflection,
            ReflectionPrompt: reflectionPrompt,
            ReflectionModel: reflectionModel);
    }
}
