// <copyright file="CapabilitiesTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Tests.Agents;

using Aura.Foundation.Agents;
using Xunit;

public class CapabilitiesTests
{
    [Theory]
    [InlineData("chat")]
    [InlineData("coding")]
    [InlineData("review")]
    [InlineData("documentation")]
    [InlineData("analysis")]
    [InlineData("digestion")]
    [InlineData("fixing")]
    public void IsValid_WithBaseCapability_ReturnsTrue(string capability)
    {
        Assert.True(Capabilities.IsValid(capability));
    }

    [Theory]
    [InlineData("CHAT")]
    [InlineData("Coding")]
    [InlineData("REVIEW")]
    public void IsValid_IsCaseInsensitive(string capability)
    {
        Assert.True(Capabilities.IsValid(capability));
    }

    [Theory]
    [InlineData("ingest:cs")]
    [InlineData("ingest:csx")]
    [InlineData("ingest:py")]
    [InlineData("ingest:ts")]
    [InlineData("ingest:md")]
    [InlineData("ingest:txt")]
    [InlineData("ingest:*")]
    public void IsValid_WithIngestCapability_ReturnsTrue(string capability)
    {
        Assert.True(Capabilities.IsValid(capability));
    }

    [Theory]
    [InlineData("INGEST:cs")]
    [InlineData("Ingest:PY")]
    [InlineData("ingest:CS")]
    public void IsValid_IngestCapability_IsCaseInsensitive(string capability)
    {
        Assert.True(Capabilities.IsValid(capability));
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("random")]
    [InlineData("")]
    [InlineData("foo:bar")]
    [InlineData("test:abc")]
    public void IsValid_WithUnknownCapability_ReturnsFalse(string capability)
    {
        Assert.False(Capabilities.IsValid(capability));
    }

    [Fact]
    public void IngestPrefix_IsCorrect()
    {
        Assert.Equal("ingest:", Capabilities.IngestPrefix);
    }

    [Fact]
    public void All_ContainsBaseCapabilities()
    {
        Assert.Contains(Capabilities.Chat, Capabilities.All);
        Assert.Contains(Capabilities.Coding, Capabilities.All);
        Assert.Contains(Capabilities.Review, Capabilities.All);
        Assert.Contains(Capabilities.Documentation, Capabilities.All);
        Assert.Contains(Capabilities.Analysis, Capabilities.All);
        Assert.Contains(Capabilities.Digestion, Capabilities.All);
        Assert.Contains(Capabilities.Fixing, Capabilities.All);
    }

    [Fact]
    public void All_HasExpectedCount()
    {
        // 7 base capabilities: chat, coding, review, documentation, analysis, digestion, fixing
        Assert.Equal(7, Capabilities.All.Count);
    }
}
