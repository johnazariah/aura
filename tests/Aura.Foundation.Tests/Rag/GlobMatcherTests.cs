// <copyright file="GlobMatcherTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Tests.Rag;

using Aura.Foundation.Rag;
using FluentAssertions;
using Xunit;

public class GlobMatcherTests
{
    [Theory]
    [InlineData("src/Models/User.cs", "*.cs", true)]
    [InlineData("src/Models/User.CS", "*.cs", true)]
    [InlineData("src/Models/User.txt", "*.cs", false)]
    [InlineData("readme.md", "*.md", true)]
    [InlineData("docs/readme.MD", "*.md", true)]
    public void Matches_ExtensionPattern_MatchesCorrectly(string path, string pattern, bool expected)
    {
        GlobMatcher.Matches(path, pattern).Should().Be(expected);
    }

    [Theory]
    [InlineData("src/bin/Debug/app.dll", "**/bin/**", true)]
    [InlineData("project/bin/Release/net8.0/app.dll", "**/bin/**", true)]
    [InlineData("bindings/config.json", "**/bin/**", false)]
    [InlineData("src/node_modules/lodash/index.js", "**/node_modules/**", true)]
    public void Matches_DoubleStarDirPattern_MatchesDirectoryAnywhere(string path, string pattern, bool expected)
    {
        GlobMatcher.Matches(path, pattern).Should().Be(expected);
    }

    [Theory]
    [InlineData("src/utils/helper.ts", "**/helper.ts", true)]
    [InlineData("helper.ts", "**/helper.ts", true)]
    [InlineData("src/deep/nested/helper.ts", "**/helper.ts", true)]
    public void Matches_DoubleStarFilePattern_MatchesFileAnywhere(string path, string pattern, bool expected)
    {
        GlobMatcher.Matches(path, pattern).Should().Be(expected);
    }

    [Theory]
    [InlineData("src\\bin\\Debug\\app.dll", "**/bin/**", true)]
    [InlineData("src\\Models\\User.cs", "*.cs", true)]
    public void Matches_BackslashPaths_NormalizesAndMatches(string path, string pattern, bool expected)
    {
        GlobMatcher.Matches(path, pattern).Should().Be(expected);
    }

    [Fact]
    public void Matches_SimpleContainsPattern_MatchesSubstring()
    {
        GlobMatcher.Matches("src/config/settings.json", "config").Should().BeTrue();
        GlobMatcher.Matches("src/models/user.cs", "config").Should().BeFalse();
    }

    [Theory]
    [InlineData("src/file.cs", "*.CS", true)]
    [InlineData("SRC/BIN/app.dll", "**/bin/**", true)]
    public void Matches_CaseInsensitive_MatchesRegardlessOfCase(string path, string pattern, bool expected)
    {
        GlobMatcher.Matches(path, pattern).Should().Be(expected);
    }

    [Fact]
    public void MatchesAny_MultiplePatterns_ReturnsTrueIfAnyMatches()
    {
        var patterns = new[] { "*.cs", "*.ts", "*.md" };

        GlobMatcher.MatchesAny("src/app.cs", patterns).Should().BeTrue();
        GlobMatcher.MatchesAny("src/app.ts", patterns).Should().BeTrue();
        GlobMatcher.MatchesAny("src/app.py", patterns).Should().BeFalse();
    }

    [Fact]
    public void MatchesAny_EmptyPatterns_ReturnsFalse()
    {
        GlobMatcher.MatchesAny("src/app.cs", []).Should().BeFalse();
    }

    [Fact]
    public void ShouldInclude_ExcludedFile_ReturnsFalse()
    {
        var includes = new[] { "*.cs" };
        var excludes = new[] { "**/bin/**" };

        GlobMatcher.ShouldInclude("src/bin/Debug/app.cs", includes, excludes).Should().BeFalse();
    }

    [Fact]
    public void ShouldInclude_IncludedNotExcluded_ReturnsTrue()
    {
        var includes = new[] { "*.cs" };
        var excludes = new[] { "**/bin/**" };

        GlobMatcher.ShouldInclude("src/Models/User.cs", includes, excludes).Should().BeTrue();
    }

    [Fact]
    public void ShouldInclude_NotInIncludePatterns_ReturnsFalse()
    {
        var includes = new[] { "*.cs" };
        var excludes = new[] { "**/bin/**" };

        GlobMatcher.ShouldInclude("src/readme.md", includes, excludes).Should().BeFalse();
    }

    [Fact]
    public void ShouldInclude_ExcludesTakePrecedence_ReturnsFalse()
    {
        var includes = new[] { "*.cs", "**/bin/**" };
        var excludes = new[] { "**/bin/**" };

        GlobMatcher.ShouldInclude("src/bin/Debug/app.cs", includes, excludes).Should().BeFalse();
    }
}
