// <copyright file="WorkspaceIdGeneratorTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Tests.Data;

using Aura.Foundation.Data;
using FluentAssertions;
using Xunit;

public class WorkspaceIdGeneratorTests
{
    [Fact]
    public void GenerateId_ValidPath_Returns16CharHexString()
    {
        var id = WorkspaceIdGenerator.GenerateId("/home/dev/project");

        id.Should().HaveLength(WorkspaceIdGenerator.IdLength);
        id.Should().MatchRegex("^[0-9a-f]{16}$");
    }

    [Fact]
    public void GenerateId_SamePath_ReturnsSameId()
    {
        var id1 = WorkspaceIdGenerator.GenerateId("/home/dev/project");
        var id2 = WorkspaceIdGenerator.GenerateId("/home/dev/project");

        id1.Should().Be(id2);
    }

    [Fact]
    public void GenerateId_DifferentPaths_ReturnDifferentIds()
    {
        var id1 = WorkspaceIdGenerator.GenerateId("/home/dev/project-a");
        var id2 = WorkspaceIdGenerator.GenerateId("/home/dev/project-b");

        id1.Should().NotBe(id2);
    }

    [Fact]
    public void GenerateId_SamePathDifferentSlashStyles_ReturnsSameId()
    {
        var id1 = WorkspaceIdGenerator.GenerateId(@"C:\Work\Aura");
        var id2 = WorkspaceIdGenerator.GenerateId("C:/Work/Aura");

        id1.Should().Be(id2);
    }

    [Fact]
    public void GenerateId_SamePathDifferentCasing_ReturnsSameId()
    {
        var id1 = WorkspaceIdGenerator.GenerateId(@"C:\Work\Aura");
        var id2 = WorkspaceIdGenerator.GenerateId("c:/work/aura");

        id1.Should().Be(id2);
    }

    [Fact]
    public void GenerateId_NullPath_ThrowsArgumentException()
    {
        var act = () => WorkspaceIdGenerator.GenerateId(null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GenerateId_EmptyPath_ThrowsArgumentException()
    {
        var act = () => WorkspaceIdGenerator.GenerateId("");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GenerateId_WhitespacePath_ThrowsArgumentException()
    {
        var act = () => WorkspaceIdGenerator.GenerateId("   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GenerateId_ResultIsLowercase()
    {
        var id = WorkspaceIdGenerator.GenerateId(@"C:\Work\Aura");

        id.Should().Be(id.ToLowerInvariant());
    }

    [Fact]
    public void IsValidId_ValidId_ReturnsTrue()
    {
        var id = WorkspaceIdGenerator.GenerateId("/home/dev/project");

        WorkspaceIdGenerator.IsValidId(id).Should().BeTrue();
    }

    [Fact]
    public void IsValidId_Null_ReturnsFalse()
    {
        WorkspaceIdGenerator.IsValidId(null).Should().BeFalse();
    }

    [Fact]
    public void IsValidId_Empty_ReturnsFalse()
    {
        WorkspaceIdGenerator.IsValidId("").Should().BeFalse();
    }

    [Fact]
    public void IsValidId_TooShort_ReturnsFalse()
    {
        WorkspaceIdGenerator.IsValidId("abcdef").Should().BeFalse();
    }

    [Fact]
    public void IsValidId_TooLong_ReturnsFalse()
    {
        WorkspaceIdGenerator.IsValidId("abcdef0123456789extra").Should().BeFalse();
    }

    [Fact]
    public void IsValidId_UppercaseHex_ReturnsFalse()
    {
        WorkspaceIdGenerator.IsValidId("ABCDEF0123456789").Should().BeFalse();
    }

    [Fact]
    public void IsValidId_NonHexCharacters_ReturnsFalse()
    {
        WorkspaceIdGenerator.IsValidId("ghijklmnopqrstuv").Should().BeFalse();
    }

    [Fact]
    public void IsValidId_ValidLowercaseHex16Chars_ReturnsTrue()
    {
        WorkspaceIdGenerator.IsValidId("abcdef0123456789").Should().BeTrue();
    }
}
