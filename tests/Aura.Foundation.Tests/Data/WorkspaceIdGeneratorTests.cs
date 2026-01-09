// <copyright file="WorkspaceIdGeneratorTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Tests.Data;

using Aura.Foundation.Data;
using Xunit;

/// <summary>
/// Tests for <see cref="WorkspaceIdGenerator"/>.
/// </summary>
public sealed class WorkspaceIdGeneratorTests
{
    [Fact]
    public void GenerateId_SamePath_ReturnsSameId()
    {
        // Arrange
        const string path = @"C:\work\aura";

        // Act
        var id1 = WorkspaceIdGenerator.GenerateId(path);
        var id2 = WorkspaceIdGenerator.GenerateId(path);

        // Assert
        Assert.Equal(id1, id2);
    }

    [Fact]
    public void GenerateId_DifferentCasing_ReturnsSameId()
    {
        // Arrange - Windows paths with different casing should normalize to same ID
        const string path1 = @"C:\Work\Aura";
        const string path2 = @"c:\work\aura";
        const string path3 = @"C:\WORK\AURA";

        // Act
        var id1 = WorkspaceIdGenerator.GenerateId(path1);
        var id2 = WorkspaceIdGenerator.GenerateId(path2);
        var id3 = WorkspaceIdGenerator.GenerateId(path3);

        // Assert
        Assert.Equal(id1, id2);
        Assert.Equal(id2, id3);
    }

    [Fact]
    public void GenerateId_DifferentSlashStyles_ReturnsSameId()
    {
        // Arrange
        const string backslash = @"C:\work\aura";
        const string forwardSlash = "C:/work/aura";
        const string mixed = @"C:\work/aura";

        // Act
        var id1 = WorkspaceIdGenerator.GenerateId(backslash);
        var id2 = WorkspaceIdGenerator.GenerateId(forwardSlash);
        var id3 = WorkspaceIdGenerator.GenerateId(mixed);

        // Assert
        Assert.Equal(id1, id2);
        Assert.Equal(id2, id3);
    }

    [Fact]
    public void GenerateId_ReturnsCorrectLength()
    {
        // Arrange
        const string path = @"C:\work\aura";

        // Act
        var id = WorkspaceIdGenerator.GenerateId(path);

        // Assert
        Assert.Equal(WorkspaceIdGenerator.IdLength, id.Length);
    }

    [Fact]
    public void GenerateId_ReturnsLowercaseHex()
    {
        // Arrange
        const string path = @"C:\work\aura";

        // Act
        var id = WorkspaceIdGenerator.GenerateId(path);

        // Assert
        Assert.All(id, c => Assert.True(char.IsAsciiHexDigitLower(c) || char.IsAsciiDigit(c)));
    }

    [Fact]
    public void GenerateId_DifferentPaths_ReturnsDifferentIds()
    {
        // Arrange
        const string path1 = @"C:\work\aura";
        const string path2 = @"C:\work\other";

        // Act
        var id1 = WorkspaceIdGenerator.GenerateId(path1);
        var id2 = WorkspaceIdGenerator.GenerateId(path2);

        // Assert
        Assert.NotEqual(id1, id2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GenerateId_EmptyOrWhitespace_ThrowsArgumentException(string path)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => WorkspaceIdGenerator.GenerateId(path));
    }

    [Fact]
    public void GenerateId_Null_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => WorkspaceIdGenerator.GenerateId(null!));
    }

    [Theory]
    [InlineData("abc123def456ab12", true)]
    [InlineData("0123456789abcdef", true)]
    [InlineData("ABC123DEF456AB12", false)] // uppercase not allowed
    [InlineData("abc123def456ab1", false)]  // too short
    [InlineData("abc123def456ab123", false)] // too long
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("abc123def456ab1g", false)] // 'g' is not hex
    public void IsValidId_ValidatesCorrectly(string? id, bool expectedValid)
    {
        // Act
        var isValid = WorkspaceIdGenerator.IsValidId(id);

        // Assert
        Assert.Equal(expectedValid, isValid);
    }

    [Fact]
    public void GenerateId_ProducesValidId()
    {
        // Arrange
        const string path = @"C:\work\aura";

        // Act
        var id = WorkspaceIdGenerator.GenerateId(path);

        // Assert
        Assert.True(WorkspaceIdGenerator.IsValidId(id));
    }
}
