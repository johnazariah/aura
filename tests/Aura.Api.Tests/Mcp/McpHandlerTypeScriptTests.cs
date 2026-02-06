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
    private readonly ITypeScriptLanguageService _typeScriptService;
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
        _typeScriptService = Substitute.For<ITypeScriptLanguageService>();
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
            _typeScriptService,
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

        _typeScriptService.FindReferencesAsync(
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
        await _typeScriptService.Received(1).FindReferencesAsync(
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
        await _typeScriptService.DidNotReceiveWithAnyArgs()
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

        _typeScriptService.FindDefinitionAsync(
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
        await _typeScriptService.Received(1).FindDefinitionAsync(
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

        await _typeScriptService.DidNotReceiveWithAnyArgs()
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

        await _typeScriptService.DidNotReceiveWithAnyArgs()
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

        _typeScriptService.RenameSymbolAsync(
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
        await _typeScriptService.Received(1).RenameSymbolAsync(
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

        _typeScriptService.ExtractFunctionAsync(
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
        await _typeScriptService.Received(1).ExtractFunctionAsync(
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

        _typeScriptService.ExtractVariableAsync(
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
        await _typeScriptService.Received(1).ExtractVariableAsync(
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

        _typeScriptService.FindReferencesAsync(
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

        _typeScriptService.RenameSymbolAsync(
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

    #region aura_inspect - TypeScript type_members

    [Theory]
    [InlineData("src/app.ts")]
    [InlineData("src/component.tsx")]
    public async Task Inspect_TypeMembers_WithTypeScriptProject_RoutesToTypeScriptService(string filePath)
    {
        // Arrange
        var expectedResult = new TypeScriptInspectTypeResult
        {
            Success = true,
            TypeName = "MyClass",
            Kind = "class",
            FilePath = filePath,
            Line = 5,
            Members = new List<TypeScriptMemberInfo>
            {
                new() { Name = "name", Kind = "property", Type = "string", Visibility = "public", IsStatic = false, IsAsync = false, Line = 6 },
                new() { Name = "doWork", Kind = "method", Type = "void", Visibility = "public", IsStatic = false, IsAsync = true, Line = 8 },
            },
        };

        _typeScriptService.InspectTypeAsync(
            Arg.Any<TypeScriptInspectTypeRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var request = CreateToolCallRequest("aura_inspect", new
        {
            operation = "type_members",
            typeName = "MyClass",
            projectPath = "/project",
            filePath,
        });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        await _typeScriptService.Received(1).InspectTypeAsync(
            Arg.Is<TypeScriptInspectTypeRequest>(r =>
                r.TypeName == "MyClass" &&
                r.ProjectPath == "/project"),
            Arg.Any<CancellationToken>());

        var response = ParseResponse(responseJson);
        response.Error.Should().BeNull();
    }

    [Fact]
    public async Task Inspect_TypeMembers_WithTypeScriptProject_ReturnsMembers()
    {
        // Arrange
        var expectedResult = new TypeScriptInspectTypeResult
        {
            Success = true,
            TypeName = "UserService",
            Kind = "class",
            FilePath = "src/services/user.ts",
            Line = 10,
            Members = new List<TypeScriptMemberInfo>
            {
                new() { Name = "constructor", Kind = "constructor", Visibility = "public", IsStatic = false, IsAsync = false, Line = 11 },
                new() { Name = "getUser", Kind = "method", Type = "Promise<User>", Visibility = "public", IsStatic = false, IsAsync = true, Line = 15 },
                new() { Name = "count", Kind = "property", Type = "number", Visibility = "private", IsStatic = true, IsAsync = false, Line = 9 },
            },
        };

        _typeScriptService.InspectTypeAsync(
            Arg.Any<TypeScriptInspectTypeRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var request = CreateToolCallRequest("aura_inspect", new
        {
            operation = "type_members",
            typeName = "UserService",
            projectPath = "/project",
            filePath = "src/services/user.ts",
        });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        var resultText = GetResultText(responseJson);
        resultText.Should().Contain("UserService");
        resultText.Should().Contain("getUser");
        resultText.Should().Contain("constructor");
    }

    #endregion

    #region aura_inspect - TypeScript list_types

    [Fact]
    public async Task Inspect_ListTypes_WithTypeScriptProject_RoutesToTypeScriptService()
    {
        // Arrange
        var expectedResult = new TypeScriptListTypesResult
        {
            Success = true,
            Count = 2,
            Types = new List<TypeScriptTypeInfo>
            {
                new() { Name = "UserService", Kind = "class", FilePath = "src/services/user.ts", Line = 5, IsExported = true, MemberCount = 4 },
                new() { Name = "IUserRepository", Kind = "interface", FilePath = "src/repos/user-repo.ts", Line = 3, IsExported = true, MemberCount = 3 },
            },
        };

        _typeScriptService.ListTypesAsync(
            Arg.Any<TypeScriptListTypesRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var request = CreateToolCallRequest("aura_inspect", new
        {
            operation = "list_types",
            projectPath = "/project",
            filePath = "src/index.ts",
        });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        await _typeScriptService.Received(1).ListTypesAsync(
            Arg.Is<TypeScriptListTypesRequest>(r =>
                r.ProjectPath == "/project"),
            Arg.Any<CancellationToken>());

        var response = ParseResponse(responseJson);
        response.Error.Should().BeNull();
    }

    [Fact]
    public async Task Inspect_ListTypes_WithNameFilter_PassesFilterToService()
    {
        // Arrange
        var expectedResult = new TypeScriptListTypesResult
        {
            Success = true,
            Count = 1,
            Types = new List<TypeScriptTypeInfo>
            {
                new() { Name = "UserService", Kind = "class", FilePath = "src/services/user.ts", Line = 5, IsExported = true, MemberCount = 4 },
            },
        };

        _typeScriptService.ListTypesAsync(
            Arg.Any<TypeScriptListTypesRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var request = CreateToolCallRequest("aura_inspect", new
        {
            operation = "list_types",
            projectPath = "/project",
            filePath = "src/index.ts",
            nameFilter = "User",
        });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        await _typeScriptService.Received(1).ListTypesAsync(
            Arg.Is<TypeScriptListTypesRequest>(r =>
                r.ProjectPath == "/project" &&
                r.NameFilter == "User"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Inspect_ListTypes_WithTypeScriptProject_ReturnsTypeList()
    {
        // Arrange
        var expectedResult = new TypeScriptListTypesResult
        {
            Success = true,
            Count = 2,
            Types = new List<TypeScriptTypeInfo>
            {
                new() { Name = "AppConfig", Kind = "interface", FilePath = "src/config.ts", Line = 1, IsExported = true, MemberCount = 5 },
                new() { Name = "LogLevel", Kind = "enum", FilePath = "src/logger.ts", Line = 3, IsExported = true, MemberCount = 4 },
            },
        };

        _typeScriptService.ListTypesAsync(
            Arg.Any<TypeScriptListTypesRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var request = CreateToolCallRequest("aura_inspect", new
        {
            operation = "list_types",
            projectPath = "/project",
            filePath = "src/config.ts",
        });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        var resultText = GetResultText(responseJson);
        resultText.Should().Contain("AppConfig");
        resultText.Should().Contain("LogLevel");
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
