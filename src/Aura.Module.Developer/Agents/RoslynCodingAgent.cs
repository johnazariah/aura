// <copyright file="RoslynCodingAgent.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Agents;

using System.IO.Abstractions;
using System.Text;
using System.Text.Json;
using Aura.Foundation.Agents;
using Aura.Foundation.Llm;
using Aura.Foundation.Prompts;
using Aura.Module.Developer.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

/// <summary>
/// Deterministic C# coding agent that extracts operations via LLM
/// and executes them directly through Roslyn services.
/// 
/// Unlike ReAct-based agents, this agent:
/// 1. Uses LLM to extract structured JSON operations
/// 2. Executes each operation directly via IRoslynRefactoringService
/// 3. Never allows LLM to choose file.modify over semantic tools
/// 
/// This ensures 100% Aura tool usage for C# code changes.
/// </summary>
public sealed class RoslynCodingAgent : IAgent
{
    private const double DefaultTemperature = 0.2;
    private const string ExtractionPromptName = "roslyn-coding-extract";

    private readonly IRoslynRefactoringService _refactoringService;
    private readonly IRoslynWorkspaceService _workspaceService;
    private readonly ILlmProviderRegistry _llmRegistry;
    private readonly IPromptRegistry _promptRegistry;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<RoslynCodingAgent> _logger;

    public RoslynCodingAgent(
        IRoslynRefactoringService refactoringService,
        IRoslynWorkspaceService workspaceService,
        ILlmProviderRegistry llmRegistry,
        IPromptRegistry promptRegistry,
        IFileSystem fileSystem,
        ILogger<RoslynCodingAgent> logger)
    {
        _refactoringService = refactoringService;
        _workspaceService = workspaceService;
        _llmRegistry = llmRegistry;
        _promptRegistry = promptRegistry;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string AgentId => "roslyn-coding";

    /// <inheritdoc/>
    public AgentMetadata Metadata { get; } = new(
        Name: "Roslyn Coding Agent",
        Description: "Deterministic C# coding agent that uses Roslyn services directly. " +
                     "Extracts operations via LLM structured output, then executes via Roslyn.",
        Capabilities: ["software-development-csharp", "software-development", "coding"],
        Priority: 10,  // Highest priority for C# coding tasks
        Languages: ["csharp"],
        Provider: null,
        Temperature: DefaultTemperature,
        Tools: [],  // No tools - we call Roslyn directly
        Tags: ["coding", "roslyn", "deterministic", "csharp"]);

    /// <inheritdoc/>
    public async Task<AgentOutput> ExecuteAsync(
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting deterministic Roslyn coding agent for task: {Task}",
            context.Prompt.Length > 100 ? context.Prompt[..100] + "..." : context.Prompt);

        var llmProvider = _llmRegistry.GetDefaultProvider()
            ?? throw new InvalidOperationException("No LLM provider available");

        var toolCalls = new List<ToolCall>();
        var results = new List<OperationResult>();
        var tokensUsed = 0;

        try
        {
            // Phase 1: Extract operations from the task using LLM
            _logger.LogDebug("Phase 1: Extracting operations from task");
            var (operations, extractTokens) = await ExtractOperationsAsync(context, llmProvider, cancellationToken);
            tokensUsed += extractTokens;

            _logger.LogInformation("Extracted {Count} operations to execute", operations.Operations.Count);

            // Phase 2: Execute each operation via Roslyn
            _logger.LogDebug("Phase 2: Executing operations via Roslyn");
            foreach (var op in operations.Operations)
            {
                var result = await ExecuteOperationAsync(op, context.WorkspacePath, cancellationToken);
                results.Add(result);

                // Track as tool call for compatibility with existing infrastructure
                toolCalls.Add(new ToolCall(
                    $"roslyn.{op.Operation}",
                    JsonSerializer.Serialize(op),
                    result.Success ? result.Message : $"Error: {result.Error}"));

                if (!result.Success)
                {
                    _logger.LogWarning("Operation {Operation} failed: {Error}", op.Operation, result.Error);
                }
            }

            // Phase 3: Validate compilation
            _logger.LogDebug("Phase 3: Validating compilation");
            var validationResult = await ValidateCompilationAsync(context.WorkspacePath, cancellationToken);
            if (!validationResult.Success)
            {
                results.Add(validationResult);
            }

            // Build output
            return BuildOutput(operations, results, toolCalls, tokensUsed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Roslyn coding agent failed");
            return new AgentOutput(
                Content: $"## Task Failed\n\nAn error occurred: {ex.Message}",
                TokensUsed: tokensUsed,
                ToolCalls: toolCalls,
                Artifacts: new Dictionary<string, string>
                {
                    ["success"] = "false",
                    ["error"] = ex.Message
                });
        }
    }

    private async Task<(CSharpOperationsDto Operations, int TokensUsed)> ExtractOperationsAsync(
        AgentContext context,
        ILlmProvider llmProvider,
        CancellationToken ct)
    {
        // Pre-fetch: Try to identify class names in the task and gather their structure
        var classContext = await GatherClassContextAsync(context.Prompt, context.WorkspacePath, ct);

        // If RAG context is empty, gather code patterns from similar types via Roslyn
        string? codePatterns = null;
        if (string.IsNullOrEmpty(context.RagContext))
        {
            codePatterns = await GatherCodePatternsAsync(context.Prompt, context.WorkspacePath, ct);
        }

        var prompt = BuildExtractionPrompt(context, classContext, codePatterns);

        var response = await llmProvider.ChatAsync(
            null, // use default model
            [new ChatMessage(ChatRole.User, prompt)],
            DefaultTemperature,
            ct);

        var tokensUsed = response.TokensUsed;

        // Parse the response
        try
        {
            var json = response.Content.Trim();

            // Try to extract JSON from the response
            json = ExtractJsonFromResponse(json);

            var operations = JsonSerializer.Deserialize<CSharpOperationsDto>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return (operations ?? new CSharpOperationsDto
            {
                Operations = [],
                Summary = "No operations extracted"
            }, tokensUsed);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse LLM response as operations");
            _logger.LogDebug("Raw LLM response: {Response}", response.Content[..Math.Min(500, response.Content.Length)]);
            return (new CSharpOperationsDto
            {
                Operations = [],
                Summary = "Failed to parse operations"
            }, tokensUsed);
        }
    }

    /// <summary>
    /// Extracts JSON from an LLM response that may contain markdown or text around it.
    /// </summary>
    private static string ExtractJsonFromResponse(string response)
    {
        // If response starts with '{', assume it's pure JSON
        if (response.StartsWith('{'))
        {
            return response;
        }

        // Try to extract JSON from markdown code blocks
        if (response.Contains("```"))
        {
            // Look for ```json or ``` followed by {
            var jsonBlockStart = response.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
            if (jsonBlockStart >= 0)
            {
                jsonBlockStart = response.IndexOf('{', jsonBlockStart);
            }
            else
            {
                // Look for first { after any ```
                var codeBlockStart = response.IndexOf("```");
                if (codeBlockStart >= 0)
                {
                    jsonBlockStart = response.IndexOf('{', codeBlockStart);
                }
            }

            if (jsonBlockStart >= 0)
            {
                var jsonBlockEnd = response.LastIndexOf('}');
                if (jsonBlockEnd > jsonBlockStart)
                {
                    return response[jsonBlockStart..(jsonBlockEnd + 1)];
                }
            }
        }

        // Last resort: find the first { and last }
        var startIdx = response.IndexOf('{');
        var endIdx = response.LastIndexOf('}');
        if (startIdx >= 0 && endIdx > startIdx)
        {
            return response[startIdx..(endIdx + 1)];
        }

        // Return as-is if no JSON found
        return response;
    }

    private string BuildExtractionPrompt(AgentContext context, string? classContext = null, string? codePatterns = null)
    {
        // Use the prompt template from the registry
        var templateContext = new
        {
            prompt = context.Prompt,
            classContext,
            ragContext = context.RagContext,
            codePatterns,
        };

        return _promptRegistry.Render(ExtractionPromptName, templateContext);
    }

    private async Task<OperationResult> ExecuteOperationAsync(
        CSharpOperationDto op,
        string? workspacePath,
        CancellationToken ct)
    {
        var solutionPath = FindSolutionPath(workspacePath);

        return op.Operation.ToLowerInvariant() switch
        {
            "read_file" => await ExecuteReadFileAsync(op, workspacePath),
            "add_method" => await ExecuteAddMethodAsync(op, solutionPath, ct),
            "add_property" => await ExecuteAddPropertyAsync(op, solutionPath, ct),
            "create_type" => await ExecuteCreateTypeAsync(op, solutionPath, ct),
            "implement_interface" => await ExecuteImplementInterfaceAsync(op, solutionPath, ct),
            "generate_constructor" => await ExecuteGenerateConstructorAsync(op, solutionPath, ct),
            "generate_tests" => await ExecuteGenerateTestsAsync(op, solutionPath, ct),
            "rename" => await ExecuteRenameAsync(op, solutionPath, ct),
            "change_signature" => await ExecuteChangeSignatureAsync(op, solutionPath, ct),
            _ => OperationResult.Failed(op.Operation, $"Unknown operation: {op.Operation}")
        };
    }

    private Task<OperationResult> ExecuteReadFileAsync(CSharpOperationDto op, string? workspacePath)
    {
        if (string.IsNullOrEmpty(op.FilePath))
        {
            return Task.FromResult(OperationResult.Failed("read_file", "filePath is required"));
        }

        var fullPath = workspacePath != null
            ? Path.Combine(workspacePath, op.FilePath)
            : op.FilePath;

        if (!_fileSystem.File.Exists(fullPath))
        {
            return Task.FromResult(OperationResult.Failed("read_file", $"File not found: {op.FilePath}"));
        }

        var content = _fileSystem.File.ReadAllText(fullPath);
        return Task.FromResult(OperationResult.Succeeded("read_file", $"Read {op.FilePath}", content));
    }

    private async Task<OperationResult> ExecuteAddMethodAsync(
        CSharpOperationDto op,
        string? solutionPath,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(op.ClassName) || string.IsNullOrEmpty(op.MethodName))
        {
            return OperationResult.Failed("add_method", "className and methodName are required");
        }

        if (string.IsNullOrEmpty(solutionPath))
        {
            return OperationResult.Failed("add_method", "No solution file found");
        }

        try
        {
            // Explicit isAsync takes precedence; otherwise auto-detect from return type or body
            var returnType = op.ReturnType ?? "void";
            var isAsync = op.IsAsync ?? (
                returnType.Contains("Task", StringComparison.Ordinal) ||
                (op.Body?.Contains("await ", StringComparison.Ordinal) ?? false));

            var request = new AddMethodRequest
            {
                ClassName = op.ClassName,
                MethodName = op.MethodName,
                ReturnType = returnType,
                Body = op.Body,
                SolutionPath = solutionPath,
                AccessModifier = op.AccessModifier ?? "public",
                Documentation = op.Documentation,
                Parameters = ParseParameters(op.Parameters),
                IsAsync = isAsync,
                IsStatic = op.IsStatic,
                MethodModifier = op.MethodModifier,
                IsExtension = op.IsExtension,
                TypeParameters = ConvertTypeParameters(op.TypeParameters),
                Attributes = ConvertAttributes(op.Attributes),
            };

            var result = await _refactoringService.AddMethodAsync(request, ct);
            return result.Success
                ? OperationResult.Succeeded("add_method", $"Added method {op.MethodName} to {op.ClassName}")
                : OperationResult.Failed("add_method", result.Error ?? "Unknown error");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add method");
            return OperationResult.Failed("add_method", $"Failed to add method: {ex.Message}");
        }
    }

    private async Task<OperationResult> ExecuteAddPropertyAsync(
        CSharpOperationDto op,
        string? solutionPath,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(op.ClassName) || string.IsNullOrEmpty(op.PropertyName))
        {
            return OperationResult.Failed("add_property", "className and propertyName are required");
        }

        if (string.IsNullOrEmpty(solutionPath))
        {
            return OperationResult.Failed("add_property", "No solution file found");
        }

        try
        {
            var request = new AddPropertyRequest
            {
                ClassName = op.ClassName,
                PropertyName = op.PropertyName,
                PropertyType = op.PropertyType ?? "string",
                SolutionPath = solutionPath,
                AccessModifier = op.AccessModifier ?? "public",
                Documentation = op.Documentation,
                IsField = op.IsField,
                IsStatic = op.IsStatic,
                InitialValue = op.InitialValue,
                HasInit = op.HasInit,
                IsRequired = op.IsRequired,
                IsReadonly = op.IsReadonly,
            };

            var result = await _refactoringService.AddPropertyAsync(request, ct);
            return result.Success
                ? OperationResult.Succeeded("add_property", $"Added property {op.PropertyName} to {op.ClassName}")
                : OperationResult.Failed("add_property", result.Error ?? "Unknown error");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add property");
            return OperationResult.Failed("add_property", $"Failed to add property: {ex.Message}");
        }
    }

    private async Task<OperationResult> ExecuteCreateTypeAsync(
        CSharpOperationDto op,
        string? solutionPath,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(op.ClassName))
        {
            return OperationResult.Failed("create_type", "className is required");
        }

        if (string.IsNullOrEmpty(solutionPath))
        {
            return OperationResult.Failed("create_type", "No solution file found");
        }

        try
        {
            var solutionDir = Path.GetDirectoryName(solutionPath)!;

            // Determine target directory from file path or use solution directory
            string targetDir;
            if (!string.IsNullOrEmpty(op.FilePath))
            {
                // Get the directory part of the file path
                var dirPart = Path.GetDirectoryName(op.FilePath) ?? op.FilePath;

                // If it's a relative path, resolve against solution directory
                targetDir = Path.IsPathRooted(dirPart)
                    ? dirPart
                    : Path.GetFullPath(Path.Combine(solutionDir, dirPart));
            }
            else
            {
                targetDir = solutionDir;
            }

            // Create directory if it doesn't exist
            if (!Directory.Exists(targetDir))
            {
                _logger.LogInformation("Creating directory: {Directory}", targetDir);
                Directory.CreateDirectory(targetDir);
            }

            var request = new CreateTypeRequest
            {
                TypeName = op.ClassName,
                TypeKind = op.TypeKind ?? "class",
                Namespace = op.Namespace,
                TargetDirectory = targetDir,
                SolutionPath = solutionPath,
                AdditionalUsings = op.AdditionalUsings,
                BaseClass = op.BaseClass,
                Interfaces = op.Interfaces,
                Attributes = ConvertAttributes(op.Attributes),
                IsStatic = op.IsStatic,
                EnumMembers = op.EnumMembers,
            };

            var result = await _refactoringService.CreateTypeAsync(request, ct);
            return result.Success
                ? OperationResult.Succeeded("create_type", $"Created {op.TypeKind ?? "class"} {op.ClassName}")
                : OperationResult.Failed("create_type", result.Error ?? "Unknown error");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create type");
            return OperationResult.Failed("create_type", $"Failed to create type: {ex.Message}");
        }
    }

    private async Task<OperationResult> ExecuteImplementInterfaceAsync(
        CSharpOperationDto op,
        string? solutionPath,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(op.ClassName) || string.IsNullOrEmpty(op.InterfaceName))
        {
            return OperationResult.Failed("implement_interface", "className and interfaceName are required");
        }

        if (string.IsNullOrEmpty(solutionPath))
        {
            return OperationResult.Failed("implement_interface", "No solution file found");
        }

        try
        {
            var request = new ImplementInterfaceRequest
            {
                ClassName = op.ClassName,
                InterfaceName = op.InterfaceName,
                SolutionPath = solutionPath,
            };

            var result = await _refactoringService.ImplementInterfaceAsync(request, ct);
            return result.Success
                ? OperationResult.Succeeded("implement_interface", $"Implemented {op.InterfaceName} on {op.ClassName}")
                : OperationResult.Failed("implement_interface", result.Error ?? "Unknown error");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to implement interface");
            return OperationResult.Failed("implement_interface", $"Failed to implement interface: {ex.Message}");
        }
    }

    private async Task<OperationResult> ExecuteGenerateConstructorAsync(
        CSharpOperationDto op,
        string? solutionPath,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(op.ClassName))
        {
            return OperationResult.Failed("generate_constructor", "className is required");
        }

        if (string.IsNullOrEmpty(solutionPath))
        {
            return OperationResult.Failed("generate_constructor", "No solution file found");
        }

        try
        {
            var request = new GenerateConstructorRequest
            {
                ClassName = op.ClassName,
                SolutionPath = solutionPath,
            };

            var result = await _refactoringService.GenerateConstructorAsync(request, ct);
            return result.Success
                ? OperationResult.Succeeded("generate_constructor", $"Generated constructor for {op.ClassName}")
                : OperationResult.Failed("generate_constructor", result.Error ?? "Unknown error");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate constructor");
            return OperationResult.Failed("generate_constructor", $"Failed to generate constructor: {ex.Message}");
        }
    }

    private Task<OperationResult> ExecuteGenerateTestsAsync(
        CSharpOperationDto op,
        string? solutionPath,
        CancellationToken ct)
    {
        // Test generation is more complex - for now, return a placeholder
        return Task.FromResult(OperationResult.Failed(
            "generate_tests",
            "Test generation not yet integrated. Use aura.generate(operation: 'tests') via MCP."));
    }

    private async Task<OperationResult> ExecuteRenameAsync(
        CSharpOperationDto op,
        string? solutionPath,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(op.OldName) || string.IsNullOrEmpty(op.NewName))
        {
            return OperationResult.Failed("rename", "oldName and newName are required");
        }

        if (string.IsNullOrEmpty(solutionPath))
        {
            return OperationResult.Failed("rename", "No solution file found");
        }

        try
        {
            var request = new RenameSymbolRequest
            {
                SymbolName = op.OldName,
                NewName = op.NewName,
                SolutionPath = solutionPath,
            };

            var result = await _refactoringService.RenameSymbolAsync(request, ct);
            return result.Success
                ? OperationResult.Succeeded("rename", $"Renamed {op.OldName} to {op.NewName}")
                : OperationResult.Failed("rename", result.Error ?? "Unknown error");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rename symbol");
            return OperationResult.Failed("rename", $"Failed to rename: {ex.Message}");
        }
    }

    private async Task<OperationResult> ExecuteChangeSignatureAsync(
        CSharpOperationDto op,
        string? solutionPath,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(op.MethodName) || string.IsNullOrEmpty(op.ClassName))
        {
            return OperationResult.Failed("change_signature", "methodName and className are required");
        }

        if (string.IsNullOrEmpty(solutionPath))
        {
            return OperationResult.Failed("change_signature", "No solution file found");
        }

        try
        {
            var request = new ChangeSignatureRequest
            {
                MethodName = op.MethodName,
                ContainingType = op.ClassName,
                SolutionPath = solutionPath,
                AddParameters = op.AddParameters?.Select(p => new ParameterInfo(p.Name, p.Type, p.DefaultValue)).ToList(),
                RemoveParameters = op.RemoveParameters?.ToList(),
            };

            _logger.LogInformation("Changing signature of {Type}.{Method}: adding {AddCount} params, removing {RemoveCount} params",
                op.ClassName, op.MethodName,
                op.AddParameters?.Count ?? 0,
                op.RemoveParameters?.Count ?? 0);

            var result = await _refactoringService.ChangeMethodSignatureAsync(request, ct);
            if (result.Success)
            {
                return OperationResult.Succeeded("change_signature",
                    $"Changed signature of {op.ClassName}.{op.MethodName}. Modified {result.ModifiedFiles.Count} files.");
            }

            return OperationResult.Failed("change_signature", result.Error ?? "Unknown error");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to change signature");
            return OperationResult.Failed("change_signature", $"Failed to change signature: {ex.Message}");
        }
    }

    private async Task<OperationResult> ValidateCompilationAsync(string? workspacePath, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(workspacePath))
        {
            return OperationResult.Succeeded("validate", "No workspace to validate");
        }

        var solutionPath = FindSolutionPath(workspacePath);
        if (string.IsNullOrEmpty(solutionPath))
        {
            return OperationResult.Succeeded("validate", "No solution to validate");
        }

        try
        {
            // Clear workspace cache to pick up file changes
            _workspaceService.ClearCache();

            var solution = await _workspaceService.GetSolutionAsync(solutionPath, ct);
            var diagnostics = new List<string>();

            foreach (var project in solution.Projects)
            {
                var compilation = await project.GetCompilationAsync(ct);
                if (compilation == null) continue;

                var errors = compilation.GetDiagnostics()
                    .Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                    .ToList();

                foreach (var error in errors.Take(5))
                {
                    diagnostics.Add($"{error.Id}: {error.GetMessage()}");
                }
            }

            if (diagnostics.Count > 0)
            {
                return OperationResult.Failed("validate", $"Compilation errors:\n{string.Join("\n", diagnostics)}");
            }

            return OperationResult.Succeeded("validate", "Compilation successful");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to validate compilation");
            return OperationResult.Failed("validate", $"Validation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts class names from the task and gathers their structure from Roslyn.
    /// This provides the LLM with accurate field/property/method names.
    /// </summary>
    private async Task<string?> GatherClassContextAsync(string prompt, string? workspacePath, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(workspacePath))
        {
            return null;
        }

        var solutionPath = FindSolutionPath(workspacePath);
        if (string.IsNullOrEmpty(solutionPath))
        {
            return null;
        }

        try
        {
            // Extract potential class names from the prompt using simple heuristics
            // Look for PascalCase words that might be class names
            var classNames = ExtractPotentialClassNames(prompt);
            if (classNames.Count == 0)
            {
                return null;
            }

            var solution = await _workspaceService.GetSolutionAsync(solutionPath, ct);
            var contextBuilder = new StringBuilder();

            foreach (var className in classNames.Take(3)) // Limit to 3 classes to avoid token bloat
            {
                var classSymbol = await FindTypeAsync(solution, className, ct);
                if (classSymbol == null)
                {
                    continue;
                }

                contextBuilder.AppendLine($"### {classSymbol.Name}");
                contextBuilder.AppendLine($"Namespace: {classSymbol.ContainingNamespace}");
                contextBuilder.AppendLine();

                // List fields
                var fields = classSymbol.GetMembers()
                    .OfType<IFieldSymbol>()
                    .Where(f => !f.IsImplicitlyDeclared)
                    .ToList();
                if (fields.Count > 0)
                {
                    contextBuilder.AppendLine("**Fields:**");
                    foreach (var field in fields)
                    {
                        contextBuilder.AppendLine($"- `{field.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} {field.Name}`");
                    }
                    contextBuilder.AppendLine();
                }

                // List properties
                var properties = classSymbol.GetMembers()
                    .OfType<IPropertySymbol>()
                    .Where(p => !p.IsImplicitlyDeclared)
                    .ToList();
                if (properties.Count > 0)
                {
                    contextBuilder.AppendLine("**Properties:**");
                    foreach (var prop in properties)
                    {
                        contextBuilder.AppendLine($"- `{prop.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} {prop.Name}`");
                    }
                    contextBuilder.AppendLine();
                }

                // List methods
                var methods = classSymbol.GetMembers()
                    .OfType<IMethodSymbol>()
                    .Where(m => m.MethodKind == MethodKind.Ordinary && !m.IsImplicitlyDeclared)
                    .ToList();
                if (methods.Count > 0)
                {
                    contextBuilder.AppendLine("**Methods:**");
                    foreach (var method in methods)
                    {
                        var paramList = string.Join(", ", method.Parameters.Select(p =>
                            $"{p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} {p.Name}"));
                        contextBuilder.AppendLine($"- `{method.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} {method.Name}({paramList})`");
                    }
                    contextBuilder.AppendLine();
                }
            }

            var result = contextBuilder.ToString().Trim();
            if (string.IsNullOrEmpty(result))
            {
                return null;
            }

            _logger.LogDebug("Gathered class context for {Count} classes: {ClassNames}",
                classNames.Count, string.Join(", ", classNames));
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to gather class context (non-fatal)");
            return null;
        }
    }

    /// <summary>
    /// Gathers actual source code patterns from similar existing types via Roslyn.
    /// When creating new types (e.g., a Model), this finds existing types in the same
    /// namespace/folder to provide examples of patterns like nullable handling.
    /// </summary>
    private async Task<string?> GatherCodePatternsAsync(string prompt, string? workspacePath, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(workspacePath))
        {
            return null;
        }

        var solutionPath = FindSolutionPath(workspacePath);
        if (string.IsNullOrEmpty(solutionPath))
        {
            return null;
        }

        try
        {
            var solution = await _workspaceService.GetSolutionAsync(solutionPath, ct);
            var patternBuilder = new StringBuilder();

            // Detect if we're creating a Model/DTO type
            var isCreatingModel = prompt.Contains("Model", StringComparison.OrdinalIgnoreCase) ||
                                  prompt.Contains("DTO", StringComparison.OrdinalIgnoreCase) ||
                                  prompt.Contains("Entity", StringComparison.OrdinalIgnoreCase) ||
                                  prompt.Contains("src/Models", StringComparison.OrdinalIgnoreCase);

            if (isCreatingModel)
            {
                // Find existing model types to use as patterns
                var modelTypes = await FindTypesInNamespaceContainingAsync(solution, "Models", ct);
                if (modelTypes.Count > 0)
                {
                    patternBuilder.AppendLine("### Existing Model Pattern (follow this style)");
                    patternBuilder.AppendLine();

                    // Get source code of the first model found (limit to avoid token bloat)
                    var firstModel = modelTypes.First();
                    var sourceText = await GetTypeSourceCodeAsync(firstModel, ct);
                    if (!string.IsNullOrEmpty(sourceText))
                    {
                        patternBuilder.AppendLine("```csharp");
                        patternBuilder.AppendLine(sourceText);
                        patternBuilder.AppendLine("```");
                        patternBuilder.AppendLine();
                        patternBuilder.AppendLine("**IMPORTANT**: Note how string properties use `= string.Empty;` to satisfy nullable reference types.");
                    }
                }
            }

            // Detect if we're creating a Service type
            var isCreatingService = prompt.Contains("Service", StringComparison.OrdinalIgnoreCase) ||
                                    prompt.Contains("src/Services", StringComparison.OrdinalIgnoreCase);

            if (isCreatingService && !isCreatingModel)
            {
                var serviceTypes = await FindTypesInNamespaceContainingAsync(solution, "Services", ct);
                if (serviceTypes.Count > 0)
                {
                    patternBuilder.AppendLine("### Existing Service Pattern (follow this style)");
                    patternBuilder.AppendLine();

                    var firstService = serviceTypes.First();
                    var sourceText = await GetTypeSourceCodeAsync(firstService, ct);
                    if (!string.IsNullOrEmpty(sourceText))
                    {
                        patternBuilder.AppendLine("```csharp");
                        patternBuilder.AppendLine(sourceText);
                        patternBuilder.AppendLine("```");
                    }
                }
            }

            // Detect if we're creating a Controller type
            var isCreatingController = prompt.Contains("Controller", StringComparison.OrdinalIgnoreCase) ||
                                       prompt.Contains("src/Controllers", StringComparison.OrdinalIgnoreCase) ||
                                       prompt.Contains("API endpoint", StringComparison.OrdinalIgnoreCase);

            if (isCreatingController)
            {
                var controllerTypes = await FindTypesInNamespaceContainingAsync(solution, "Controllers", ct);
                if (controllerTypes.Count > 0)
                {
                    patternBuilder.AppendLine("### Existing Controller Pattern (follow this style EXACTLY)");
                    patternBuilder.AppendLine();

                    var firstController = controllerTypes.First();
                    var sourceText = await GetTypeSourceCodeAsync(firstController, ct);
                    if (!string.IsNullOrEmpty(sourceText))
                    {
                        patternBuilder.AppendLine("```csharp");
                        patternBuilder.AppendLine(sourceText);
                        patternBuilder.AppendLine("```");
                        patternBuilder.AppendLine();
                        patternBuilder.AppendLine("**CRITICAL**: Controllers MUST:");
                        patternBuilder.AppendLine("- Inherit from `ControllerBase`");
                        patternBuilder.AppendLine("- Have `[ApiController]` and `[Route(\"api/[controller]\")]` attributes");
                        patternBuilder.AppendLine("- Use `async Task<ActionResult<T>>` for methods with `await`");
                        patternBuilder.AppendLine("- Use constructor injection for dependencies");
                    }
                }
            }

            // Detect if we're creating a Repository type
            var isCreatingRepository = prompt.Contains("Repository", StringComparison.OrdinalIgnoreCase) ||
                                       prompt.Contains("src/Repositories", StringComparison.OrdinalIgnoreCase);

            if (isCreatingRepository && !isCreatingService && !isCreatingController)
            {
                var repoTypes = await FindTypesInNamespaceContainingAsync(solution, "Repositories", ct);
                if (repoTypes.Count > 0)
                {
                    patternBuilder.AppendLine("### Existing Repository Pattern (follow this style)");
                    patternBuilder.AppendLine();

                    var firstRepo = repoTypes.First();
                    var sourceText = await GetTypeSourceCodeAsync(firstRepo, ct);
                    if (!string.IsNullOrEmpty(sourceText))
                    {
                        patternBuilder.AppendLine("```csharp");
                        patternBuilder.AppendLine(sourceText);
                        patternBuilder.AppendLine("```");
                    }
                }
            }

            var result = patternBuilder.ToString().Trim();
            if (string.IsNullOrEmpty(result))
            {
                return null;
            }

            _logger.LogDebug("Gathered code patterns: {Length} chars", result.Length);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to gather code patterns (non-fatal)");
            return null;
        }
    }

    /// <summary>
    /// Finds types whose namespace contains the given substring.
    /// </summary>
    private static async Task<List<INamedTypeSymbol>> FindTypesInNamespaceContainingAsync(
        Solution solution,
        string namespaceSubstring,
        CancellationToken ct)
    {
        var results = new List<INamedTypeSymbol>();

        foreach (var project in solution.Projects)
        {
            ct.ThrowIfCancellationRequested();
            var compilation = await project.GetCompilationAsync(ct);
            if (compilation == null)
            {
                continue;
            }

            foreach (var syntaxTree in compilation.SyntaxTrees)
            {
                var semanticModel = compilation.GetSemanticModel(syntaxTree);
                var root = await syntaxTree.GetRootAsync(ct);

                foreach (var typeDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
                {
                    if (semanticModel.GetDeclaredSymbol(typeDecl) is INamedTypeSymbol typeSymbol &&
                        typeSymbol.ContainingNamespace.ToDisplayString().Contains(namespaceSubstring, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(typeSymbol);
                        if (results.Count >= 3)
                        {
                            return results; // Limit to 3 examples
                        }
                    }
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Gets the source code of a type declaration.
    /// </summary>
    private static async Task<string?> GetTypeSourceCodeAsync(INamedTypeSymbol typeSymbol, CancellationToken ct)
    {
        var syntaxRefs = typeSymbol.DeclaringSyntaxReferences;
        if (syntaxRefs.IsEmpty)
        {
            return null;
        }

        var syntaxNode = await syntaxRefs[0].GetSyntaxAsync(ct);
        return syntaxNode.ToFullString().Trim();
    }

    /// <summary>
    /// Extracts potential class names from a prompt using simple heuristics.
    /// </summary>
    private static List<string> ExtractPotentialClassNames(string prompt)
    {
        var classNames = new List<string>();

        // Match PascalCase words that might be class names
        // Patterns: "UserService", "IUserRepository", etc.
        var regex = new System.Text.RegularExpressions.Regex(@"\b([A-Z][a-z]+(?:[A-Z][a-z]*)+)\b");
        var matches = regex.Matches(prompt);

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var name = match.Groups[1].Value;
            // Skip common words that aren't class names
            if (name is "GetUser" or "AddMethod" or "CreateNew" or "DeleteAll")
            {
                continue;
            }
            if (!classNames.Contains(name))
            {
                classNames.Add(name);
            }
        }

        return classNames;
    }

    /// <summary>
    /// Finds a type by name in the solution.
    /// </summary>
    private static async Task<INamedTypeSymbol?> FindTypeAsync(Solution solution, string typeName, CancellationToken ct)
    {
        foreach (var project in solution.Projects)
        {
            ct.ThrowIfCancellationRequested();
            var compilation = await project.GetCompilationAsync(ct);
            if (compilation == null) continue;

            // Try exact match first
            var symbol = compilation.GetTypeByMetadataName(typeName);
            if (symbol != null) return symbol;

            // Search all types for a match
            foreach (var syntaxTree in compilation.SyntaxTrees)
            {
                var semanticModel = compilation.GetSemanticModel(syntaxTree);
                var root = await syntaxTree.GetRootAsync(ct);

                foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
                {
                    var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
                    if (typeSymbol?.Name == typeName)
                    {
                        return typeSymbol;
                    }
                }
            }
        }

        return null;
    }

    private string? FindSolutionPath(string? workspacePath)
    {
        if (string.IsNullOrEmpty(workspacePath))
        {
            return null;
        }

        // Look for .sln file
        var slnFiles = _fileSystem.Directory.GetFiles(workspacePath, "*.sln", SearchOption.TopDirectoryOnly);
        if (slnFiles.Length > 0)
        {
            return slnFiles[0];
        }

        // Look for .csproj file
        var csprojFiles = _fileSystem.Directory.GetFiles(workspacePath, "*.csproj", SearchOption.AllDirectories);
        return csprojFiles.Length > 0 ? csprojFiles[0] : null;
    }

    private static List<ParameterInfo>? ParseParameters(string? parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters))
        {
            return null;
        }

        var result = new List<ParameterInfo>();
        var parts = parameters.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            var tokens = part.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length >= 2)
            {
                var type = string.Join(" ", tokens[..^1]);
                var name = tokens[^1];
                result.Add(new ParameterInfo(name, type, null));
            }
        }

        return result.Count > 0 ? result : null;
    }

    private static List<AttributeInfo>? ConvertAttributes(IReadOnlyList<AttributeDto>? attributes)
    {
        if (attributes == null || attributes.Count == 0)
        {
            return null;
        }

        return attributes.Select(a => new AttributeInfo(a.Name, a.Arguments?.ToList())).ToList();
    }

    private static List<TypeParameterInfo>? ConvertTypeParameters(IReadOnlyList<TypeParameterDto>? typeParameters)
    {
        if (typeParameters == null || typeParameters.Count == 0)
        {
            return null;
        }

        return typeParameters.Select(tp => new TypeParameterInfo(tp.Name, tp.Constraints?.ToList())).ToList();
    }

    private AgentOutput BuildOutput(
        CSharpOperationsDto operations,
        List<OperationResult> results,
        List<ToolCall> toolCalls,
        int tokensUsed)
    {
        var successCount = results.Count(r => r.Success);
        var failCount = results.Count(r => !r.Success);

        // Success if any modification operation succeeded (ignore read_file and unknown operations)
        var modificationOps = new[] { "add_method", "add_property", "create_type", "implement_interface", "generate_constructor", "generate_tests", "rename" };
        var hasSuccessfulModification = results.Any(r => r.Success && modificationOps.Contains(r.Operation.ToLowerInvariant()));
        var allSuccess = failCount == 0 || hasSuccessfulModification;

        var content = new StringBuilder();
        content.AppendLine(allSuccess ? "## Task Completed Successfully" : "## Task Completed with Errors");
        content.AppendLine();
        content.AppendLine($"**Summary:** {operations.Summary}");
        content.AppendLine();
        content.AppendLine($"**Results:** {successCount} succeeded, {failCount} failed");
        content.AppendLine();

        foreach (var result in results)
        {
            var status = result.Success ? "✓" : "✗";
            content.AppendLine($"- {status} {result.Operation}: {result.Message}");
        }

        var artifacts = new Dictionary<string, string>
        {
            ["success"] = allSuccess.ToString().ToLowerInvariant(),
            ["operations_count"] = operations.Operations.Count.ToString(),
            ["success_count"] = successCount.ToString(),
            ["fail_count"] = failCount.ToString(),
        };

        if (!allSuccess)
        {
            var errors = results.Where(r => !r.Success).Select(r => $"{r.Operation}: {r.Error}");
            artifacts["errors"] = string.Join("\n", errors);
        }

        return new AgentOutput(
            Content: content.ToString(),
            TokensUsed: tokensUsed,
            ToolCalls: toolCalls,
            Artifacts: artifacts);
    }

    private sealed record OperationResult(string Operation, bool Success, string Message, string? Error = null)
    {
        public static OperationResult Succeeded(string operation, string message, string? output = null) =>
            new(operation, true, message);

        public static OperationResult Failed(string operation, string error) =>
            new(operation, false, error, error);
    }
}
