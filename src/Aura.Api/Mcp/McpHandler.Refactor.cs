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
    /// <summary>
    /// aura_refactor - Transform existing code.
    /// Routes to: rename, change_signature, extract_interface, extract_method, extract_variable, safe_delete.
    /// Auto-detects language from filePath extension.
    /// </summary>
    private async Task<object> RefactorAsync(JsonElement? args, CancellationToken ct)
    {
        var operation = args?.GetProperty("operation").GetString() ?? throw new ArgumentException("operation is required");
        var language = DetectLanguageFromArgs(args);

        return operation switch
        {
            "rename" when language == "python" => await PythonRenameAsync(args, ct),
            "rename" when language == "typescript" => await TypeScriptRenameAsync(args, ct),
            "rename" => await RenameSymbolAsync(args, ct),
            "change_signature" => await ChangeSignatureAsync(args, ct),
            "extract_interface" => await ExtractInterfaceFromRefactor(args, ct),
            "extract_method" when language == "python" => await PythonExtractMethodAsync(args, ct),
            "extract_method" when language == "typescript" => await TypeScriptExtractFunctionAsync(args, ct),
            "extract_variable" when language == "python" => await PythonExtractVariableAsync(args, ct),
            "extract_variable" when language == "typescript" => await TypeScriptExtractVariableAsync(args, ct),
            "safe_delete" => await SafeDeleteAsync(args, ct),
            "move_type_to_file" => await MoveTypeToFileAsync(args, ct),
            "move_members_to_partial" => await MoveMembersToPartialAsync(args, ct),
            "extract_method" => throw new NotSupportedException("C# extract_method not yet implemented. Use Python or TypeScript files, or manual extraction."),
            "extract_variable" => throw new NotSupportedException("C# extract_variable not yet implemented. Use Python or TypeScript files, or manual extraction."),
            _ => throw new ArgumentException($"Unknown refactor operation: {operation}")
        };
    }

    private async Task<object> ExtractInterfaceFromRefactor(JsonElement? args, CancellationToken ct)
    {
        // Map from new schema (symbolName as class name, newName as interface name) to old schema
        var className = args?.GetProperty("symbolName").GetString() ?? throw new ArgumentException("symbolName (class name) is required for extract_interface");
        var interfaceName = args?.GetProperty("newName").GetString() ?? throw new ArgumentException("newName (interface name) is required for extract_interface");
        var solutionPath = args?.GetProperty("solutionPath").GetString() ?? throw new ArgumentException("solutionPath is required for extract_interface");
        var preview = false;
        if (args.HasValue && args.Value.TryGetProperty("preview", out var previewEl))
        {
            preview = previewEl.GetBoolean();
        }

        var members = new List<string>();
        if (args.HasValue && args.Value.TryGetProperty("members", out var membersEl))
        {
            foreach (var m in membersEl.EnumerateArray())
            {
                if (m.GetString() is string memberName)
                    members.Add(memberName);
            }
        }

        var result = await _refactoringService.ExtractInterfaceAsync(new ExtractInterfaceRequest { ClassName = className, InterfaceName = interfaceName, SolutionPath = solutionPath, Members = members.Count > 0 ? members : null, Preview = preview }, ct);
        return new
        {
            success = result.Success,
            filesModified = result.ModifiedFiles,
            message = result.Error ?? $"Extracted interface {interfaceName} from {className}",
            preview = preview ? result.Preview : null
        };
    }

    // =========================================================================
    // Refactoring Tool Handlers (Phase 5)
    // =========================================================================
    private async Task<object> RenameSymbolAsync(JsonElement? args, CancellationToken ct)
    {
        var symbolName = args?.GetProperty("symbolName").GetString() ?? "";
        var newName = args?.GetProperty("newName").GetString() ?? "";
        var solutionPath = args?.GetProperty("solutionPath").GetString() ?? "";
        string? containingType = null;
        string? filePath = null;
        var preview = false;
        var validate = false;
        var analyze = true; // Default to analyze mode
        if (args.HasValue)
        {
            if (args.Value.TryGetProperty("containingType", out var ctEl))
                containingType = ctEl.GetString();
            if (args.Value.TryGetProperty("filePath", out var fpEl))
                filePath = fpEl.GetString();
            if (args.Value.TryGetProperty("preview", out var prevEl))
                preview = prevEl.GetBoolean();
            if (args.Value.TryGetProperty("validate", out var valEl))
                validate = valEl.GetBoolean();
            if (args.Value.TryGetProperty("analyze", out var analyzeEl))
                analyze = analyzeEl.GetBoolean();
        }

        // If analyze mode, return blast radius without executing
        if (analyze)
        {
            var blastRadius = await _refactoringService.AnalyzeRenameAsync(new RenameSymbolRequest { SymbolName = symbolName, NewName = newName, SolutionPath = solutionPath, ContainingType = containingType }, ct);
            return new
            {
                operation = blastRadius.Operation,
                symbol = blastRadius.Symbol,
                newName = blastRadius.NewName,
                success = blastRadius.Success,
                error = blastRadius.Error,
                blastRadius = new
                {
                    relatedSymbols = blastRadius.RelatedSymbols.Select(s => new { name = s.Name, kind = s.Kind, filePath = s.FilePath, referenceCount = s.ReferenceCount, suggestedNewName = s.SuggestedNewName }),
                    totalReferences = blastRadius.TotalReferences,
                    filesAffected = blastRadius.FilesAffected,
                    filesToRename = blastRadius.FilesToRename
                },
                suggestedPlan = blastRadius.SuggestedPlan.Select(op => new { order = op.Order, operation = op.Operation, target = op.Target, newValue = op.NewValue, referenceCount = op.ReferenceCount }),
                awaitsConfirmation = blastRadius.AwaitsConfirmation,
                instructions = $"""
                    STEP-BY-STEP EXECUTION REQUIRED:
                    
                    The suggestedPlan contains {blastRadius.SuggestedPlan.Count} operations. Execute them ONE AT A TIME:
                    
                    1. Present this blast radius to the user and wait for confirmation
                    2. For each step in suggestedPlan:
                       a. State which step you're executing (e.g., "Step 1 of {blastRadius.SuggestedPlan.Count}: Renaming X → Y")
                       b. Explain WHY this rename is needed
                       c. Call aura_refactor(operation: "rename", symbolName: "<target>", newName: "<newValue>", analyze: false)
                       d. Run `dotnet build` to verify
                       e. Report result before proceeding to next step
                    3. For 'rename_file' operations, use aura_refactor(operation: "move_type_to_file", symbolName: "<newValue>")
                    4. After all steps, sweep for residuals with grep_search
                    
                    DO NOT execute multiple steps in one tool call. Each rename is a separate operation.
                    """
            };
        }

        var result = await _refactoringService.RenameSymbolAsync(new RenameSymbolRequest { SymbolName = symbolName, NewName = newName, SolutionPath = solutionPath, ContainingType = containingType, FilePath = filePath, Preview = preview, Validate = validate }, ct);
        return new
        {
            success = result.Success,
            message = result.Message,
            modifiedFiles = result.ModifiedFiles,
            preview = result.Preview?.Select(p => new { p.FilePath, p.NewContent }),
            validation = result.Validation is null ? null : new
            {
                buildSucceeded = result.Validation.BuildSucceeded,
                buildOutput = result.Validation.BuildOutput,
                residuals = result.Validation.Residuals
            }
        };
    }

    private async Task<object> ChangeSignatureAsync(JsonElement? args, CancellationToken ct)
    {
        var methodName = args?.GetProperty("methodName").GetString() ?? "";
        var containingType = args?.GetProperty("containingType").GetString() ?? "";
        var solutionPath = args?.GetProperty("solutionPath").GetString() ?? "";
        List<RefactoringParameterInfo>? addParams = null;
        List<string>? removeParams = null;
        var preview = false;
        if (args.HasValue)
        {
            if (args.Value.TryGetProperty("addParameters", out var addEl) && addEl.ValueKind == JsonValueKind.Array)
            {
                addParams = addEl.EnumerateArray().Select(p => new RefactoringParameterInfo(p.GetProperty("name").GetString() ?? "", p.GetProperty("type").GetString() ?? "", p.TryGetProperty("defaultValue", out var dv) ? dv.GetString() : null)).ToList();
            }

            if (args.Value.TryGetProperty("removeParameters", out var remEl) && remEl.ValueKind == JsonValueKind.Array)
            {
                removeParams = remEl.EnumerateArray().Select(p => p.GetString() ?? "").ToList();
            }

            if (args.Value.TryGetProperty("preview", out var prevEl))
                preview = prevEl.GetBoolean();
        }

        var result = await _refactoringService.ChangeMethodSignatureAsync(new ChangeSignatureRequest { MethodName = methodName, ContainingType = containingType, SolutionPath = solutionPath, AddParameters = addParams, RemoveParameters = removeParams, Preview = preview }, ct);
        return new
        {
            success = result.Success,
            message = result.Message,
            modifiedFiles = result.ModifiedFiles,
            preview = result.Preview?.Select(p => new { p.FilePath, p.NewContent })
        };
    }

    private async Task<object> SafeDeleteAsync(JsonElement? args, CancellationToken ct)
    {
        var symbolName = args?.GetProperty("symbolName").GetString() ?? "";
        var solutionPath = args?.GetProperty("solutionPath").GetString() ?? "";
        string? containingType = null;
        var preview = false;
        if (args.HasValue)
        {
            if (args.Value.TryGetProperty("containingType", out var ctEl))
                containingType = ctEl.GetString();
            if (args.Value.TryGetProperty("preview", out var prevEl))
                preview = prevEl.GetBoolean();
        }

        var result = await _refactoringService.SafeDeleteAsync(new SafeDeleteRequest { SymbolName = symbolName, SolutionPath = solutionPath, ContainingType = containingType, Preview = preview }, ct);
        if (!result.Success && result.RemainingReferences?.Count > 0)
        {
            return new
            {
                success = false,
                message = result.Message,
                remainingReferences = result.RemainingReferences.Select(r => new { r.FilePath, r.Line, r.CodeSnippet })
            };
        }

        return new
        {
            success = result.Success,
            message = result.Message,
            modifiedFiles = result.ModifiedFiles
        };
    }

    private async Task<object> MoveTypeToFileAsync(JsonElement? args, CancellationToken ct)
    {
        var typeName = args?.GetProperty("symbolName").GetString() ?? throw new ArgumentException("symbolName (type name) is required for move_type_to_file");
        var solutionPath = args?.GetProperty("solutionPath").GetString() ?? throw new ArgumentException("solutionPath is required for move_type_to_file");
        string? targetDirectory = null;
        string? targetFileName = null;
        var useGitMove = true;
        var preview = false;
        if (args.HasValue)
        {
            if (args.Value.TryGetProperty("targetDirectory", out var tdEl))
                targetDirectory = tdEl.GetString();
            if (args.Value.TryGetProperty("newName", out var tfEl))
                targetFileName = tfEl.GetString();
            if (args.Value.TryGetProperty("useGitMove", out var gmEl))
                useGitMove = gmEl.GetBoolean();
            if (args.Value.TryGetProperty("preview", out var prevEl))
                preview = prevEl.GetBoolean();
        }

        var result = await _refactoringService.MoveTypeToFileAsync(new MoveTypeToFileRequest { TypeName = typeName, SolutionPath = solutionPath, TargetDirectory = targetDirectory, TargetFileName = targetFileName, UseGitMove = useGitMove, Preview = preview }, ct);
        return new
        {
            success = result.Success,
            message = result.Message,
            modifiedFiles = result.ModifiedFiles,
            createdFiles = result.CreatedFiles,
            deletedFiles = result.DeletedFiles,
            preview = result.Preview?.Select(p => new { p.FilePath, p.NewContent })
        };
    }

    private async Task<object> MoveMembersToPartialAsync(JsonElement? args, CancellationToken ct)
    {
        var className = args?.GetProperty("className").GetString() ?? throw new ArgumentException("className is required");
        var solutionPath = args?.GetProperty("solutionPath").GetString() ?? throw new ArgumentException("solutionPath is required");
        var targetFileName = args?.GetProperty("targetFileName").GetString() ?? throw new ArgumentException("targetFileName is required");
        // memberNames can be a single string or an array
        var memberNames = new List<string>();
        if (args.HasValue && args.Value.TryGetProperty("memberNames", out var membersEl))
        {
            if (membersEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var m in membersEl.EnumerateArray())
                {
                    if (m.GetString() is { } name)
                        memberNames.Add(name);
                }
            }
            else if (membersEl.GetString() is { } singleName)
            {
                memberNames.Add(singleName);
            }
        }

        if (memberNames.Count == 0)
        {
            throw new ArgumentException("memberNames is required (array of member names)");
        }

        string? targetDirectory = null;
        if (args.HasValue && args.Value.TryGetProperty("targetDirectory", out var dirEl))
        {
            targetDirectory = dirEl.GetString();
        }

        var preview = args?.TryGetProperty("preview", out var previewEl) == true && previewEl.GetBoolean();
        var ensurePartial = true;
        if (args?.TryGetProperty("ensureSourceIsPartial", out var ensureEl) == true)
        {
            ensurePartial = ensureEl.GetBoolean();
        }

        var result = await _refactoringService.MoveMembersToPartialAsync(new MoveMembersToPartialRequest { ClassName = className, SolutionPath = solutionPath, MemberNames = memberNames, TargetFileName = targetFileName, TargetDirectory = targetDirectory, Preview = preview, EnsureSourceIsPartial = ensurePartial }, ct);
        return new
        {
            success = result.Success,
            message = result.Message,
            modifiedFiles = result.ModifiedFiles,
            createdFiles = result.CreatedFiles,
            preview = result.Preview?.Select(p => new { p.FilePath, p.NewContent })
        };
    }
}
