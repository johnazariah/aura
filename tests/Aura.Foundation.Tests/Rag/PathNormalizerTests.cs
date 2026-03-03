// <copyright file="PathNormalizerTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Tests.Rag;

using Aura.Foundation.Rag;
using FluentAssertions;
using Xunit;

public class PathNormalizerTests
{
    [Fact]
    public void Normalize_Null_ReturnsNull()
    {
        PathNormalizer.Normalize(null!).Should().BeNull();
    }

    [Fact]
    public void Normalize_EmptyString_ReturnsEmpty()
    {
        PathNormalizer.Normalize("").Should().BeEmpty();
    }

    [Fact]
    public void Normalize_BackslashPath_ConvertedToForwardSlash()
    {
        PathNormalizer.Normalize(@"C:\Users\Dev\project").Should().Be("c:/users/dev/project");
    }

    [Fact]
    public void Normalize_ForwardSlashPath_PreservedAsForwardSlash()
    {
        PathNormalizer.Normalize("/home/dev/project").Should().Be("/home/dev/project");
    }

    [Fact]
    public void Normalize_MixedSeparators_AllConvertedToForwardSlash()
    {
        PathNormalizer.Normalize(@"C:\Users/Dev\project/src").Should().Be("c:/users/dev/project/src");
    }

    [Fact]
    public void Normalize_EscapedBackslashes_ConvertedToForwardSlash()
    {
        PathNormalizer.Normalize("C:\\\\Users\\\\Dev\\\\project").Should().Be("c:/users/dev/project");
    }

    [Fact]
    public void Normalize_UppercasePath_ConvertedToLowercase()
    {
        PathNormalizer.Normalize("SRC/Models/USER.cs").Should().Be("src/models/user.cs");
    }

    [Fact]
    public void Normalize_MultipleConsecutiveSlashes_CollapsedToSingle()
    {
        PathNormalizer.Normalize("src///models//user.cs").Should().Be("src/models/user.cs");
    }

    [Fact]
    public void Normalize_UriScheme_PreservesSchemeDoubleSlash()
    {
        PathNormalizer.Normalize("file:///home/dev/project").Should().Be("file:///home/dev/project");
    }

    [Fact]
    public void Normalize_UriSchemeWithExtraSlashes_CollapsesAfterScheme()
    {
        PathNormalizer.Normalize("file:////home///dev//project").Should().Be("file:///home/dev/project");
    }

    [Theory]
    [InlineData(@"C:\Work\Aura", "c:/work/aura")]
    [InlineData("/work/aura", "/work/aura")]
    [InlineData(@"D:\Projects\My App\src", "d:/projects/my app/src")]
    public void Normalize_VariousPaths_NormalizesCorrectly(string input, string expected)
    {
        PathNormalizer.Normalize(input).Should().Be(expected);
    }

    [Fact]
    public void Normalize_SamePaths_DifferentFormats_ProduceSameResult()
    {
        var result1 = PathNormalizer.Normalize(@"C:\Work\Aura");
        var result2 = PathNormalizer.Normalize("C:/Work/Aura");
        var result3 = PathNormalizer.Normalize("c:/work/aura");

        result1.Should().Be(result2);
        result2.Should().Be(result3);
    }
}
