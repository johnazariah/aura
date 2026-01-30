// <copyright file="StubLlmProviderTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Tests.Llm;

using Aura.Foundation.Agents;
using Aura.Foundation.Llm;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class StubLlmProviderTests
{
    private readonly StubLlmProvider _sut;

    public StubLlmProviderTests()
    {
        _sut = new StubLlmProvider(NullLogger<StubLlmProvider>.Instance);
    }

    [Fact]
    public void ProviderId_ReturnsStub()
    {
        // Assert
        _sut.ProviderId.Should().Be(LlmProviders.Stub);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsSuccessWithEchoResponse()
    {
        // Arrange
        var prompt = "Hello, world!";

        // Act
        var result = await _sut.GenerateAsync("any-model", prompt);

        // Assert
        result.Content.Should().Contain(prompt);
        result.Content.Should().Contain("[Stub response to:");
    }

    [Fact]
    public async Task GenerateAsync_IncludesModelInResponse()
    {
        // Arrange
        var model = "test-model";

        // Act
        var result = await _sut.GenerateAsync(model, "test prompt");

        // Assert
        result.Model.Should().Be(model);
    }

    [Fact]
    public async Task ChatAsync_ReturnsSuccessWithResponse()
    {
        // Arrange
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello!"),
            new(ChatRole.Assistant, "Hi there!"),
            new(ChatRole.User, "How are you?")
        };

        // Act
        var result = await _sut.ChatAsync("any-model", messages);

        // Assert
        result.Content.Should().Contain("[Stub chat response to:");
        result.Content.Should().Contain("How are you?"); // Last user message
    }

    [Fact]
    public async Task IsModelAvailableAsync_AlwaysReturnsTrue()
    {
        // Act
        var result = await _sut.IsModelAvailableAsync("any-model");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ListModelsAsync_ReturnsModelsIncludingStubModel()
    {
        // Act
        var result = await _sut.ListModelsAsync();

        // Assert
        result.Should().HaveCountGreaterThanOrEqualTo(1);
        result.Should().Contain(m => m.Name == "stub-model");
    }

    [Fact]
    public async Task GenerateAsync_SetsTokensUsed()
    {
        // Act
        var result = await _sut.GenerateAsync("model", "test");

        // Assert
        result.TokensUsed.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GenerateAsync_ThrowsWhenCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await _sut.Invoking(x => x.GenerateAsync("model", "test", cancellationToken: cts.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }
}
