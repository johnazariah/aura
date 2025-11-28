// <copyright file="RagContentTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Tests.Rag;

using Aura.Foundation.Rag;
using FluentAssertions;
using Xunit;

public class RagContentTests
{
    [Fact]
    public void FromFile_CSharpFile_DetectsCodeType()
    {
        // Arrange
        var path = "/path/to/file.cs";
        var content = "public class Test {}";

        // Act
        var ragContent = RagContent.FromFile(path, content);

        // Assert
        ragContent.ContentType.Should().Be(RagContentType.Code);
        ragContent.ContentId.Should().Be(path);
        ragContent.SourcePath.Should().Be(path);
        ragContent.Language.Should().Be("csharp");
    }

    [Fact]
    public void FromFile_MarkdownFile_DetectsMarkdownType()
    {
        // Arrange
        var path = "/docs/readme.md";
        var content = "# Hello World";

        // Act
        var ragContent = RagContent.FromFile(path, content);

        // Assert
        ragContent.ContentType.Should().Be(RagContentType.Markdown);
        ragContent.Language.Should().BeNull();
    }

    [Fact]
    public void FromFile_TextFile_DetectsPlainTextType()
    {
        // Arrange
        var path = "/notes.txt";
        var content = "Just some notes";

        // Act
        var ragContent = RagContent.FromFile(path, content);

        // Assert
        ragContent.ContentType.Should().Be(RagContentType.PlainText);
    }

    [Fact]
    public void FromFile_PythonFile_DetectsCodeWithLanguage()
    {
        // Arrange
        var path = "script.py";
        var content = "def hello(): pass";

        // Act
        var ragContent = RagContent.FromFile(path, content);

        // Assert
        ragContent.ContentType.Should().Be(RagContentType.Code);
        ragContent.Language.Should().Be("python");
    }

    [Fact]
    public void FromFile_TypeScriptFile_DetectsCodeWithLanguage()
    {
        // Arrange
        var path = "app.ts";
        var content = "const x: number = 1;";

        // Act
        var ragContent = RagContent.FromFile(path, content);

        // Assert
        ragContent.ContentType.Should().Be(RagContentType.Code);
        ragContent.Language.Should().Be("typescript");
    }

    [Fact]
    public void FromFile_JavaScriptFile_DetectsCodeWithLanguage()
    {
        // Arrange
        var path = "app.js";
        var content = "const x = 1;";

        // Act
        var ragContent = RagContent.FromFile(path, content);

        // Assert
        ragContent.ContentType.Should().Be(RagContentType.Code);
        ragContent.Language.Should().Be("javascript");
    }

    [Fact]
    public void FromFile_WithOverrideType_UsesOverride()
    {
        // Arrange
        var path = "data.json";
        var content = "{}";

        // Act
        var ragContent = RagContent.FromFile(path, content, RagContentType.Code);

        // Assert
        ragContent.ContentType.Should().Be(RagContentType.Code);
    }

    [Theory]
    [InlineData(".cs", "csharp")]
    [InlineData(".fs", "fsharp")]
    [InlineData(".py", "python")]
    [InlineData(".ts", "typescript")]
    [InlineData(".js", "javascript")]
    [InlineData(".java", "java")]
    [InlineData(".go", "go")]
    [InlineData(".rs", "rust")]
    [InlineData(".cpp", "cpp")]
    [InlineData(".c", "c")]
    public void FromFile_CodeExtensions_DetectsCorrectLanguage(string extension, string expectedLanguage)
    {
        // Act
        var ragContent = RagContent.FromFile("file" + extension, "code");

        // Assert
        ragContent.Language.Should().Be(expectedLanguage);
    }

    [Fact]
    public void Constructor_SetsPropertiesCorrectly()
    {
        // Arrange
        var contentId = "test-id";
        var text = "test content";
        var contentType = RagContentType.Code;

        // Act
        var ragContent = new RagContent(contentId, text, contentType)
        {
            SourcePath = "/path",
            Language = "csharp",
            Metadata = new Dictionary<string, string> { ["key"] = "value" }
        };

        // Assert
        ragContent.ContentId.Should().Be(contentId);
        ragContent.Text.Should().Be(text);
        ragContent.ContentType.Should().Be(contentType);
        ragContent.SourcePath.Should().Be("/path");
        ragContent.Language.Should().Be("csharp");
        ragContent.Metadata.Should().ContainKey("key");
    }
}
