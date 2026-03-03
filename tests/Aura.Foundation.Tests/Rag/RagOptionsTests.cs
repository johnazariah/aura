// <copyright file="RagOptionsTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Tests.Rag;

using Aura.Foundation.Rag;
using FluentAssertions;
using Xunit;

public class RagOptionsTests
{
    [Fact]
    public void DefaultExcludePatterns_ContainsExpectedPatterns()
    {
        // Act
        var patterns = RagIndexOptions.DefaultExcludePatterns;

        // Assert
        patterns.Should().Contain("**/bin/**");
        patterns.Should().Contain("**/obj/**");
        patterns.Should().Contain("**/node_modules/**");
        patterns.Should().Contain("**/.git/**");
        patterns.Should().Contain("**/.vs/**");
    }

    [Fact]
    public void DefaultIncludePatterns_ContainsExpectedPatterns()
    {
        // Act
        var patterns = RagIndexOptions.DefaultIncludePatterns;

        // Assert
        patterns.Should().Contain("*.cs");
        patterns.Should().Contain("*.md");
        patterns.Should().Contain("*.pdf");
        patterns.Should().Contain("*.ts");
        patterns.Should().Contain("*.py");
        patterns.Should().Contain("*.json");
    }

    [Fact]
    public void EffectiveIncludePatterns_ReturnsDefaults_WhenIncludePatternsIsNull()
    {
        // Arrange
        var options = new RagIndexOptions();

        // Act
        var effective = options.EffectiveIncludePatterns;

        // Assert
        effective.Should().BeSameAs(RagIndexOptions.DefaultIncludePatterns);
    }

    [Fact]
    public void EffectiveIncludePatterns_ReturnsCustomPatterns_WhenSet()
    {
        // Arrange
        var custom = new List<string> { "*.razor", "*.html" };
        var options = new RagIndexOptions { IncludePatterns = custom };

        // Act
        var effective = options.EffectiveIncludePatterns;

        // Assert
        effective.Should().BeSameAs(custom);
        effective.Should().HaveCount(2);
        effective.Should().Contain("*.razor");
    }

    [Fact]
    public void EffectiveExcludePatterns_ReturnsDefaults_WhenExcludePatternsIsNull()
    {
        // Arrange
        var options = new RagIndexOptions();

        // Act
        var effective = options.EffectiveExcludePatterns;

        // Assert
        effective.Should().BeSameAs(RagIndexOptions.DefaultExcludePatterns);
    }

    [Fact]
    public void EffectiveExcludePatterns_ReturnsCustomPatterns_WhenSet()
    {
        // Arrange
        var custom = new List<string> { "**/build/**" };
        var options = new RagIndexOptions { ExcludePatterns = custom };

        // Act
        var effective = options.EffectiveExcludePatterns;

        // Assert
        effective.Should().BeSameAs(custom);
        effective.Should().HaveCount(1);
        effective.Should().Contain("**/build/**");
    }

    [Fact]
    public void Recursive_DefaultsToTrue()
    {
        // Arrange & Act
        var options = new RagIndexOptions();

        // Assert
        options.Recursive.Should().BeTrue();
    }

    [Fact]
    public void PreferGitTrackedFiles_DefaultsToTrue()
    {
        // Arrange & Act
        var options = new RagIndexOptions();

        // Assert
        options.PreferGitTrackedFiles.Should().BeTrue();
    }

    [Fact]
    public void ContentType_DefaultsToNull()
    {
        // Arrange & Act
        var options = new RagIndexOptions();

        // Assert
        options.ContentType.Should().BeNull();
    }

    [Fact]
    public void RagOptions_HasExpectedDefaults()
    {
        // Arrange & Act
        var options = new RagOptions();

        // Assert
        options.EmbeddingModel.Should().Be("nomic-embed-text");
        options.EmbeddingDimension.Should().Be(768);
        options.ChunkSize.Should().Be(2000);
        options.ChunkOverlap.Should().Be(200);
        options.DefaultTopK.Should().Be(5);
        options.MinRelevanceScore.Should().Be(0.3);
    }

    [Fact]
    public void RagQueryOptions_HasExpectedDefaults()
    {
        // Arrange & Act
        var options = new RagQueryOptions();

        // Assert
        options.TopK.Should().Be(5);
        options.MinScore.Should().BeNull();
        options.ContentTypes.Should().BeNull();
        options.SourcePathPrefix.Should().BeNull();
        options.PrioritizeFiles.Should().BeNull();
    }
}
