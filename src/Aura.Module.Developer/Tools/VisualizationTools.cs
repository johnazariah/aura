// <copyright file="VisualizationTools.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Tools;

using System.Text;
using Aura.Foundation.Data.Entities;
using Aura.Foundation.Rag;
using Aura.Foundation.Tools;
using Microsoft.Extensions.Logging;

/// <summary>
/// Tools for generating architecture visualizations (ASCII and Mermaid diagrams).
/// Inspired by Copilot CLI's architecture visualization capability.
/// </summary>
public static class VisualizationTools
{
    /// <summary>
    /// Registers all visualization tools with the registry.
    /// </summary>
    public static void RegisterVisualizationTools(
        IToolRegistry registry,
        ICodeGraphService codeGraphService,
        ILogger logger)
    {
        registry.RegisterTool(CreateVisualizeDependenciesTool(codeGraphService, logger));
        registry.RegisterTool(CreateVisualizeClassHierarchyTool(codeGraphService, logger));
        registry.RegisterTool(CreateVisualizeCallGraphTool(codeGraphService, logger));

        logger.LogInformation("Registered 3 visualization tools");
    }

    private static ToolDefinition CreateVisualizeDependenciesTool(
        ICodeGraphService graphService,
        ILogger logger) => new()
        {
            ToolId = "graph.visualize_dependencies",
            Name = "Visualize Project Dependencies",
            Description = """
            Generates a diagram showing project/package dependencies.
            Outputs Mermaid or ASCII format based on preference.
            Useful for understanding codebase structure.
            """,
            Categories = ["visualization", "architecture"],
            RequiresConfirmation = false,
            InputSchema = """
        {
            "type": "object",
            "properties": {
                "projectName": { 
                    "type": "string", 
                    "description": "Project name to visualize dependencies for"
                },
                "format": { 
                    "type": "string", 
                    "enum": ["mermaid", "ascii"],
                    "description": "Output format",
                    "default": "mermaid"
                },
                "workspacePath": { 
                    "type": "string", 
                    "description": "Workspace path to query"
                }
            },
            "required": ["projectName"]
        }
        """,
            Handler = async (input, ct) =>
            {
                var projectName = input.GetParameter<string>("projectName")!;
                var format = input.GetParameter("format", "mermaid")!;
                var workspacePath = input.GetParameter<string?>("workspacePath", input.WorkingDirectory);

                logger.LogInformation("Visualizing dependencies for {Project} in {Format} format",
                    projectName, format);

                try
                {
                    var references = await graphService.GetProjectReferencesAsync(
                        projectName, workspacePath, ct);

                    var diagram = format.ToLowerInvariant() switch
                    {
                        "ascii" => GenerateAsciiDependencies(projectName, references),
                        _ => GenerateMermaidDependencies(projectName, references),
                    };

                    return ToolResult.Ok(new
                    {
                        projectName,
                        format,
                        referenceCount = references.Count,
                        diagram,
                        references = references.Select(r => r.Name).ToList(),
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to visualize dependencies");
                    return ToolResult.Fail(ex.Message);
                }
            },
        };

    private static ToolDefinition CreateVisualizeClassHierarchyTool(
        ICodeGraphService graphService,
        ILogger logger) => new()
        {
            ToolId = "graph.visualize_class_hierarchy",
            Name = "Visualize Class Hierarchy",
            Description = """
            Generates a diagram showing inheritance/implementation hierarchy for a type.
            Shows base classes, interfaces, and derived classes.
            """,
            Categories = ["visualization", "architecture"],
            RequiresConfirmation = false,
            InputSchema = """
        {
            "type": "object",
            "properties": {
                "typeName": { 
                    "type": "string", 
                    "description": "Type name to visualize hierarchy for"
                },
                "format": { 
                    "type": "string", 
                    "enum": ["mermaid", "ascii"],
                    "description": "Output format",
                    "default": "mermaid"
                },
                "includeImplementations": { 
                    "type": "boolean", 
                    "description": "For interfaces, include implementing classes",
                    "default": true
                },
                "workspacePath": { 
                    "type": "string", 
                    "description": "Workspace path to query"
                }
            },
            "required": ["typeName"]
        }
        """,
            Handler = async (input, ct) =>
            {
                var typeName = input.GetParameter<string>("typeName")!;
                var format = input.GetParameter("format", "mermaid")!;
                var includeImplementations = input.GetParameter("includeImplementations", true);
                var workspacePath = input.GetParameter<string?>("workspacePath", input.WorkingDirectory);

                logger.LogInformation("Visualizing class hierarchy for {Type}", typeName);

                try
                {
                    // Find the type itself (could be class or interface)
                    var typeNodes = await graphService.FindNodesAsync(
                        typeName, null, workspacePath, ct);

                    // Find implementations (for interfaces) or derived types (for classes)
                    var implementations = includeImplementations
                        ? await graphService.FindImplementationsAsync(typeName, workspacePath, ct)
                        : [];

                    var derivedTypes = await graphService.FindDerivedTypesAsync(typeName, workspacePath, ct);

                    var diagram = format.ToLowerInvariant() switch
                    {
                        "ascii" => GenerateAsciiHierarchy(typeName, typeNodes, implementations, derivedTypes),
                        _ => GenerateMermaidHierarchy(typeName, typeNodes, implementations, derivedTypes),
                    };

                    return ToolResult.Ok(new
                    {
                        typeName,
                        format,
                        implementationCount = implementations.Count,
                        derivedTypeCount = derivedTypes.Count,
                        diagram,
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to visualize class hierarchy");
                    return ToolResult.Fail(ex.Message);
                }
            },
        };

    private static ToolDefinition CreateVisualizeCallGraphTool(
        ICodeGraphService graphService,
        ILogger logger) => new()
        {
            ToolId = "graph.visualize_call_graph",
            Name = "Visualize Call Graph",
            Description = """
            Generates a diagram showing method call relationships.
            Can show callers (who calls this method) or callees (what this method calls).
            """,
            Categories = ["visualization", "architecture"],
            RequiresConfirmation = false,
            InputSchema = """
        {
            "type": "object",
            "properties": {
                "methodName": { 
                    "type": "string", 
                    "description": "Method name to visualize calls for"
                },
                "containingType": { 
                    "type": "string", 
                    "description": "Optional containing type name for disambiguation"
                },
                "direction": { 
                    "type": "string", 
                    "enum": ["callers", "callees", "both"],
                    "description": "Direction of calls to show",
                    "default": "both"
                },
                "format": { 
                    "type": "string", 
                    "enum": ["mermaid", "ascii"],
                    "description": "Output format",
                    "default": "mermaid"
                },
                "workspacePath": { 
                    "type": "string", 
                    "description": "Workspace path to query"
                }
            },
            "required": ["methodName"]
        }
        """,
            Handler = async (input, ct) =>
            {
                var methodName = input.GetParameter<string>("methodName")!;
                var containingType = input.GetParameter<string?>("containingType", null);
                var direction = input.GetParameter("direction", "both")!;
                var format = input.GetParameter("format", "mermaid")!;
                var workspacePath = input.GetParameter<string?>("workspacePath", input.WorkingDirectory);

                logger.LogInformation("Visualizing call graph for {Method}", methodName);

                try
                {
                    IReadOnlyList<CodeNode> callers = [];
                    IReadOnlyList<CodeNode> callees = [];

                    if (direction is "callers" or "both")
                    {
                        callers = await graphService.FindCallersAsync(
                            methodName, containingType, workspacePath, ct);
                    }

                    if (direction is "callees" or "both")
                    {
                        callees = await graphService.FindDependenciesAsync(
                            methodName, containingType, workspacePath, ct);
                    }

                    var diagram = format.ToLowerInvariant() switch
                    {
                        "ascii" => GenerateAsciiCallGraph(methodName, callers, callees),
                        _ => GenerateMermaidCallGraph(methodName, callers, callees),
                    };

                    return ToolResult.Ok(new
                    {
                        methodName,
                        containingType,
                        direction,
                        format,
                        callerCount = callers.Count,
                        calleeCount = callees.Count,
                        diagram,
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to visualize call graph");
                    return ToolResult.Fail(ex.Message);
                }
            },
        };

    // ========================================================================
    // Mermaid Diagram Generators
    // ========================================================================

    private static string GenerateMermaidDependencies(string projectName, IReadOnlyList<CodeNode> references)
    {
        var sb = new StringBuilder();
        sb.AppendLine("```mermaid");
        sb.AppendLine("graph TD");

        var safeProjectName = SanitizeForMermaid(projectName);
        sb.AppendLine($"    {safeProjectName}[{projectName}]");

        foreach (var reference in references)
        {
            var safeRefName = SanitizeForMermaid(reference.Name);
            sb.AppendLine($"    {safeProjectName} --> {safeRefName}[{reference.Name}]");
        }

        if (references.Count == 0)
        {
            sb.AppendLine($"    {safeProjectName} --> NoRefs[No dependencies found]");
        }

        sb.AppendLine("```");
        return sb.ToString();
    }

    private static string GenerateMermaidHierarchy(
        string typeName,
        IReadOnlyList<CodeNode> typeNodes,
        IReadOnlyList<CodeNode> implementations,
        IReadOnlyList<CodeNode> derivedTypes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("```mermaid");
        sb.AppendLine("classDiagram");

        var safeTypeName = SanitizeForMermaid(typeName);

        // Determine if it's an interface
        var isInterface = typeNodes.Count > 0 &&
            (typeNodes[0].Name.StartsWith('I') && char.IsUpper(typeNodes[0].Name[1]));

        if (isInterface)
        {
            sb.AppendLine($"    class {safeTypeName}");
            sb.AppendLine($"    <<interface>> {safeTypeName}");
        }
        else
        {
            sb.AppendLine($"    class {safeTypeName}");
        }

        // Add implementations
        foreach (var impl in implementations)
        {
            var safeImplName = SanitizeForMermaid(impl.Name);
            sb.AppendLine($"    class {safeImplName}");
            sb.AppendLine($"    {safeTypeName} <|.. {safeImplName} : implements");
        }

        // Add derived types
        foreach (var derived in derivedTypes)
        {
            var safeDerivedName = SanitizeForMermaid(derived.Name);
            sb.AppendLine($"    class {safeDerivedName}");
            sb.AppendLine($"    {safeTypeName} <|-- {safeDerivedName} : extends");
        }

        sb.AppendLine("```");
        return sb.ToString();
    }

    private static string GenerateMermaidCallGraph(
        string methodName,
        IReadOnlyList<CodeNode> callers,
        IReadOnlyList<CodeNode> callees)
    {
        var sb = new StringBuilder();
        sb.AppendLine("```mermaid");
        sb.AppendLine("graph LR");

        var safeMethodName = SanitizeForMermaid(methodName);
        sb.AppendLine($"    {safeMethodName}(({methodName}))");

        // Add callers (arrows pointing to our method)
        foreach (var caller in callers.Take(10)) // Limit to avoid huge diagrams
        {
            var safeCallerName = SanitizeForMermaid(caller.Name);
            sb.AppendLine($"    {safeCallerName}[{caller.Name}] --> {safeMethodName}");
        }

        // Add callees (arrows pointing from our method)
        foreach (var callee in callees.Take(10))
        {
            var safeCalleeName = SanitizeForMermaid(callee.Name);
            sb.AppendLine($"    {safeMethodName} --> {safeCalleeName}[{callee.Name}]");
        }

        if (callers.Count > 10)
        {
            sb.AppendLine($"    MoreCallers[...+{callers.Count - 10} more callers] --> {safeMethodName}");
        }

        if (callees.Count > 10)
        {
            sb.AppendLine($"    {safeMethodName} --> MoreCallees[...+{callees.Count - 10} more callees]");
        }

        sb.AppendLine("```");
        return sb.ToString();
    }

    // ========================================================================
    // ASCII Diagram Generators
    // ========================================================================

    private static string GenerateAsciiDependencies(string projectName, IReadOnlyList<CodeNode> references)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"┌{'─'.Repeat(projectName.Length + 2)}┐");
        sb.AppendLine($"│ {projectName} │");
        sb.AppendLine($"└{'─'.Repeat(projectName.Length + 2)}┘");

        if (references.Count > 0)
        {
            sb.AppendLine("       │");
            sb.AppendLine("       ▼");
            sb.AppendLine("  Dependencies:");

            foreach (var reference in references)
            {
                sb.AppendLine($"    ├─ {reference.Name}");
            }
        }
        else
        {
            sb.AppendLine("  (no dependencies)");
        }

        return sb.ToString();
    }

    private static string GenerateAsciiHierarchy(
        string typeName,
        IReadOnlyList<CodeNode> typeNodes,
        IReadOnlyList<CodeNode> implementations,
        IReadOnlyList<CodeNode> derivedTypes)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"  ┌{'─'.Repeat(typeName.Length + 2)}┐");
        sb.AppendLine($"  │ {typeName} │");
        sb.AppendLine($"  └{'─'.Repeat(typeName.Length + 2)}┘");

        if (implementations.Count > 0)
        {
            sb.AppendLine("         │");
            sb.AppendLine("         ▼ implements");
            foreach (var impl in implementations)
            {
                sb.AppendLine($"    ├─ {impl.Name}");
            }
        }

        if (derivedTypes.Count > 0)
        {
            sb.AppendLine("         │");
            sb.AppendLine("         ▼ extends");
            foreach (var derived in derivedTypes)
            {
                sb.AppendLine($"    ├─ {derived.Name}");
            }
        }

        if (implementations.Count == 0 && derivedTypes.Count == 0)
        {
            sb.AppendLine("  (no implementations or derived types found)");
        }

        return sb.ToString();
    }

    private static string GenerateAsciiCallGraph(
        string methodName,
        IReadOnlyList<CodeNode> callers,
        IReadOnlyList<CodeNode> callees)
    {
        var sb = new StringBuilder();

        // Callers section
        if (callers.Count > 0)
        {
            sb.AppendLine("  Callers:");
            foreach (var caller in callers.Take(10))
            {
                sb.AppendLine($"    {caller.Name} ─┐");
            }

            if (callers.Count > 10)
            {
                sb.AppendLine($"    ...+{callers.Count - 10} more");
            }

            sb.AppendLine("                 │");
            sb.AppendLine("                 ▼");
        }

        // Target method
        sb.AppendLine($"  ┌{'─'.Repeat(methodName.Length + 2)}┐");
        sb.AppendLine($"  │ {methodName} │");
        sb.AppendLine($"  └{'─'.Repeat(methodName.Length + 2)}┘");

        // Callees section
        if (callees.Count > 0)
        {
            sb.AppendLine("         │");
            sb.AppendLine("         ▼");
            sb.AppendLine("  Calls:");
            foreach (var callee in callees.Take(10))
            {
                sb.AppendLine($"    ├─ {callee.Name}");
            }

            if (callees.Count > 10)
            {
                sb.AppendLine($"    ...+{callees.Count - 10} more");
            }
        }

        return sb.ToString();
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    internal static string SanitizeForMermaid(string name)
    {
        // Mermaid node IDs can't have special characters
        return new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
    }

    private static string Repeat(this char c, int count) => new(c, count);
}
