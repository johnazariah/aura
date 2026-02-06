using System.Text.Json;
using Aura.Api.Mcp.Tools;
using Aura.Api.Services;
using Aura.Foundation.Data.Entities;
using Aura.Foundation.Git;
using Aura.Foundation.Rag;
using Aura.Module.Developer.Data.Entities;
using Aura.Module.Developer.GitHub;
using Aura.Module.Developer.Services;
using Aura.Module.Developer.Services.Testing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.Extensions.Logging;
using RefactoringParameterInfo = Aura.Module.Developer.Services.ParameterInfo;

namespace Aura.Api.Mcp;

public sealed partial class McpHandler
{
    // =========================================================================
    // Python Refactoring Tool Handlers (Phase 6)
    // =========================================================================
    private async Task<object> PythonRenameAsync(JsonElement? args, CancellationToken ct)
    {
        var projectPath = args?.GetProperty("projectPath").GetString() ?? "";
        var filePath = args?.GetProperty("filePath").GetString() ?? "";
        var offset = args?.GetProperty("offset").GetInt32() ?? 0;
        var newName = args?.GetProperty("newName").GetString() ?? "";
        var preview = args.HasValue && args.Value.TryGetProperty("preview", out var prevEl) && prevEl.GetBoolean();
        var result = await _pythonRefactoringService.RenameSymbolAsync(new PythonRenameRequest { ProjectPath = projectPath, FilePath = filePath, Offset = offset, NewName = newName, Preview = preview }, ct);
        return new
        {
            success = result.Success,
            error = result.Error,
            preview = result.Preview,
            changedFiles = result.ChangedFiles,
            description = result.Description,
            fileChanges = result.FileChanges?.Select(fc => new { fc.FilePath, fc.OldContent, fc.NewContent })
        };
    }

    private async Task<object> PythonExtractMethodAsync(JsonElement? args, CancellationToken ct)
    {
        var projectPath = args?.GetProperty("projectPath").GetString() ?? "";
        var filePath = args?.GetProperty("filePath").GetString() ?? "";
        var startOffset = args?.GetProperty("startOffset").GetInt32() ?? 0;
        var endOffset = args?.GetProperty("endOffset").GetInt32() ?? 0;
        var newName = args?.GetProperty("newName").GetString() ?? "";
        var preview = args.HasValue && args.Value.TryGetProperty("preview", out var prevEl) && prevEl.GetBoolean();
        var result = await _pythonRefactoringService.ExtractMethodAsync(new PythonExtractMethodRequest { ProjectPath = projectPath, FilePath = filePath, StartOffset = startOffset, EndOffset = endOffset, NewName = newName, Preview = preview }, ct);
        return new
        {
            success = result.Success,
            error = result.Error,
            preview = result.Preview,
            changedFiles = result.ChangedFiles,
            description = result.Description,
            fileChanges = result.FileChanges?.Select(fc => new { fc.FilePath, fc.OldContent, fc.NewContent })
        };
    }

    private async Task<object> PythonExtractVariableAsync(JsonElement? args, CancellationToken ct)
    {
        var projectPath = args?.GetProperty("projectPath").GetString() ?? "";
        var filePath = args?.GetProperty("filePath").GetString() ?? "";
        var startOffset = args?.GetProperty("startOffset").GetInt32() ?? 0;
        var endOffset = args?.GetProperty("endOffset").GetInt32() ?? 0;
        var newName = args?.GetProperty("newName").GetString() ?? "";
        var preview = args.HasValue && args.Value.TryGetProperty("preview", out var prevEl) && prevEl.GetBoolean();
        var result = await _pythonRefactoringService.ExtractVariableAsync(new PythonExtractVariableRequest { ProjectPath = projectPath, FilePath = filePath, StartOffset = startOffset, EndOffset = endOffset, NewName = newName, Preview = preview }, ct);
        return new
        {
            success = result.Success,
            error = result.Error,
            preview = result.Preview,
            changedFiles = result.ChangedFiles,
            description = result.Description,
            fileChanges = result.FileChanges?.Select(fc => new { fc.FilePath, fc.OldContent, fc.NewContent })
        };
    }

    private async Task<object> PythonFindReferencesAsync(JsonElement? args, CancellationToken ct)
    {
        var projectPath = args?.GetProperty("projectPath").GetString() ?? "";
        var filePath = args?.GetProperty("filePath").GetString() ?? "";
        var offset = args?.GetProperty("offset").GetInt32() ?? 0;
        var result = await _pythonRefactoringService.FindReferencesAsync(new PythonFindReferencesRequest { ProjectPath = projectPath, FilePath = filePath, Offset = offset }, ct);
        return new
        {
            success = result.Success,
            error = result.Error,
            count = result.Count,
            references = result.References.Select(r => new { filePath = r.FilePath, offset = r.Offset, isDefinition = r.IsDefinition, isWrite = r.IsWrite })
        };
    }

    private async Task<object> PythonFindDefinitionAsync(JsonElement? args, CancellationToken ct)
    {
        var projectPath = args?.GetProperty("projectPath").GetString() ?? "";
        var filePath = args?.GetProperty("filePath").GetString() ?? "";
        var offset = args?.GetProperty("offset").GetInt32() ?? 0;
        var result = await _pythonRefactoringService.FindDefinitionAsync(new PythonFindDefinitionRequest { ProjectPath = projectPath, FilePath = filePath, Offset = offset }, ct);
        return new
        {
            success = result.Success,
            error = result.Error,
            found = result.Found,
            filePath = result.FilePath,
            offset = result.Offset,
            line = result.Line,
            message = result.Message
        };
    }

    // =========================================================================
    // TypeScript/JavaScript Refactoring Tool Handlers
    // =========================================================================
    private async Task<object> TypeScriptRenameAsync(JsonElement? args, CancellationToken ct)
    {
        var projectPath = args?.GetProperty("projectPath").GetString() ?? "";
        var filePath = args?.GetProperty("filePath").GetString() ?? "";
        var offset = args?.GetProperty("offset").GetInt32() ?? 0;
        var newName = args?.GetProperty("newName").GetString() ?? "";
        var preview = args.HasValue && args.Value.TryGetProperty("preview", out var prevEl) && prevEl.GetBoolean();
        var result = await _typeScriptService.RenameSymbolAsync(new TypeScriptRenameRequest { ProjectPath = projectPath, FilePath = filePath, Offset = offset, NewName = newName, Preview = preview }, ct);
        return new
        {
            success = result.Success,
            error = result.Error,
            preview = result.Preview,
            changedFiles = result.ChangedFiles,
            description = result.Description
        };
    }

    private async Task<object> TypeScriptExtractFunctionAsync(JsonElement? args, CancellationToken ct)
    {
        var projectPath = args?.GetProperty("projectPath").GetString() ?? "";
        var filePath = args?.GetProperty("filePath").GetString() ?? "";
        var startOffset = args?.GetProperty("startOffset").GetInt32() ?? 0;
        var endOffset = args?.GetProperty("endOffset").GetInt32() ?? 0;
        var newName = args?.GetProperty("newName").GetString() ?? "";
        var preview = args.HasValue && args.Value.TryGetProperty("preview", out var prevEl) && prevEl.GetBoolean();
        var result = await _typeScriptService.ExtractFunctionAsync(new TypeScriptExtractFunctionRequest { ProjectPath = projectPath, FilePath = filePath, StartOffset = startOffset, EndOffset = endOffset, NewName = newName, Preview = preview }, ct);
        return new
        {
            success = result.Success,
            error = result.Error,
            preview = result.Preview,
            changedFiles = result.ChangedFiles,
            description = result.Description
        };
    }

    private async Task<object> TypeScriptExtractVariableAsync(JsonElement? args, CancellationToken ct)
    {
        var projectPath = args?.GetProperty("projectPath").GetString() ?? "";
        var filePath = args?.GetProperty("filePath").GetString() ?? "";
        var startOffset = args?.GetProperty("startOffset").GetInt32() ?? 0;
        var endOffset = args?.GetProperty("endOffset").GetInt32() ?? 0;
        var newName = args?.GetProperty("newName").GetString() ?? "";
        var preview = args.HasValue && args.Value.TryGetProperty("preview", out var prevEl) && prevEl.GetBoolean();
        var result = await _typeScriptService.ExtractVariableAsync(new TypeScriptExtractVariableRequest { ProjectPath = projectPath, FilePath = filePath, StartOffset = startOffset, EndOffset = endOffset, NewName = newName, Preview = preview }, ct);
        return new
        {
            success = result.Success,
            error = result.Error,
            preview = result.Preview,
            changedFiles = result.ChangedFiles,
            description = result.Description
        };
    }

    private async Task<object> TypeScriptFindReferencesAsync(JsonElement? args, CancellationToken ct)
    {
        var projectPath = args?.GetProperty("projectPath").GetString() ?? "";
        var filePath = args?.GetProperty("filePath").GetString() ?? "";
        var offset = args?.GetProperty("offset").GetInt32() ?? 0;
        var result = await _typeScriptService.FindReferencesAsync(new TypeScriptFindReferencesRequest { ProjectPath = projectPath, FilePath = filePath, Offset = offset }, ct);
        return new
        {
            success = result.Success,
            error = result.Error,
            count = result.Count,
            references = result.References?.Select(r => new { filePath = r.File, line = r.Line, column = r.Column, text = r.Text })
        };
    }

    private async Task<object> TypeScriptFindDefinitionAsync(JsonElement? args, CancellationToken ct)
    {
        var projectPath = args?.GetProperty("projectPath").GetString() ?? "";
        var filePath = args?.GetProperty("filePath").GetString() ?? "";
        var offset = args?.GetProperty("offset").GetInt32() ?? 0;
        var result = await _typeScriptService.FindDefinitionAsync(new TypeScriptFindDefinitionRequest { ProjectPath = projectPath, FilePath = filePath, Offset = offset }, ct);
        return new
        {
            success = result.Success,
            error = result.Error,
            found = result.Found,
            filePath = result.FilePath,
            line = result.Line,
            column = result.Column,
            offset = result.Offset,
            message = result.Message
        };
    }
}
