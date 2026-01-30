// <copyright file="McpHandlerTypeScriptTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Tests.Mcp;

using System.Text.Json;
using Aura.Api.Mcp;
using Aura.Api.Mcp.Tools;
using Aura.Api.Services;
using Aura.Foundation.Git;
using Aura.Foundation.Rag;
using Aura.Module.Developer.GitHub;
using Aura.Module.Developer.Services;
using Aura.Module.Developer.Services.Testing;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

/// <summary>
/// Tests for TypeScript refactoring support in McpHandler.
/// Verifies that .ts/.tsx/.js/.jsx files route to the TypeScript refactoring service.
/// </summary>
public class McpHandlerTypeScriptTests
{
    private readonly IRagService _ragService;
    private readonly ICodeGraphService _graphService;
    private readonly IStoryService _storyService;
    private readonly IGitHubService _gitHubService;
    private readonly IRoslynWorkspaceService _roslynService;
    private readonly IRoslynRefactoringService _refactoringService;
    private readonly IPythonRefactoringService _pythonRefactoringService;
    private readonly ITypeScriptRefactoringService _typeScriptRefactoringService;
    private readonly ITestGenerationService _testGenerationService;
    private readonly IGitWorktreeService _worktreeService;
    private readonly ITreeBuilderService _treeBuilderService;
    private readonly IAuraDocsTool _auraDocsTool;
    private readonly IDocsService _docsService;
    private readonly IWorkspaceRegistryService _workspaceRegistryService;
    private readonly McpHandler _handler;

    public McpHandlerTypeScriptTests()
    {
        _ragService = Substitute.For<IRagService>();
        _graphService = Substitute.For<ICodeGraphService>();
        _storyService = Substitute.For<IStoryService>();
        _gitHubService = Substitute.For<IGitHubService>();
        _roslynService = Substitute.For<IRoslynWorkspaceService>();
        _refactoringService = Substitute.For<IRoslynRefactoringService>();
        _pythonRefactoringService = Substitute.For<IPythonRefactoringService>();
        _typeScriptRefactoringService = Substitute.For<ITypeScriptRefactoringService>();
        _testGenerationService = Substitute.For<ITestGenerationService>();
        _worktreeService = Substitute.For<IGitWorktreeService>();
        _treeBuilderService = Substitute.For<ITreeBuilderService>();
        _auraDocsTool = Substitute.For<IAuraDocsTool>();
        _docsService = Substitute.For<IDocsService>();
        _workspaceRegistryService = Substitute.For<IWorkspaceRegistryService>();

        _handler = new McpHandler(
            _ragService,
            _graphService,
            _storyService,
            _gitHubService,
            _roslynService,
            _refactoringService,
            _pythonRefactoringService,
            _typeScriptRefactoringService,
            _testGenerationService,
            _worktreeService,
            _treeBuilderService,
            _auraDocsTool,
            _docsService,
            _workspaceRegistryService,
            NullLogger<McpHandler>.Instance);
    }

    #region aura_navigate - TypeScript References

    [Theory]
    [InlineData("src/app.ts")]
    [InlineData("src/component.tsx")]
    [InlineData("src/utils.js")]
    [InlineData("src/helpers.jsx")]
    public async Task Navigate_References_WithTypeScriptFile_RoutesToTypeScriptService(string filePath)
    {
        // Arrange
        var expectedResult = new TypeScriptFindReferencesResult
        {
            Success = true,
            Count = 3,
            References = new List<TypeScriptReferenceLocation>
            {
                new() { File = filePath, Line = 10, Column = 5, Text = "myFunction" },
                new() { File = filePath, Line = 25, Column = 12, Text = "myFunction" },
                new() { File = "src/other.ts", Line = 8, Column = 3, Text = "myFunction" },
            },
        };

        _typeScriptRefactoringService.FindReferencesAsync(
            Arg.Any<TypeScriptFindReferencesRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var request = CreateToolCallRequest("aura_navigate", new
        {
            operation = "references",
            filePath,
            projectPath = "/project",
            offset = 42,
        });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        await _typeScriptRefactoringService.Received(1).FindReferencesAsync(
            Arg.Is<TypeScriptFindReferencesRequest>(r =>
                r.FilePath == filePath &&
                r.ProjectPath == "/project" &&
                r.Offset == 42),
            Arg.Any<CancellationToken>());

        var response = ParseResponse(responseJson);
        response.Error.Should().BeNull();
    }

    [Fact]
    public async Task Navigate_References_WithCSharpFile_DoesNotRouteToTypeScriptService()
    {
        // Arrange
        var request = CreateToolCallRequest("aura_navigate", new
        {
            operation = "references",
            filePath = "src/Service.cs",
            symbolName = "MyMethod",
            solutionPath = "c:\\test\\Test.sln",
        });

        // Act - will fail because Roslyn isn't set up, but we verify TypeScript wasn't called
        try
        {
            await _handler.HandleAsync(request);
        }
        catch
        {
            // Expected - Roslyn service isn't mocked for success
        }

        // Assert - TypeScript service should NOT be called for .cs files
        await _typeScriptRefactoringService.DidNotReceiveWithAnyArgs()
            .FindReferencesAsync(Arg.Any<TypeScriptFindReferencesRequest>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region aura_navigate - TypeScript Definition

    [Theory]
    [InlineData("src/app.ts")]
    [InlineData("src/component.tsx")]
    [InlineData("src/utils.js")]
    [InlineData("src/helpers.jsx")]
    public async Task Navigate_Definition_WithTypeScriptFile_RoutesToTypeScriptService(string filePath)
    {
        // Arrange
        var expectedResult = new TypeScriptFindDefinitionResult
        {
            Success = true,
            Found = true,
            FilePath = "src/definitions.ts",
            Line = 15,
            Column = 10,
            Offset = 320,
        };

        _typeScriptRefactoringService.FindDefinitionAsync(
            Arg.Any<TypeScriptFindDefinitionRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var request = CreateToolCallRequest("aura_navigate", new
        {
            operation = "definition",
            filePath,
            projectPath = "/project",
            offset = 100,
        });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        await _typeScriptRefactoringService.Received(1).FindDefinitionAsync(
            Arg.Is<TypeScriptFindDefinitionRequest>(r =>
                r.FilePath == filePath &&
                r.ProjectPath == "/project" &&
                r.Offset == 100),
            Arg.Any<CancellationToken>());

        var response = ParseResponse(responseJson);
        response.Error.Should().BeNull();
    }

    [Fact]
    public async Task Navigate_Definition_WithTypeScriptFile_MissingProjectPath_ReturnsError()
    {
        // Arrange
        var request = CreateToolCallRequest("aura_navigate", new
        {
            operation = "definition",
            filePath = "src/app.ts",
            offset = 100,
            // projectPath is missing
        });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        var response = ParseResponse(responseJson);
        response.Error.Should().BeNull(); // No JSON-RPC error, but...

        var resultText = GetResultText(responseJson);
        resultText.Should().Contain("projectPath is required");

        await _typeScriptRefactoringService.DidNotReceiveWithAnyArgs()
            .FindDefinitionAsync(Arg.Any<TypeScriptFindDefinitionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Navigate_Definition_WithTypeScriptFile_MissingOffset_ReturnsError()
    {
        // Arrange
        var request = CreateToolCallRequest("aura_navigate", new
        {
            operation = "definition",
            filePath = "src/app.ts",
            projectPath = "/project",
            // offset is missing
        });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        var resultText = GetResultText(responseJson);
        resultText.Should().Contain("offset is required");

        await _typeScriptRefactoringService.DidNotReceiveWithAnyArgs()
            .FindDefinitionAsync(Arg.Any<TypeScriptFindDefinitionRequest>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region aura_refactor - TypeScript Rename

    [Theory]
    [InlineData("src/app.ts")]
    [InlineData("src/component.tsx")]
    [InlineData("src/utils.js")]
    [InlineData("src/helpers.jsx")]
    public async Task Refactor_Rename_WithTypeScriptFile_RoutesToTypeScriptService(string filePath)
    {
        // Arrange
        var expectedResult = new TypeScriptRefactoringResult
        {
            Success = true,
            Preview = false,
            ChangedFiles = new List<string> { filePath, "src/other.ts" },
            Description = "Renamed to 'newName'",
        };

        _typeScriptRefactoringService.RenameSymbolAsync(
            Arg.Any<TypeScriptRenameRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var request = CreateToolCallRequest("aura_refactor", new
        {
            operation = "rename",
            filePath,
            projectPath = "/project",
            offset = 42,
            newName = "newName",
        });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        await _typeScriptRefactoringService.Received(1).RenameSymbolAsync(
            Arg.Is<TypeScriptRenameRequest>(r =>
                r.FilePath == filePath &&
                r.ProjectPath == "/project" &&
                r.Offset == 42 &&
                r.NewName == "newName"),
            Arg.Any<CancellationToken>());

        var response = ParseResponse(responseJson);
        response.Error.Should().BeNull();
    }

    #endregion

    #region aura_refactor - TypeScript Extract Method

    [Theory]
    [InlineData("src/app.ts")]
    [InlineData("src/component.tsx")]
    public async Task Refactor_ExtractMethod_WithTypeScriptFile_RoutesToTypeScriptService(string filePath)
    {
        // Arrange
        var expectedResult = new TypeScriptRefactoringResult
        {
            Success = true,
            Preview = false,
            ChangedFiles = new List<string> { filePath },
            Description = "Extracted function 'extractedFunction'",
        };

        _typeScriptRefactoringService.ExtractFunctionAsync(
            Arg.Any<TypeScriptExtractFunctionRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var request = CreateToolCallRequest("aura_refactor", new
        {
            operation = "extract_method",
            filePath,
            projectPath = "/project",
            startOffset = 100,
            endOffset = 200,
            newName = "extractedFunction",
        });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        await _typeScriptRefactoringService.Received(1).ExtractFunctionAsync(
            Arg.Is<TypeScriptExtractFunctionRequest>(r =>
                r.FilePath == filePath &&
                r.ProjectPath == "/project" &&
                r.StartOffset == 100 &&
                r.EndOffset == 200 &&
                r.NewName == "extractedFunction"),
            Arg.Any<CancellationToken>());

        var response = ParseResponse(responseJson);
        response.Error.Should().BeNull();
    }

    #endregion

    #region aura_refactor - TypeScript Extract Variable

    [Theory]
    [InlineData("src/app.ts")]
    [InlineData("src/component.tsx")]
    public async Task Refactor_ExtractVariable_WithTypeScriptFile_RoutesToTypeScriptService(string filePath)
    {
        // Arrange
        var expectedResult = new TypeScriptRefactoringResult
        {
            Success = true,
            Preview = false,
            ChangedFiles = new List<string> { filePath },
            Description = "Extracted variable 'extractedVar'",
        };

        _typeScriptRefactoringService.ExtractVariableAsync(
            Arg.Any<TypeScriptExtractVariableRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var request = CreateToolCallRequest("aura_refactor", new
        {
            operation = "extract_variable",
            filePath,
            projectPath = "/project",
            startOffset = 50,
            endOffset = 80,
            newName = "extractedVar",
        });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        await _typeScriptRefactoringService.Received(1).ExtractVariableAsync(
            Arg.Is<TypeScriptExtractVariableRequest>(r =>
                r.FilePath == filePath &&
                r.ProjectPath == "/project" &&
                r.StartOffset == 50 &&
                r.EndOffset == 80 &&
                r.NewName == "extractedVar"),
            Arg.Any<CancellationToken>());

        var response = ParseResponse(responseJson);
        response.Error.Should().BeNull();
    }

    #endregion

    #region Error Handling

    [Fact]
    public async Task Navigate_References_WithTypeScriptFile_ServiceError_ReturnsError()
    {
        // Arrange
        var expectedResult = new TypeScriptFindReferencesResult
        {
            Success = false,
            Error = "No symbol found at offset 42",
        };

        _typeScriptRefactoringService.FindReferencesAsync(
            Arg.Any<TypeScriptFindReferencesRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var request = CreateToolCallRequest("aura_navigate", new
        {
            operation = "references",
            filePath = "src/app.ts",
            projectPath = "/project",
            offset = 42,
        });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        var resultText = GetResultText(responseJson);
        resultText.Should().Contain("success");
        resultText.Should().Contain("false");
        resultText.Should().Contain("No symbol found");
    }

    [Fact]
    public async Task Refactor_Rename_WithTypeScriptFile_ServiceError_ReturnsError()
    {
        // Arrange
        var expectedResult = new TypeScriptRefactoringResult
        {
            Success = false,
            Error = "Cannot rename this symbol",
            ErrorType = "NotRenameable",
        };

        _typeScriptRefactoringService.RenameSymbolAsync(
            Arg.Any<TypeScriptRenameRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var request = CreateToolCallRequest("aura_refactor", new
        {
            operation = "rename",
            filePath = "src/app.ts",
            projectPath = "/project",
            offset = 42,
            newName = "newName",
        });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        var resultText = GetResultText(responseJson);
        resultText.Should().Contain("success");
        resultText.Should().Contain("false");
        resultText.Should().Contain("Cannot rename");
    }

    #endregion

    #region Helper Methods

    private static string CreateToolCallRequest(string toolName, object arguments)
    {
        var request = new
        {
            jsonrpc = "2.0",
            method = "tools/call",
            id = 1,
            @params = new
            {
                name = toolName,
                arguments,
            },
        };

        return JsonSerializer.Serialize(request);
    }

    private static JsonRpcResponse ParseResponse(string json)
    {
        return JsonSerializer.Deserialize<JsonRpcResponse>(json)!;
    }

    private static string GetResultText(string responseJson)
    {
        var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        if (root.TryGetProperty("result", out var result) &&
            result.TryGetProperty("content", out var content) &&
            content.GetArrayLength() > 0)
        {
            var firstContent = content[0];
            if (firstContent.TryGetProperty("text", out var text))
            {
                return text.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    #endregion
}
