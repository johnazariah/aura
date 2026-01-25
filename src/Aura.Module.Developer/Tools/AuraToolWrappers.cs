// <copyright file="AuraToolWrappers.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Tools;

using Aura.Foundation.Tools;
using Aura.Module.Developer.Services;
using Aura.Module.Developer.Services.Testing;
using Microsoft.Extensions.Logging;
using RefactoringParameterInfo = Aura.Module.Developer.Services.ParameterInfo;

#region Input/Output Records

/// <summary>
/// Input for aura.refactor tool.
/// </summary>
public record AuraRefactorInput
{
    /// <summary>Refactoring operation: rename, change_signature, extract_interface, safe_delete, move_type_to_file.</summary>
    public required string Operation { get; init; }

    /// <summary>Path to the solution file (.sln).</summary>
    public required string SolutionPath { get; init; }

    /// <summary>Symbol to refactor (method, type, property name).</summary>
    public required string SymbolName { get; init; }

    /// <summary>New name for rename/extract operations.</summary>
    public string? NewName { get; init; }

    /// <summary>Type containing the symbol (for disambiguation).</summary>
    public string? ContainingType { get; init; }

    /// <summary>If true (default), analyze blast radius without executing. Set to false to execute.</summary>
    public bool Analyze { get; init; } = true;

    /// <summary>If true, return changes without applying (default: false).</summary>
    public bool Preview { get; init; }

    /// <summary>If true, validate build after refactoring (default: false).</summary>
    public bool Validate { get; init; }

    /// <summary>Member names for extract_interface.</summary>
    public string[]? Members { get; init; }

    /// <summary>Parameters to add for change_signature.</summary>
    public ParameterSpec[]? AddParameters { get; init; }

    /// <summary>Parameter names to remove for change_signature.</summary>
    public string[]? RemoveParameters { get; init; }

    /// <summary>Target directory for move_type_to_file.</summary>
    public string? TargetDirectory { get; init; }
}

/// <summary>
/// Parameter specification for change_signature.
/// </summary>
public record ParameterSpec
{
    /// <summary>Parameter name.</summary>
    public required string Name { get; init; }

    /// <summary>Parameter type.</summary>
    public required string Type { get; init; }

    /// <summary>Default value (optional).</summary>
    public string? DefaultValue { get; init; }
}

/// <summary>
/// Output for aura.refactor tool.
/// </summary>
public record AuraRefactorOutput
{
    /// <summary>Whether the operation succeeded.</summary>
    public required bool Success { get; init; }

    /// <summary>Human-readable message.</summary>
    public required string Message { get; init; }

    /// <summary>Files that were modified.</summary>
    public string[] ModifiedFiles { get; init; } = [];

    /// <summary>Files that were created.</summary>
    public string[] CreatedFiles { get; init; } = [];

    /// <summary>Files that were deleted.</summary>
    public string[] DeletedFiles { get; init; } = [];

    /// <summary>Blast radius analysis (when analyze=true).</summary>
    public BlastRadiusInfo? BlastRadius { get; init; }

    /// <summary>Error details if failed.</summary>
    public string? Error { get; init; }
}

/// <summary>
/// Blast radius information for refactoring analysis.
/// </summary>
public record BlastRadiusInfo
{
    /// <summary>Total reference count.</summary>
    public int ReferenceCount { get; init; }

    /// <summary>Number of files affected.</summary>
    public int FilesAffected { get; init; }

    /// <summary>Related symbols discovered by naming convention.</summary>
    public string[] RelatedSymbols { get; init; } = [];

    /// <summary>Suggested execution plan.</summary>
    public string[] SuggestedPlan { get; init; } = [];
}

/// <summary>
/// Input for aura.generate tool.
/// </summary>
public record AuraGenerateInput
{
    /// <summary>Generation operation: tests, create_type, implement_interface, constructor, property, method.</summary>
    public required string Operation { get; init; }

    /// <summary>Path to the solution file (.sln).</summary>
    public required string SolutionPath { get; init; }

    /// <summary>Target for generation (class name, method, namespace).</summary>
    public string? Target { get; init; }

    /// <summary>Class name for operations on existing classes.</summary>
    public string? ClassName { get; init; }

    /// <summary>Interface name to implement.</summary>
    public string? InterfaceName { get; init; }

    /// <summary>Type name for create_type.</summary>
    public string? TypeName { get; init; }

    /// <summary>Type kind: class, interface, record, struct.</summary>
    public string? TypeKind { get; init; }

    /// <summary>Target directory for new type file.</summary>
    public string? TargetDirectory { get; init; }

    /// <summary>Property name for property generation.</summary>
    public string? PropertyName { get; init; }

    /// <summary>Property/field type.</summary>
    public string? PropertyType { get; init; }

    /// <summary>Method name for method generation.</summary>
    public string? MethodName { get; init; }

    /// <summary>Return type for method.</summary>
    public string? ReturnType { get; init; }

    /// <summary>Method body code.</summary>
    public string? Body { get; init; }

    /// <summary>Method parameters.</summary>
    public ParameterSpec[]? Parameters { get; init; }

    /// <summary>Member names for constructor initialization.</summary>
    public string[]? Members { get; init; }

    /// <summary>Access modifier (public, private, etc.).</summary>
    public string? AccessModifier { get; init; }

    /// <summary>If true, analyze only without generating (for tests).</summary>
    public bool AnalyzeOnly { get; init; }

    /// <summary>If true, validate generated code compiles (for tests).</summary>
    public bool ValidateCompilation { get; init; }

    /// <summary>Test framework override: xunit, nunit, mstest.</summary>
    public string? TestFramework { get; init; }

    /// <summary>Focus area for tests: all, happy_path, edge_cases, error_handling.</summary>
    public string? Focus { get; init; }

    /// <summary>Maximum tests to generate (default: 20).</summary>
    public int? MaxTests { get; init; }

    /// <summary>Output directory for test file.</summary>
    public string? OutputDirectory { get; init; }

    /// <summary>Base class to inherit from.</summary>
    public string? BaseClass { get; init; }

    /// <summary>Interfaces to implement.</summary>
    public string[]? Implements { get; init; }

    /// <summary>Whether class is static.</summary>
    public bool IsStatic { get; init; }

    /// <summary>Whether class is abstract.</summary>
    public bool IsAbstract { get; init; }

    /// <summary>Whether class is sealed.</summary>
    public bool IsSealed { get; init; }

    /// <summary>XML documentation summary.</summary>
    public string? Documentation { get; init; }
}

/// <summary>
/// Output for aura.generate tool.
/// </summary>
public record AuraGenerateOutput
{
    /// <summary>Whether the operation succeeded.</summary>
    public required bool Success { get; init; }

    /// <summary>Human-readable message.</summary>
    public required string Message { get; init; }

    /// <summary>Files that were created.</summary>
    public string[] CreatedFiles { get; init; } = [];

    /// <summary>Files that were modified.</summary>
    public string[] ModifiedFiles { get; init; } = [];

    /// <summary>Analysis information (for analyzeOnly).</summary>
    public TestAnalysisInfo? Analysis { get; init; }

    /// <summary>Generated test count.</summary>
    public int? TestsGenerated { get; init; }

    /// <summary>Whether generated code compiles (when validateCompilation=true).</summary>
    public bool? CompilesSuccessfully { get; init; }

    /// <summary>Compilation diagnostics.</summary>
    public string[]? Diagnostics { get; init; }

    /// <summary>Error details if failed.</summary>
    public string? Error { get; init; }
}

/// <summary>
/// Test analysis information.
/// </summary>
public record TestAnalysisInfo
{
    /// <summary>Target symbol analyzed.</summary>
    public required string Target { get; init; }

    /// <summary>Testable methods found.</summary>
    public string[] TestableMethods { get; init; } = [];

    /// <summary>Detected test framework.</summary>
    public string? DetectedFramework { get; init; }

    /// <summary>Detected mock library.</summary>
    public string? DetectedMockLibrary { get; init; }

    /// <summary>Suggested test count.</summary>
    public int SuggestedTestCount { get; init; }
}

/// <summary>
/// Input for aura.validate tool.
/// </summary>
public record AuraValidateInput
{
    /// <summary>Validation operation: compilation, tests.</summary>
    public required string Operation { get; init; }

    /// <summary>Path to solution file (.sln) for compilation.</summary>
    public string? SolutionPath { get; init; }

    /// <summary>Project name for compilation validation.</summary>
    public string? ProjectName { get; init; }

    /// <summary>Path to test project for tests operation.</summary>
    public string? ProjectPath { get; init; }

    /// <summary>Test filter (dotnet test --filter syntax).</summary>
    public string? Filter { get; init; }

    /// <summary>Timeout in seconds for tests (default: 120).</summary>
    public int TimeoutSeconds { get; init; } = 120;
}

/// <summary>
/// Output for aura.validate tool.
/// </summary>
public record AuraValidateOutput
{
    /// <summary>Whether validation passed.</summary>
    public required bool Success { get; init; }

    /// <summary>Human-readable message.</summary>
    public required string Message { get; init; }

    /// <summary>Suggested command to run.</summary>
    public string? SuggestedCommand { get; init; }

    /// <summary>Error details if failed.</summary>
    public string? Error { get; init; }
}

#endregion

#region Tool Implementations

/// <summary>
/// Wrapper tool for aura_refactor MCP operations.
/// Exposes Roslyn refactoring to internal agents.
/// </summary>
public sealed class AuraRefactorTool(
    IRoslynRefactoringService refactoringService,
    ILogger<AuraRefactorTool> logger) : TypedToolBase<AuraRefactorInput, AuraRefactorOutput>
{
    public override string ToolId => "aura.refactor";

    public override string Name => "Aura Refactor";

    public override string Description =>
        "Transform existing code: rename symbols, change signatures, extract interfaces, safe delete, move types. " +
        "Defaults to analyze mode (analyze=true) to show blast radius before executing.";

    public override async Task<ToolResult<AuraRefactorOutput>> ExecuteAsync(
        AuraRefactorInput input,
        CancellationToken ct = default)
    {
        logger.LogDebug("Executing aura.refactor: {Operation} on {Symbol}", input.Operation, input.SymbolName);

        try
        {
            return input.Operation.ToLowerInvariant() switch
            {
                "rename" => await HandleRenameAsync(input, ct),
                "change_signature" => await HandleChangeSignatureAsync(input, ct),
                "extract_interface" => await HandleExtractInterfaceAsync(input, ct),
                "safe_delete" => await HandleSafeDeleteAsync(input, ct),
                "move_type_to_file" => await HandleMoveTypeToFileAsync(input, ct),
                _ => ToolResult<AuraRefactorOutput>.Fail($"Unknown refactor operation: {input.Operation}. " +
                    "Supported: rename, change_signature, extract_interface, safe_delete, move_type_to_file")
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in aura.refactor {Operation}", input.Operation);
            return ToolResult<AuraRefactorOutput>.Fail($"Refactor failed: {ex.Message}");
        }
    }

    private async Task<ToolResult<AuraRefactorOutput>> HandleRenameAsync(AuraRefactorInput input, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(input.NewName))
        {
            return ToolResult<AuraRefactorOutput>.Fail("newName is required for rename operation");
        }

        var request = new RenameSymbolRequest
        {
            SymbolName = input.SymbolName,
            NewName = input.NewName,
            SolutionPath = input.SolutionPath,
            ContainingType = input.ContainingType,
            Preview = input.Preview,
            Validate = input.Validate
        };

        // Analyze mode - just show blast radius
        if (input.Analyze)
        {
            var analysis = await refactoringService.AnalyzeRenameAsync(request, ct);
            return ToolResult<AuraRefactorOutput>.Ok(new AuraRefactorOutput
            {
                Success = true,
                Message = $"Blast radius analysis for renaming '{input.SymbolName}' to '{input.NewName}'. " +
                          $"Found {analysis.TotalReferences} references across {analysis.FilesAffected} files. " +
                          "Set analyze=false to execute the rename.",
                BlastRadius = new BlastRadiusInfo
                {
                    ReferenceCount = analysis.TotalReferences,
                    FilesAffected = analysis.FilesAffected,
                    RelatedSymbols = analysis.RelatedSymbols.Select(s => s.Name).ToArray(),
                    SuggestedPlan = analysis.SuggestedPlan.Select(op => $"{op.Order}. {op.Operation}: {op.Target} -> {op.NewValue}").ToArray()
                }
            });
        }

        // Execute mode
        var result = await refactoringService.RenameSymbolAsync(request, ct);
        return ToolResult<AuraRefactorOutput>.Ok(new AuraRefactorOutput
        {
            Success = result.Success,
            Message = result.Message,
            ModifiedFiles = result.ModifiedFiles.ToArray(),
            Error = result.Error
        });
    }

    private async Task<ToolResult<AuraRefactorOutput>> HandleChangeSignatureAsync(AuraRefactorInput input, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(input.ContainingType))
        {
            return ToolResult<AuraRefactorOutput>.Fail("containingType is required for change_signature operation");
        }

        var request = new ChangeSignatureRequest
        {
            MethodName = input.SymbolName,
            SolutionPath = input.SolutionPath,
            ContainingType = input.ContainingType,
            Preview = input.Preview,
            AddParameters = input.AddParameters?.Select(p => new RefactoringParameterInfo(
                p.Name,
                p.Type,
                p.DefaultValue)).ToList(),
            RemoveParameters = input.RemoveParameters?.ToList()
        };

        var result = await refactoringService.ChangeMethodSignatureAsync(request, ct);
        return ToolResult<AuraRefactorOutput>.Ok(new AuraRefactorOutput
        {
            Success = result.Success,
            Message = result.Message,
            ModifiedFiles = result.ModifiedFiles.ToArray(),
            Error = result.Error
        });
    }

    private async Task<ToolResult<AuraRefactorOutput>> HandleExtractInterfaceAsync(AuraRefactorInput input, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(input.NewName))
        {
            return ToolResult<AuraRefactorOutput>.Fail("newName (interface name) is required for extract_interface operation");
        }

        var request = new ExtractInterfaceRequest
        {
            ClassName = input.SymbolName,
            InterfaceName = input.NewName,
            SolutionPath = input.SolutionPath,
            Members = input.Members?.ToList(),
            Preview = input.Preview
        };

        var result = await refactoringService.ExtractInterfaceAsync(request, ct);
        return ToolResult<AuraRefactorOutput>.Ok(new AuraRefactorOutput
        {
            Success = result.Success,
            Message = result.Message,
            ModifiedFiles = result.ModifiedFiles.ToArray(),
            CreatedFiles = result.CreatedFiles.ToArray(),
            Error = result.Error
        });
    }

    private async Task<ToolResult<AuraRefactorOutput>> HandleSafeDeleteAsync(AuraRefactorInput input, CancellationToken ct)
    {
        var request = new SafeDeleteRequest
        {
            SymbolName = input.SymbolName,
            SolutionPath = input.SolutionPath,
            ContainingType = input.ContainingType
        };

        var result = await refactoringService.SafeDeleteAsync(request, ct);
        return ToolResult<AuraRefactorOutput>.Ok(new AuraRefactorOutput
        {
            Success = result.Success,
            Message = result.Message,
            DeletedFiles = result.DeletedFiles.ToArray(),
            ModifiedFiles = result.ModifiedFiles.ToArray(),
            Error = result.Error
        });
    }

    private async Task<ToolResult<AuraRefactorOutput>> HandleMoveTypeToFileAsync(AuraRefactorInput input, CancellationToken ct)
    {
        var request = new MoveTypeToFileRequest
        {
            TypeName = input.SymbolName,
            SolutionPath = input.SolutionPath,
            TargetDirectory = input.TargetDirectory
        };

        var result = await refactoringService.MoveTypeToFileAsync(request, ct);
        return ToolResult<AuraRefactorOutput>.Ok(new AuraRefactorOutput
        {
            Success = result.Success,
            Message = result.Message,
            CreatedFiles = result.CreatedFiles.ToArray(),
            ModifiedFiles = result.ModifiedFiles.ToArray(),
            DeletedFiles = result.DeletedFiles.ToArray(),
            Error = result.Error
        });
    }
}

/// <summary>
/// Wrapper tool for aura_generate MCP operations.
/// Exposes code generation to internal agents.
/// </summary>
public sealed class AuraGenerateTool(
    ITestGenerationService testGenerationService,
    IRoslynRefactoringService refactoringService,
    ILogger<AuraGenerateTool> logger) : TypedToolBase<AuraGenerateInput, AuraGenerateOutput>
{
    public override string ToolId => "aura.generate";

    public override string Name => "Aura Generate";

    public override string Description =>
        "Generate new code: tests, types, interfaces, constructors, properties, methods. " +
        "Test generation includes framework detection and proper namespace imports.";

    public override async Task<ToolResult<AuraGenerateOutput>> ExecuteAsync(
        AuraGenerateInput input,
        CancellationToken ct = default)
    {
        logger.LogDebug("Executing aura.generate: {Operation}", input.Operation);

        try
        {
            return input.Operation.ToLowerInvariant() switch
            {
                "tests" => await HandleGenerateTestsAsync(input, ct),
                "create_type" => await HandleCreateTypeAsync(input, ct),
                "implement_interface" => await HandleImplementInterfaceAsync(input, ct),
                "constructor" => await HandleGenerateConstructorAsync(input, ct),
                "property" => await HandleAddPropertyAsync(input, ct),
                "method" => await HandleAddMethodAsync(input, ct),
                _ => ToolResult<AuraGenerateOutput>.Fail($"Unknown generate operation: {input.Operation}. " +
                    "Supported: tests, create_type, implement_interface, constructor, property, method")
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in aura.generate {Operation}", input.Operation);
            return ToolResult<AuraGenerateOutput>.Fail($"Generate failed: {ex.Message}");
        }
    }

    private async Task<ToolResult<AuraGenerateOutput>> HandleGenerateTestsAsync(AuraGenerateInput input, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(input.Target))
        {
            return ToolResult<AuraGenerateOutput>.Fail("target is required for tests operation");
        }

        var focus = input.Focus?.ToLowerInvariant() switch
        {
            "happy_path" => TestFocus.HappyPath,
            "edge_cases" => TestFocus.EdgeCases,
            "error_handling" => TestFocus.ErrorHandling,
            _ => TestFocus.All
        };

        var request = new TestGenerationRequest
        {
            Target = input.Target,
            SolutionPath = input.SolutionPath,
            Focus = focus,
            MaxTests = input.MaxTests ?? 20,
            TestFramework = input.TestFramework,
            OutputDirectory = input.OutputDirectory,
            AnalyzeOnly = input.AnalyzeOnly,
            ValidateCompilation = input.ValidateCompilation
        };

        var result = await testGenerationService.GenerateTestsAsync(request, ct);

        var output = new AuraGenerateOutput
        {
            Success = result.Success,
            Message = result.Message,
            Error = result.Error
        };

        if (result.Analysis != null)
        {
            output = output with
            {
                Analysis = new TestAnalysisInfo
                {
                    Target = input.Target,
                    TestableMethods = result.Analysis.TestableMembers.Select(m => m.Name).ToArray(),
                    DetectedFramework = result.Analysis.DetectedFramework,
                    DetectedMockLibrary = result.Analysis.DetectedMockingLibrary,
                    SuggestedTestCount = result.Analysis.SuggestedTestCount
                }
            };
        }

        if (result.Generated != null)
        {
            output = output with
            {
                CreatedFiles = [result.Generated.TestFilePath],
                TestsGenerated = result.Generated.TestsAdded,
                CompilesSuccessfully = result.Generated.CompilesSuccessfully,
                Diagnostics = result.Generated.CompilationDiagnostics?.ToArray()
            };
        }

        return ToolResult<AuraGenerateOutput>.Ok(output);
    }

    private async Task<ToolResult<AuraGenerateOutput>> HandleCreateTypeAsync(AuraGenerateInput input, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(input.TypeName))
        {
            return ToolResult<AuraGenerateOutput>.Fail("typeName is required for create_type operation");
        }

        if (string.IsNullOrEmpty(input.TargetDirectory))
        {
            return ToolResult<AuraGenerateOutput>.Fail("targetDirectory is required for create_type operation");
        }

        var typeKind = input.TypeKind?.ToLowerInvariant() ?? "class";

        var request = new CreateTypeRequest
        {
            TypeName = input.TypeName,
            TypeKind = typeKind,
            SolutionPath = input.SolutionPath,
            TargetDirectory = input.TargetDirectory,
            BaseClass = input.BaseClass,
            Interfaces = input.Implements?.ToList(),
            IsStatic = input.IsStatic,
            IsAbstract = input.IsAbstract,
            IsSealed = input.IsSealed,
            DocumentationSummary = input.Documentation
        };

        var result = await refactoringService.CreateTypeAsync(request, ct);
        return ToolResult<AuraGenerateOutput>.Ok(new AuraGenerateOutput
        {
            Success = result.Success,
            Message = result.Message,
            CreatedFiles = result.CreatedFiles.ToArray(),
            Error = result.Error
        });
    }

    private async Task<ToolResult<AuraGenerateOutput>> HandleImplementInterfaceAsync(AuraGenerateInput input, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(input.ClassName))
        {
            return ToolResult<AuraGenerateOutput>.Fail("className is required for implement_interface operation");
        }

        if (string.IsNullOrEmpty(input.InterfaceName))
        {
            return ToolResult<AuraGenerateOutput>.Fail("interfaceName is required for implement_interface operation");
        }

        var request = new ImplementInterfaceRequest
        {
            ClassName = input.ClassName,
            InterfaceName = input.InterfaceName,
            SolutionPath = input.SolutionPath
        };

        var result = await refactoringService.ImplementInterfaceAsync(request, ct);
        return ToolResult<AuraGenerateOutput>.Ok(new AuraGenerateOutput
        {
            Success = result.Success,
            Message = result.Message,
            ModifiedFiles = result.ModifiedFiles.ToArray(),
            Error = result.Error
        });
    }

    private async Task<ToolResult<AuraGenerateOutput>> HandleGenerateConstructorAsync(AuraGenerateInput input, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(input.ClassName))
        {
            return ToolResult<AuraGenerateOutput>.Fail("className is required for constructor operation");
        }

        var request = new GenerateConstructorRequest
        {
            ClassName = input.ClassName,
            SolutionPath = input.SolutionPath,
            Members = input.Members?.ToList()
        };

        var result = await refactoringService.GenerateConstructorAsync(request, ct);
        return ToolResult<AuraGenerateOutput>.Ok(new AuraGenerateOutput
        {
            Success = result.Success,
            Message = result.Message,
            ModifiedFiles = result.ModifiedFiles.ToArray(),
            Error = result.Error
        });
    }

    private async Task<ToolResult<AuraGenerateOutput>> HandleAddPropertyAsync(AuraGenerateInput input, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(input.ClassName))
        {
            return ToolResult<AuraGenerateOutput>.Fail("className is required for property operation");
        }

        if (string.IsNullOrEmpty(input.PropertyName) || string.IsNullOrEmpty(input.PropertyType))
        {
            return ToolResult<AuraGenerateOutput>.Fail("propertyName and propertyType are required for property operation");
        }

        var request = new AddPropertyRequest
        {
            ClassName = input.ClassName,
            PropertyName = input.PropertyName,
            PropertyType = input.PropertyType,
            SolutionPath = input.SolutionPath,
            AccessModifier = input.AccessModifier ?? "public",
            Documentation = input.Documentation
        };

        var result = await refactoringService.AddPropertyAsync(request, ct);
        return ToolResult<AuraGenerateOutput>.Ok(new AuraGenerateOutput
        {
            Success = result.Success,
            Message = result.Message,
            ModifiedFiles = result.ModifiedFiles.ToArray(),
            Error = result.Error
        });
    }

    private async Task<ToolResult<AuraGenerateOutput>> HandleAddMethodAsync(AuraGenerateInput input, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(input.ClassName))
        {
            return ToolResult<AuraGenerateOutput>.Fail("className is required for method operation");
        }

        if (string.IsNullOrEmpty(input.MethodName))
        {
            return ToolResult<AuraGenerateOutput>.Fail("methodName is required for method operation");
        }

        var request = new AddMethodRequest
        {
            ClassName = input.ClassName,
            MethodName = input.MethodName,
            ReturnType = input.ReturnType ?? "void",
            SolutionPath = input.SolutionPath,
            AccessModifier = input.AccessModifier ?? "public",
            Body = input.Body,
            Parameters = input.Parameters?.Select(p => new RefactoringParameterInfo(
                p.Name,
                p.Type,
                p.DefaultValue)).ToList(),
            Documentation = input.Documentation
        };

        var result = await refactoringService.AddMethodAsync(request, ct);
        return ToolResult<AuraGenerateOutput>.Ok(new AuraGenerateOutput
        {
            Success = result.Success,
            Message = result.Message,
            ModifiedFiles = result.ModifiedFiles.ToArray(),
            Error = result.Error
        });
    }
}

/// <summary>
/// Wrapper tool for aura_validate MCP operations.
/// Guides agents to use appropriate validation commands.
/// </summary>
public sealed class AuraValidateTool(
    ILogger<AuraValidateTool> logger) : TypedToolBase<AuraValidateInput, AuraValidateOutput>
{
    public override string ToolId => "aura.validate";

    public override string Name => "Aura Validate";

    public override string Description =>
        "Validate code: get compilation validation or test execution commands. " +
        "Returns the appropriate command to run for validation.";

    public override Task<ToolResult<AuraValidateOutput>> ExecuteAsync(
        AuraValidateInput input,
        CancellationToken ct = default)
    {
        logger.LogDebug("Executing aura.validate: {Operation}", input.Operation);

        try
        {
            return Task.FromResult(input.Operation.ToLowerInvariant() switch
            {
                "compilation" => HandleCompilation(input),
                "tests" => HandleTests(input),
                _ => ToolResult<AuraValidateOutput>.Fail($"Unknown validate operation: {input.Operation}. " +
                    "Supported: compilation, tests")
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in aura.validate {Operation}", input.Operation);
            return Task.FromResult(ToolResult<AuraValidateOutput>.Fail($"Validate failed: {ex.Message}"));
        }
    }

    private static ToolResult<AuraValidateOutput> HandleCompilation(AuraValidateInput input)
    {
        var command = string.IsNullOrEmpty(input.ProjectName)
            ? $"dotnet build \"{input.SolutionPath}\" -v q"
            : $"dotnet build \"{input.SolutionPath}\" -v q --project {input.ProjectName}";

        return ToolResult<AuraValidateOutput>.Ok(new AuraValidateOutput
        {
            Success = true,
            Message = "Run the following command to validate compilation. Use dotnet.build_until_success or shell.execute to run it.",
            SuggestedCommand = command
        });
    }

    private static ToolResult<AuraValidateOutput> HandleTests(AuraValidateInput input)
    {
        var command = string.IsNullOrEmpty(input.Filter)
            ? $"dotnet test \"{input.ProjectPath ?? input.SolutionPath}\" --no-build"
            : $"dotnet test \"{input.ProjectPath ?? input.SolutionPath}\" --no-build --filter \"{input.Filter}\"";

        return ToolResult<AuraValidateOutput>.Ok(new AuraValidateOutput
        {
            Success = true,
            Message = "Run the following command to execute tests. Use dotnet.run_tests or shell.execute to run it.",
            SuggestedCommand = command
        });
    }
}

#endregion
