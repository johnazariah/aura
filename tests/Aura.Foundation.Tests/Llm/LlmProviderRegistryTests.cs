// <copyright file="LlmProviderRegistryTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Tests.Llm;

using Aura.Foundation.Llm;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

public class LlmProviderRegistryTests
{
    private readonly LlmProviderRegistry _sut;

    public LlmProviderRegistryTests()
    {
        var options = Options.Create(new LlmOptions { DefaultProvider = LlmProviders.Ollama });
        _sut = new LlmProviderRegistry(options, NullLogger<LlmProviderRegistry>.Instance);
    }

    [Fact]
    public void Register_AddsProviderToRegistry()
    {
        // Arrange
        var provider = CreateMockProvider("test-provider");

        // Act
        _sut.Register(provider);

        // Assert
        _sut.Providers.Should().ContainSingle();
        _sut.Providers.Should().Contain(provider);
    }

    [Fact]
    public void Register_MultipleTimes_OverwritesPrevious()
    {
        // Arrange
        var provider1 = CreateMockProvider("test-provider");
        var provider2 = CreateMockProvider("test-provider");
        _sut.Register(provider1);

        // Act
        _sut.Register(provider2);

        // Assert - registry uses upsert semantics
        _sut.Providers.Should().ContainSingle();
        _sut.GetProvider("test-provider").Should().Be(provider2);
    }

    [Fact]
    public void GetProvider_ExistingProvider_ReturnsProvider()
    {
        // Arrange
        var provider = CreateMockProvider(LlmProviders.Ollama);
        _sut.Register(provider);

        // Act
        var result = _sut.GetProvider(LlmProviders.Ollama);

        // Assert
        result.Should().Be(provider);
    }

    [Fact]
    public void GetProvider_NonExistingProvider_ReturnsNull()
    {
        // Act
        var result = _sut.GetProvider("non-existent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetProvider_CaseInsensitive_ReturnsProvider()
    {
        // Arrange
        var provider = CreateMockProvider(LlmProviders.Ollama.ToUpperInvariant());
        _sut.Register(provider);

        // Act
        var result = _sut.GetProvider(LlmProviders.Ollama.ToLowerInvariant());

        // Assert
        result.Should().Be(provider);
    }

    [Fact]
    public void GetDefaultProvider_WhenEmpty_ReturnsNull()
    {
        // Act
        var result = _sut.GetDefaultProvider();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetDefaultProvider_ReturnsConfiguredDefault()
    {
        // Arrange - the registry is configured with DefaultProvider = LlmProviders.Ollama in constructor
        var ollamaProvider = CreateMockProvider(LlmProviders.Ollama);
        var otherProvider = CreateMockProvider("other");
        _sut.Register(otherProvider);  // Register a different one first
        _sut.Register(ollamaProvider); // Then register the default

        // Act
        var result = _sut.GetDefaultProvider();

        // Assert - should return the configured default, not the first registered
        result.Should().Be(ollamaProvider);
    }

    [Fact]
    public void TryGetProvider_ExistingProvider_ReturnsTrueAndProvider()
    {
        // Arrange
        var provider = CreateMockProvider("test");
        _sut.Register(provider);

        // Act
        var found = _sut.TryGetProvider("test", out var result);

        // Assert
        found.Should().BeTrue();
        result.Should().Be(provider);
    }

    [Fact]
    public void TryGetProvider_NonExistingProvider_ReturnsFalseAndNull()
    {
        // Act
        var found = _sut.TryGetProvider("non-existent", out var result);

        // Assert
        found.Should().BeFalse();
        result.Should().BeNull();
    }

    private static ILlmProvider CreateMockProvider(string providerId)
    {
        var provider = Substitute.For<ILlmProvider>();
        provider.ProviderId.Returns(providerId);
        return provider;
    }
}
