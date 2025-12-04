// <copyright file="PromptRegistryTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Tests.Prompts;

using System.IO.Abstractions.TestingHelpers;
using Aura.Foundation.Prompts;
using HandlebarsDotNet;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// Tests for <see cref="PromptRegistry"/> including Handlebars-style template rendering.
/// </summary>
public sealed class PromptRegistryTests
{
    private readonly MockFileSystem _fileSystem;
    private readonly PromptRegistry _registry;

    public PromptRegistryTests()
    {
        _fileSystem = new MockFileSystem();
        _fileSystem.AddDirectory("prompts");

        var options = Options.Create(new PromptOptions { Directories = ["prompts"] });
        var handlebars = Handlebars.Create(new HandlebarsConfiguration
        {
            ThrowOnUnresolvedBindingExpression = false,
            NoEscape = true,
        });
        _registry = new PromptRegistry(_fileSystem, options, handlebars, NullLogger<PromptRegistry>.Instance);
    }

    #region Basic Variable Substitution

    [Fact]
    public void Render_SimpleVariable_SubstitutesValue()
    {
        // Arrange
        _fileSystem.AddFile("prompts/test.prompt", new MockFileData("Hello, {{name}}!"));
        _registry.Reload();

        // Act
        var result = _registry.Render("test", new { name = "World" });

        // Assert
        Assert.Equal("Hello, World!", result);
    }

    [Fact]
    public void Render_MultipleVariables_SubstitutesAll()
    {
        // Arrange
        _fileSystem.AddFile("prompts/test.prompt", new MockFileData("{{greeting}}, {{name}}!"));
        _registry.Reload();

        // Act
        var result = _registry.Render("test", new { greeting = "Hello", name = "World" });

        // Assert
        Assert.Equal("Hello, World!", result);
    }

    [Fact]
    public void Render_NestedProperty_SubstitutesValue()
    {
        // Arrange
        _fileSystem.AddFile("prompts/test.prompt", new MockFileData("User: {{user.name}}"));
        _registry.Reload();

        // Act
        var result = _registry.Render("test", new { user = new { name = "Alice" } });

        // Assert
        Assert.Equal("User: Alice", result);
    }

    [Fact]
    public void Render_MissingVariable_ReplacesWithEmpty()
    {
        // Arrange
        _fileSystem.AddFile("prompts/test.prompt", new MockFileData("Hello, {{name}}!"));
        _registry.Reload();

        // Act
        var result = _registry.Render("test", new { });

        // Assert
        Assert.Equal("Hello, !", result);
    }

    #endregion

    #region If Blocks

    [Fact]
    public void Render_IfBlock_TruthyString_IncludesContent()
    {
        // Arrange
        _fileSystem.AddFile("prompts/test.prompt", new MockFileData(
            "Start{{#if showSection}}\nShown Content\n{{/if}}End"));
        _registry.Reload();

        // Act
        var result = _registry.Render("test", new { showSection = "yes" });

        // Assert
        Assert.Equal("Start\nShown Content\nEnd", result);
    }

    [Fact]
    public void Render_IfBlock_FalsyString_ExcludesContent()
    {
        // Arrange
        _fileSystem.AddFile("prompts/test.prompt", new MockFileData(
            "Start{{#if showSection}}\nHidden Content\n{{/if}}End"));
        _registry.Reload();

        // Act
        var result = _registry.Render("test", new { showSection = "" });

        // Assert
        Assert.Equal("StartEnd", result);
    }

    [Fact]
    public void Render_IfBlock_TruthyBool_IncludesContent()
    {
        // Arrange
        _fileSystem.AddFile("prompts/test.prompt", new MockFileData(
            "{{#if enabled}}Feature is ON{{/if}}"));
        _registry.Reload();

        // Act
        var result = _registry.Render("test", new { enabled = true });

        // Assert
        Assert.Equal("Feature is ON", result);
    }

    [Fact]
    public void Render_IfBlock_FalsyBool_ExcludesContent()
    {
        // Arrange
        _fileSystem.AddFile("prompts/test.prompt", new MockFileData(
            "{{#if enabled}}Feature is ON{{/if}}"));
        _registry.Reload();

        // Act
        var result = _registry.Render("test", new { enabled = false });

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Render_IfBlock_Null_ExcludesContent()
    {
        // Arrange
        _fileSystem.AddFile("prompts/test.prompt", new MockFileData(
            "{{#if data}}Has data{{/if}}"));
        _registry.Reload();

        // Act
        var result = _registry.Render("test", new { data = (string?)null });

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Render_IfElseBlock_Truthy_IncludesIfContent()
    {
        // Arrange
        _fileSystem.AddFile("prompts/test.prompt", new MockFileData(
            "{{#if premium}}Premium User{{else}}Free User{{/if}}"));
        _registry.Reload();

        // Act
        var result = _registry.Render("test", new { premium = true });

        // Assert
        Assert.Equal("Premium User", result);
    }

    [Fact]
    public void Render_IfElseBlock_Falsy_IncludesElseContent()
    {
        // Arrange
        _fileSystem.AddFile("prompts/test.prompt", new MockFileData(
            "{{#if premium}}Premium User{{else}}Free User{{/if}}"));
        _registry.Reload();

        // Act
        var result = _registry.Render("test", new { premium = false });

        // Assert
        Assert.Equal("Free User", result);
    }

    [Fact]
    public void Render_NestedIfBlocks_WorksCorrectly()
    {
        // Arrange
        _fileSystem.AddFile("prompts/test.prompt", new MockFileData(
            "{{#if outer}}Outer{{#if inner}}+Inner{{/if}}{{/if}}"));
        _registry.Reload();

        // Act
        var bothTrue = _registry.Render("test", new { outer = true, inner = true });
        var outerOnly = _registry.Render("test", new { outer = true, inner = false });
        var neitherTrue = _registry.Render("test", new { outer = false, inner = true });

        // Assert
        Assert.Equal("Outer+Inner", bothTrue);
        Assert.Equal("Outer", outerOnly);
        Assert.Equal(string.Empty, neitherTrue);
    }

    #endregion

    #region Unless Blocks

    [Fact]
    public void Render_UnlessBlock_Falsy_IncludesContent()
    {
        // Arrange
        _fileSystem.AddFile("prompts/test.prompt", new MockFileData(
            "{{#unless disabled}}Feature Available{{/unless}}"));
        _registry.Reload();

        // Act
        var result = _registry.Render("test", new { disabled = false });

        // Assert
        Assert.Equal("Feature Available", result);
    }

    [Fact]
    public void Render_UnlessBlock_Truthy_ExcludesContent()
    {
        // Arrange
        _fileSystem.AddFile("prompts/test.prompt", new MockFileData(
            "{{#unless disabled}}Feature Available{{/unless}}"));
        _registry.Reload();

        // Act
        var result = _registry.Render("test", new { disabled = true });

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Render_UnlessElseBlock_Works()
    {
        // Arrange
        _fileSystem.AddFile("prompts/test.prompt", new MockFileData(
            "{{#unless error}}Success{{else}}Error occurred{{/unless}}"));
        _registry.Reload();

        // Act
        var successResult = _registry.Render("test", new { error = false });
        var errorResult = _registry.Render("test", new { error = true });

        // Assert
        Assert.Equal("Success", successResult);
        Assert.Equal("Error occurred", errorResult);
    }

    #endregion

    #region Each Blocks

    [Fact]
    public void Render_EachBlock_IteratesOverCollection()
    {
        // Arrange
        _fileSystem.AddFile("prompts/test.prompt", new MockFileData(
            "Items:{{#each items}}\n- {{this}}{{/each}}"));
        _registry.Reload();

        // Act
        var result = _registry.Render("test", new { items = new[] { "Apple", "Banana", "Cherry" } });

        // Assert
        Assert.Equal("Items:\n- Apple\n- Banana\n- Cherry", result);
    }

    [Fact]
    public void Render_EachBlock_EmptyCollection_ShowsElse()
    {
        // Arrange
        _fileSystem.AddFile("prompts/test.prompt", new MockFileData(
            "{{#each items}}Item: {{this}}{{else}}No items{{/each}}"));
        _registry.Reload();

        // Act
        var result = _registry.Render("test", new { items = Array.Empty<string>() });

        // Assert
        Assert.Equal("No items", result);
    }

    [Fact]
    public void Render_EachBlock_ObjectProperties_Accessible()
    {
        // Arrange
        _fileSystem.AddFile("prompts/test.prompt", new MockFileData(
            "{{#each users}}{{name}} ({{email}})\n{{/each}}"));
        _registry.Reload();

        var users = new[]
        {
            new { name = "Alice", email = "alice@test.com" },
            new { name = "Bob", email = "bob@test.com" },
        };

        // Act
        var result = _registry.Render("test", new { users });

        // Assert
        Assert.Equal("Alice (alice@test.com)\nBob (bob@test.com)\n", result);
    }

    [Fact]
    public void Render_EachBlock_IndexVariable_Available()
    {
        // Arrange
        _fileSystem.AddFile("prompts/test.prompt", new MockFileData(
            "{{#each items}}{{@index}}: {{this}}\n{{/each}}"));
        _registry.Reload();

        // Act
        var result = _registry.Render("test", new { items = new[] { "A", "B", "C" } });

        // Assert
        Assert.Equal("0: A\n1: B\n2: C\n", result);
    }

    [Fact]
    public void Render_EachBlock_FirstLastVariables_Available()
    {
        // Arrange
        _fileSystem.AddFile("prompts/test.prompt", new MockFileData(
            "{{#each items}}{{#if @first}}[FIRST]{{/if}}{{this}}{{#if @last}}[LAST]{{/if}} {{/each}}"));
        _registry.Reload();

        // Act
        var result = _registry.Render("test", new { items = new[] { "A", "B", "C" } });

        // Assert
        Assert.Equal("[FIRST]A B C[LAST] ", result);
    }

    #endregion

    #region Complex Templates

    [Fact]
    public void Render_ComplexTemplate_AllFeaturesWork()
    {
        // Arrange
        var template = """
            # Task: {{taskName}}

            {{#if description}}
            ## Description
            {{description}}
            {{/if}}

            {{#if steps}}
            ## Steps
            {{#each steps}}
            {{@index}}. {{this}}
            {{/each}}
            {{else}}
            No steps defined.
            {{/if}}

            {{#unless skipFooter}}
            ---
            Generated by Aura
            {{/unless}}
            """;

        _fileSystem.AddFile("prompts/complex.prompt", new MockFileData(template));
        _registry.Reload();

        // Act
        var result = _registry.Render("complex", new
        {
            taskName = "Build Feature",
            description = "Implement the new feature",
            steps = new[] { "Design", "Implement", "Test" },
            skipFooter = false,
        });

        // Assert
        Assert.Contains("# Task: Build Feature", result);
        Assert.Contains("## Description", result);
        Assert.Contains("Implement the new feature", result);
        Assert.Contains("## Steps", result);
        Assert.Contains("0. Design", result);
        Assert.Contains("1. Implement", result);
        Assert.Contains("2. Test", result);
        Assert.Contains("Generated by Aura", result);
        Assert.DoesNotContain("No steps defined", result);
    }

    [Fact]
    public void Render_PromptWithFrontmatter_ParsesCorrectly()
    {
        // Arrange
        var template = """
            ---
            description: Test prompt with frontmatter
            ---
            Hello, {{name}}!
            """;

        _fileSystem.AddFile("prompts/frontmatter.prompt", new MockFileData(template));
        _registry.Reload();

        // Act
        var prompt = _registry.GetPrompt("frontmatter");
        var result = _registry.Render("frontmatter", new { name = "World" });

        // Assert
        Assert.NotNull(prompt);
        Assert.Equal("Test prompt with frontmatter", prompt.Description);
        Assert.Equal("Hello, World!", result);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Render_NonexistentPrompt_ThrowsException()
    {
        // Arrange
        _registry.Reload();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(
            () => _registry.Render("nonexistent", new { }));
        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public void Render_EmptyTemplate_ReturnsEmpty()
    {
        // Arrange
        _fileSystem.AddFile("prompts/empty.prompt", new MockFileData(string.Empty));
        _registry.Reload();

        // Act
        var result = _registry.Render("empty", new { });

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Render_DictionaryContext_WorksCorrectly()
    {
        // Arrange
        _fileSystem.AddFile("prompts/dict.prompt", new MockFileData("Key: {{key}}"));
        _registry.Reload();

        var context = new Dictionary<string, object?> { ["key"] = "value" };

        // Act
        var result = _registry.Render("dict", context);

        // Assert
        Assert.Equal("Key: value", result);
    }

    [Fact]
    public void Render_NonEmptyCollection_IsTruthy()
    {
        // Arrange
        _fileSystem.AddFile("prompts/test.prompt", new MockFileData(
            "{{#if items}}Has items{{else}}Empty{{/if}}"));
        _registry.Reload();

        // Act
        var withItems = _registry.Render("test", new { items = new[] { 1, 2, 3 } });
        var empty = _registry.Render("test", new { items = Array.Empty<int>() });

        // Assert
        Assert.Equal("Has items", withItems);
        Assert.Equal("Empty", empty);
    }

    [Fact]
    public void Render_NonZeroNumber_IsTruthy()
    {
        // Arrange
        _fileSystem.AddFile("prompts/test.prompt", new MockFileData(
            "{{#if count}}Count: {{count}}{{else}}None{{/if}}"));
        _registry.Reload();

        // Act
        var nonZero = _registry.Render("test", new { count = 5 });
        var zero = _registry.Render("test", new { count = 0 });

        // Assert
        Assert.Equal("Count: 5", nonZero);
        Assert.Equal("None", zero);
    }

    #endregion
}
