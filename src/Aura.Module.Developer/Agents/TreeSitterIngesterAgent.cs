// <copyright file="TreeSitterIngesterAgent.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Agents;

using System.Text.Json;
using System.Text.RegularExpressions;
using Aura.Foundation.Agents;
using Aura.Foundation.Rag;
using Microsoft.Extensions.Logging;
using TreeSitter;

/// <summary>
/// Generic TreeSitter-based ingester that supports all languages bundled with TreeSitter.DotNet.
/// Priority 20: Below Roslyn (10), above LLM fallback (40).
/// Extracts rich semantic information: signatures, docstrings, parameters, types.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="TreeSitterIngesterAgent"/> class.
/// </remarks>
/// <param name="logger">Logger instance.</param>
public sealed partial class TreeSitterIngesterAgent(ILogger<TreeSitterIngesterAgent> logger) : IAgent
{
    private readonly ILogger<TreeSitterIngesterAgent> _logger = logger;

    /// <summary>
    /// Language configurations for TreeSitter parsing.
    /// Maps file extensions to TreeSitter language names and extraction rules.
    /// </summary>
    private static readonly Dictionary<string, LanguageConfig> SupportedLanguages = new()
    {
        // Python
        ["py"] = new("python", ["function_definition", "async_function_definition", "class_definition", "decorated_definition"]),
        ["pyw"] = new("python", ["function_definition", "async_function_definition", "class_definition", "decorated_definition"]),

        // TypeScript/JavaScript
        ["ts"] = new("typescript", ["function_declaration", "class_declaration", "interface_declaration", "type_alias_declaration", "enum_declaration", "method_definition"]),
        ["tsx"] = new("tsx", ["function_declaration", "class_declaration", "interface_declaration", "type_alias_declaration", "enum_declaration", "method_definition"]),
        ["js"] = new("javascript", ["function_declaration", "class_declaration", "method_definition", "arrow_function"]),
        ["jsx"] = new("javascript", ["function_declaration", "class_declaration", "method_definition", "arrow_function"]),
        ["mjs"] = new("javascript", ["function_declaration", "class_declaration", "method_definition"]),

        // Go
        ["go"] = new("go", ["function_declaration", "method_declaration", "type_declaration"]),

        // Rust
        ["rs"] = new("rust", ["function_item", "impl_item", "struct_item", "enum_item", "trait_item", "mod_item"]),

        // Java
        ["java"] = new("java", ["class_declaration", "interface_declaration", "method_declaration", "enum_declaration", "record_declaration"]),

        // C/C++
        ["c"] = new("c", ["function_definition", "struct_specifier", "enum_specifier", "type_definition"]),
        ["h"] = new("c", ["function_definition", "struct_specifier", "enum_specifier", "type_definition"]),
        ["cpp"] = new("cpp", ["function_definition", "class_specifier", "struct_specifier", "enum_specifier", "namespace_definition"]),
        ["hpp"] = new("cpp", ["function_definition", "class_specifier", "struct_specifier", "enum_specifier", "namespace_definition"]),
        ["cc"] = new("cpp", ["function_definition", "class_specifier", "struct_specifier", "enum_specifier"]),
        ["cxx"] = new("cpp", ["function_definition", "class_specifier", "struct_specifier", "enum_specifier"]),

        // C# (backup for Roslyn)
        ["cs"] = new("c-sharp", ["class_declaration", "interface_declaration", "struct_declaration", "record_declaration", "enum_declaration", "method_declaration"]),

        // Ruby
        ["rb"] = new("ruby", ["method", "class", "module", "singleton_method"]),

        // PHP
        ["php"] = new("php", ["function_definition", "class_declaration", "interface_declaration", "trait_declaration", "method_declaration"]),

        // Swift
        ["swift"] = new("swift", ["function_declaration", "class_declaration", "struct_declaration", "enum_declaration", "protocol_declaration"]),

        // Scala
        ["scala"] = new("scala", ["function_definition", "class_definition", "object_definition", "trait_definition"]),

        // Haskell
        ["hs"] = new("haskell", ["function", "type_signature", "data", "newtype", "class", "instance"]),

        // OCaml (F#-like)
        ["ml"] = new("ocaml", ["value_definition", "type_definition", "module_definition", "class_definition"]),
        ["mli"] = new("ocaml", ["value_specification", "type_definition", "module_specification"]),

        // Julia
        ["jl"] = new("julia", ["function_definition", "struct_definition", "module_definition", "macro_definition"]),

        // Bash
        ["sh"] = new("bash", ["function_definition"]),
        ["bash"] = new("bash", ["function_definition"]),

        // HTML/CSS
        ["html"] = new("html", ["element", "script_element", "style_element"]),
        ["css"] = new("css", ["rule_set", "media_statement", "keyframes_statement"]),

        // Config files
        ["json"] = new("json", ["object", "array"]),
        ["toml"] = new("toml", ["table", "table_array_element"]),
    };

    /// <inheritdoc/>
    public string AgentId => "treesitter-ingester";

    /// <inheritdoc/>
    public AgentMetadata Metadata { get; } = new(
        Name: "TreeSitter Ingester",
        Description: "Parses source files using TreeSitter for accurate AST-based chunking. Supports 30+ languages.",
        Capabilities: SupportedLanguages.Keys.Select(ext => $"ingest:{ext}").ToList(),
        Priority: 20,  // Below Roslyn (10), above LLM (40)
        Languages: ["python", "typescript", "javascript", "go", "rust", "java", "c", "cpp", "ruby", "php", "swift", "scala", "haskell", "ocaml", "julia", "bash"],
        Provider: "native",
        Model: "treesitter",
        Temperature: 0,
        Tools: [],
        Tags: ["ingester", "treesitter", "native", "polyglot"]);

    /// <inheritdoc/>
    public Task<AgentOutput> ExecuteAsync(
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        var filePath = context.Properties.GetValueOrDefault("filePath") as string
            ?? throw new ArgumentException("filePath property is required");
        var content = context.Properties.GetValueOrDefault("content") as string
            ?? context.Prompt
            ?? throw new ArgumentException("content property or prompt is required");

        var extension = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();

        if (!SupportedLanguages.TryGetValue(extension, out var config))
        {
            _logger.LogWarning("Extension .{Extension} not supported by TreeSitter ingester", extension);
            throw new NotSupportedException($"Extension .{extension} not supported by TreeSitter ingester");
        }

        try
        {
            var chunks = ParseWithTreeSitter(filePath, content, config);

            _logger.LogDebug(
                "TreeSitter extracted {ChunkCount} chunks from {FilePath} using {Language} grammar",
                chunks.Count,
                filePath,
                config.LanguageName);

            return Task.FromResult(AgentOutput.WithArtifacts(
                $"Extracted {chunks.Count} semantic chunks using TreeSitter ({config.LanguageName})",
                new Dictionary<string, string>
                {
                    ["chunks"] = JsonSerializer.Serialize(chunks),
                    ["language"] = config.LanguageName,
                    ["parser"] = "treesitter",
                }));
        }
        catch (NotSupportedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TreeSitter parsing failed for {FilePath}", filePath);
            throw new InvalidOperationException($"TreeSitter parsing failed: {ex.Message}", ex);
        }
    }

    private List<SemanticChunk> ParseWithTreeSitter(string filePath, string content, LanguageConfig config)
    {
        var chunks = new List<SemanticChunk>();

        using var parser = new Parser();
        using var language = new Language(config.LanguageName);
        parser.Language = language;

        using var tree = parser.Parse(content);
        if (tree == null)
        {
            return chunks;
        }

        // Extract imports from the root level
        var imports = ExtractImports(tree.RootNode, config.LanguageName);

        var targetTypes = config.ChunkTypes.ToHashSet();
        ExtractChunksRecursive(tree.RootNode, filePath, config.LanguageName, targetTypes, chunks, null, imports);

        return chunks;
    }

    private void ExtractChunksRecursive(
        Node node,
        string filePath,
        string language,
        HashSet<string> targetTypes,
        List<SemanticChunk> chunks,
        string? parentSymbol,
        IReadOnlyList<ImportInfo>? fileImports = null)
    {
        if (targetTypes.Contains(node.Type))
        {
            var chunk = CreateChunk(node, filePath, language, parentSymbol, fileImports);
            chunks.Add(chunk);

            // For class-like nodes, extract children with class as parent
            if (IsClassLike(node.Type))
            {
                var className = GetSymbolName(node, language);
                var body = GetClassBody(node);

                if (body != null)
                {
                    foreach (var child in body.NamedChildren)
                    {
                        ExtractChunksRecursive(child, filePath, language, targetTypes, chunks, className, fileImports);
                    }
                }

                return; // Don't double-process children
            }
        }

        // Recurse into children
        foreach (var child in node.NamedChildren)
        {
            ExtractChunksRecursive(child, filePath, language, targetTypes, chunks, parentSymbol, fileImports);
        }
    }

    private static bool IsClassLike(string nodeType) => nodeType is
        "class_definition" or "class_declaration" or "class_specifier" or "class" or
        "interface_declaration" or "trait_declaration" or "protocol_declaration" or
        "struct_item" or "struct_specifier" or "struct_definition" or "struct_declaration" or
        "impl_item" or "trait_item" or
        "module_definition" or "module" or "mod_item" or
        "object_definition";

    private static Node? GetClassBody(Node node)
    {
        // Different languages use different body node types
        return node.NamedChildren.FirstOrDefault(c => c.Type is
            "block" or "class_body" or "declaration_list" or "field_declaration_list" or
            "interface_body" or "trait_body" or "enum_body" or "impl_body" or
            "module_body" or "body");
    }

    private SemanticChunk CreateChunk(Node node, string filePath, string language, string? parentSymbol, IReadOnlyList<ImportInfo>? fileImports)
    {
        var symbolName = GetSymbolName(node, language);
        var chunkType = MapNodeTypeToChunkType(node.Type);

        // Extract semantic information based on language
        var semantics = ExtractSemantics(node, language);

        return new SemanticChunk
        {
            Text = node.Text,
            FilePath = filePath,
            ChunkType = chunkType,
            SymbolName = symbolName,
            ParentSymbol = parentSymbol,
            FullyQualifiedName = parentSymbol != null ? $"{parentSymbol}.{symbolName}" : symbolName,
            StartLine = node.StartPosition.Row + 1,
            EndLine = node.EndPosition.Row + 1,
            Language = language,
            Signature = semantics.Signature,
            Docstring = semantics.Docstring,
            Summary = semantics.Summary,
            ReturnType = semantics.ReturnType,
            Parameters = semantics.Parameters,
            Decorators = semantics.Decorators,
            TypeReferences = semantics.TypeReferences,
            Imports = fileImports,
        };
    }

    /// <summary>
    /// Extracts semantic information from a node based on language.
    /// </summary>
    private SemanticExtraction ExtractSemantics(Node node, string language)
    {
        return language switch
        {
            "python" => ExtractPythonSemantics(node),
            "typescript" or "tsx" => ExtractTypeScriptSemantics(node),
            "javascript" => ExtractJavaScriptSemantics(node),
            "go" => ExtractGoSemantics(node),
            "rust" => ExtractRustSemantics(node),
            "java" => ExtractJavaSemantics(node),
            _ => SemanticExtraction.Empty,
        };
    }

    #region Import Extraction

    /// <summary>
    /// Extracts import statements from the root of a file.
    /// </summary>
    private List<ImportInfo>? ExtractImports(Node rootNode, string language)
    {
        var imports = new List<ImportInfo>();

        foreach (var child in rootNode.NamedChildren)
        {
            var extracted = language switch
            {
                "python" => ExtractPythonImport(child),
                "typescript" or "tsx" or "javascript" => ExtractTypeScriptImport(child),
                "go" => ExtractGoImport(child),
                "rust" => ExtractRustImport(child),
                "java" => ExtractJavaImport(child),
                _ => null,
            };

            if (extracted != null)
            {
                imports.AddRange(extracted);
            }
        }

        return imports.Count > 0 ? imports : null;
    }

    private static List<ImportInfo>? ExtractPythonImport(Node node)
    {
        // import_statement: 'import os'
        // import_from_statement: 'from typing import List, Optional'
        if (node.Type == "import_statement")
        {
            var moduleName = node.NamedChildren.FirstOrDefault(c => c.Type == "dotted_name");
            if (moduleName != null)
            {
                return [new ImportInfo { Module = moduleName.Text }];
            }
        }
        else if (node.Type == "import_from_statement")
        {
            var moduleNode = node.NamedChildren.FirstOrDefault(c => c.Type == "dotted_name");
            if (moduleNode != null)
            {
                var symbols = node.NamedChildren
                    .Skip(1) // Skip the module name
                    .Where(c => c.Type == "dotted_name")
                    .Select(c => c.Text)
                    .ToList();

                return [new ImportInfo
                {
                    Module = moduleNode.Text,
                    IsRelative = node.Text.StartsWith("from ."),
                    Symbols = symbols.Count > 0 ? symbols : null,
                }];
            }
        }

        return null;
    }

    private static List<ImportInfo>? ExtractTypeScriptImport(Node node)
    {
        // import_statement: 'import { useState } from 'react''
        if (node.Type != "import_statement")
        {
            return null;
        }

        var stringNode = node.NamedChildren.FirstOrDefault(c => c.Type == "string");
        if (stringNode == null)
        {
            return null;
        }

        var moduleText = stringNode.NamedChildren.FirstOrDefault(c => c.Type == "string_fragment")?.Text
            ?? stringNode.Text.Trim('\'', '"');

        var importClause = node.NamedChildren.FirstOrDefault(c => c.Type == "import_clause");
        if (importClause == null)
        {
            return [new ImportInfo
            {
                Module = moduleText,
                IsRelative = moduleText.StartsWith("./") || moduleText.StartsWith("../"),
            }];
        }

        var symbols = new List<string>();
        string? alias = null;

        foreach (var child in importClause.NamedChildren)
        {
            if (child.Type == "identifier")
            {
                // Default import
                symbols.Add(child.Text);
            }
            else if (child.Type == "named_imports")
            {
                foreach (var specifier in child.NamedChildren)
                {
                    if (specifier.Type == "import_specifier")
                    {
                        var name = specifier.NamedChildren.FirstOrDefault(c => c.Type == "identifier")?.Text;
                        if (name != null)
                        {
                            symbols.Add(name);
                        }
                    }
                }
            }
            else if (child.Type == "namespace_import")
            {
                alias = child.NamedChildren.FirstOrDefault(c => c.Type == "identifier")?.Text;
            }
        }

        return [new ImportInfo
        {
            Module = moduleText,
            IsRelative = moduleText.StartsWith("./") || moduleText.StartsWith("../"),
            Symbols = symbols.Count > 0 ? symbols : null,
            Alias = alias,
        }];
    }

    private static List<ImportInfo>? ExtractGoImport(Node node)
    {
        // import_declaration with import_spec_list or single import_spec
        if (node.Type != "import_declaration")
        {
            return null;
        }

        var imports = new List<ImportInfo>();

        foreach (var child in node.NamedChildren)
        {
            if (child.Type == "import_spec_list")
            {
                foreach (var spec in child.NamedChildren)
                {
                    if (spec.Type == "import_spec")
                    {
                        var pathNode = spec.NamedChildren.FirstOrDefault(c => c.Type == "interpreted_string_literal");
                        if (pathNode != null)
                        {
                            var path = pathNode.Text.Trim('"');
                            var aliasNode = spec.NamedChildren.FirstOrDefault(c => c.Type == "package_identifier");
                            imports.Add(new ImportInfo
                            {
                                Module = path,
                                Alias = aliasNode?.Text,
                            });
                        }
                    }
                }
            }
            else if (child.Type == "import_spec")
            {
                var pathNode = child.NamedChildren.FirstOrDefault(c => c.Type == "interpreted_string_literal");
                if (pathNode != null)
                {
                    imports.Add(new ImportInfo { Module = pathNode.Text.Trim('"') });
                }
            }
        }

        return imports.Count > 0 ? imports : null;
    }

    private static List<ImportInfo>? ExtractRustImport(Node node)
    {
        // use_declaration: 'use std::collections::HashMap;'
        if (node.Type != "use_declaration")
        {
            return null;
        }

        var pathText = node.Text.Replace("use ", "").TrimEnd(';').Trim();
        var isExternal = !pathText.StartsWith("crate::") && !pathText.StartsWith("self::") && !pathText.StartsWith("super::");

        return [new ImportInfo
        {
            Module = pathText,
            IsRelative = !isExternal,
        }];
    }

    private static List<ImportInfo>? ExtractJavaImport(Node node)
    {
        // import_declaration: 'import java.util.List;'
        if (node.Type != "import_declaration")
        {
            return null;
        }

        var scopedId = node.NamedChildren.FirstOrDefault(c => c.Type == "scoped_identifier");
        if (scopedId != null)
        {
            return [new ImportInfo { Module = scopedId.Text }];
        }

        return null;
    }

    #endregion

    #region Python Semantic Extraction

    private SemanticExtraction ExtractPythonSemantics(Node node)
    {
        var extraction = new SemanticExtraction();

        // Handle decorated definitions - unwrap to get the actual function/class
        var targetNode = node;
        if (node.Type == "decorated_definition")
        {
            extraction.Decorators = ExtractPythonDecorators(node);
            targetNode = node.NamedChildren.FirstOrDefault(c =>
                c.Type is "function_definition" or "async_function_definition" or "class_definition") ?? node;
        }

        // Extract signature (first line up to the colon)
        var signatureLine = targetNode.Text.Split('\n').FirstOrDefault()?.TrimEnd();
        if (signatureLine != null)
        {
            extraction.Signature = signatureLine;
        }

        // Extract docstring (first statement in body if it's a string)
        var body = targetNode.NamedChildren.FirstOrDefault(c => c.Type == "block");
        if (body != null)
        {
            var firstChild = body.NamedChildren.FirstOrDefault();

            // Python docstrings can be:
            // 1. Direct string child of block
            // 2. expression_statement containing a string
            if (firstChild?.Type == "string")
            {
                var docstring = CleanPythonDocstring(firstChild.Text);
                extraction.Docstring = docstring;
                extraction.Summary = ExtractFirstSentence(docstring);
            }
            else if (firstChild?.Type == "expression_statement")
            {
                var expr = firstChild.NamedChildren.FirstOrDefault();
                if (expr?.Type == "string")
                {
                    var docstring = CleanPythonDocstring(expr.Text);
                    extraction.Docstring = docstring;
                    extraction.Summary = ExtractFirstSentence(docstring);
                }
            }
        }

        // Extract parameters and return type for functions
        if (targetNode.Type is "function_definition" or "async_function_definition")
        {
            var parameters = targetNode.NamedChildren.FirstOrDefault(c => c.Type == "parameters");
            if (parameters != null)
            {
                extraction.Parameters = ExtractPythonParameters(parameters, extraction.Docstring);
                extraction.TypeReferences = ExtractTypeReferencesFromParameters(extraction.Parameters);
            }

            // Return type annotation
            var returnType = targetNode.NamedChildren.FirstOrDefault(c => c.Type == "type");
            if (returnType != null)
            {
                extraction.ReturnType = returnType.Text;
                extraction.TypeReferences = AddTypeReference(extraction.TypeReferences, returnType.Text);
            }
        }

        return extraction;
    }

    private static List<string> ExtractPythonDecorators(Node decoratedNode)
    {
        var decorators = new List<string>();
        foreach (var child in decoratedNode.NamedChildren)
        {
            if (child.Type == "decorator")
            {
                decorators.Add(child.Text.TrimStart('@').Trim());
            }
        }

        return decorators;
    }

    private static List<ParameterInfo> ExtractPythonParameters(Node parametersNode, string? docstring)
    {
        var parameters = new List<ParameterInfo>();
        var docParams = ParsePythonDocstringParams(docstring);

        foreach (var child in parametersNode.NamedChildren)
        {
            if (child.Type is "identifier" or "typed_parameter" or "default_parameter" or "typed_default_parameter")
            {
                var param = ExtractPythonParameter(child, docParams);
                if (param != null && param.Name != "self" && param.Name != "cls")
                {
                    parameters.Add(param);
                }
            }
            else if (child.Type is "list_splat_pattern" or "dictionary_splat_pattern")
            {
                // *args or **kwargs
                var name = child.NamedChildren.FirstOrDefault()?.Text;
                if (name != null)
                {
                    var prefix = child.Type == "list_splat_pattern" ? "*" : "**";
                    parameters.Add(new ParameterInfo
                    {
                        Name = prefix + name,
                        Description = docParams.GetValueOrDefault(name),
                    });
                }
            }
        }

        return parameters;
    }

    private static ParameterInfo? ExtractPythonParameter(Node paramNode, Dictionary<string, string> docParams)
    {
        string? name = null;
        string? type = null;
        string? defaultValue = null;

        switch (paramNode.Type)
        {
            case "identifier":
                name = paramNode.Text;
                break;

            case "typed_parameter":
                name = paramNode.NamedChildren.FirstOrDefault(c => c.Type == "identifier")?.Text;
                type = paramNode.NamedChildren.FirstOrDefault(c => c.Type == "type")?.Text;
                break;

            case "default_parameter":
                name = paramNode.NamedChildren.FirstOrDefault(c => c.Type == "identifier")?.Text;
                defaultValue = paramNode.NamedChildren.LastOrDefault()?.Text;
                break;

            case "typed_default_parameter":
                name = paramNode.NamedChildren.FirstOrDefault(c => c.Type == "identifier")?.Text;
                type = paramNode.NamedChildren.FirstOrDefault(c => c.Type == "type")?.Text;
                // Default value is after the '='
                var children = paramNode.NamedChildren.ToList();
                if (children.Count >= 3)
                {
                    defaultValue = children[^1].Text;
                }

                break;
        }

        if (name == null)
        {
            return null;
        }

        return new ParameterInfo
        {
            Name = name,
            Type = type,
            DefaultValue = defaultValue,
            Description = docParams.GetValueOrDefault(name),
        };
    }

    private static Dictionary<string, string> ParsePythonDocstringParams(string? docstring)
    {
        var result = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(docstring))
        {
            return result;
        }

        // Parse Google-style: Args:\n    param_name: description
        // Parse NumPy-style: Parameters\n----------\nparam_name : type\n    description
        // Parse Sphinx-style: :param param_name: description

        // Google/NumPy style
        var argsMatch = GoogleStyleArgsRegex().Match(docstring);
        if (argsMatch.Success)
        {
            var argsSection = argsMatch.Groups[1].Value;
            foreach (Match paramMatch in GoogleStyleParamRegex().Matches(argsSection))
            {
                result[paramMatch.Groups[1].Value] = paramMatch.Groups[2].Value.Trim();
            }
        }

        // Sphinx style
        foreach (Match match in SphinxStyleParamRegex().Matches(docstring))
        {
            result[match.Groups[1].Value] = match.Groups[2].Value.Trim();
        }

        return result;
    }

    private static string CleanPythonDocstring(string raw)
    {
        // Remove surrounding quotes (""" or ''')
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("\"\"\"") && trimmed.EndsWith("\"\"\""))
        {
            trimmed = trimmed[3..^3];
        }
        else if (trimmed.StartsWith("'''") && trimmed.EndsWith("'''"))
        {
            trimmed = trimmed[3..^3];
        }
        else if (trimmed.StartsWith("\"") && trimmed.EndsWith("\""))
        {
            trimmed = trimmed[1..^1];
        }
        else if (trimmed.StartsWith("'") && trimmed.EndsWith("'"))
        {
            trimmed = trimmed[1..^1];
        }

        return trimmed.Trim();
    }

    [GeneratedRegex(@"Args?:\s*\n((?:\s+\w+.*\n?)+)", RegexOptions.IgnoreCase)]
    private static partial Regex GoogleStyleArgsRegex();

    [GeneratedRegex(@"^\s+(\w+)\s*(?:\([^)]*\))?:\s*(.+?)(?=\n\s+\w+|\n\n|\z)", RegexOptions.Multiline)]
    private static partial Regex GoogleStyleParamRegex();

    [GeneratedRegex(@":param\s+(\w+):\s*(.+?)(?=:param|:returns?|:raises?|\z)", RegexOptions.Singleline)]
    private static partial Regex SphinxStyleParamRegex();

    #endregion

    #region TypeScript/JavaScript Semantic Extraction

    private SemanticExtraction ExtractTypeScriptSemantics(Node node)
    {
        var extraction = new SemanticExtraction();

        // Extract JSDoc comment (preceding comment)
        var jsdoc = ExtractPrecedingComment(node);
        if (jsdoc != null)
        {
            extraction.Docstring = CleanJsDocComment(jsdoc);
            extraction.Summary = ExtractFirstSentence(extraction.Docstring);
        }

        // Extract signature
        extraction.Signature = ExtractTypeScriptSignature(node);

        // Extract parameters
        var parameters = node.NamedChildren.FirstOrDefault(c =>
            c.Type is "formal_parameters" or "parameters");
        if (parameters != null)
        {
            extraction.Parameters = ExtractTypeScriptParameters(parameters, extraction.Docstring);
            extraction.TypeReferences = ExtractTypeReferencesFromParameters(extraction.Parameters);
        }

        // Extract return type
        var returnType = node.NamedChildren.FirstOrDefault(c => c.Type == "type_annotation");
        if (returnType != null)
        {
            var typeNode = returnType.NamedChildren.FirstOrDefault();
            if (typeNode != null)
            {
                extraction.ReturnType = typeNode.Text;
                extraction.TypeReferences = AddTypeReference(extraction.TypeReferences, typeNode.Text);
            }
        }

        return extraction;
    }

    private SemanticExtraction ExtractJavaScriptSemantics(Node node)
    {
        // JavaScript is similar but without type annotations
        var extraction = new SemanticExtraction();

        var jsdoc = ExtractPrecedingComment(node);
        if (jsdoc != null)
        {
            extraction.Docstring = CleanJsDocComment(jsdoc);
            extraction.Summary = ExtractFirstSentence(extraction.Docstring);

            // Parse @param and @returns from JSDoc
            extraction.Parameters = ParseJsDocParams(jsdoc);
            extraction.ReturnType = ParseJsDocReturnType(jsdoc);
        }

        extraction.Signature = ExtractTypeScriptSignature(node);

        return extraction;
    }

    private static string? ExtractTypeScriptSignature(Node node)
    {
        // Get first line, or up to the opening brace
        var text = node.Text;
        var braceIndex = text.IndexOf('{');
        if (braceIndex > 0)
        {
            var sig = text[..braceIndex].Trim();
            // Remove trailing newlines but keep the signature
            return sig.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).FirstOrDefault();
        }

        return text.Split('\n').FirstOrDefault()?.Trim();
    }

    private static List<ParameterInfo> ExtractTypeScriptParameters(Node parametersNode, string? docstring)
    {
        var parameters = new List<ParameterInfo>();
        var docParams = ParseJsDocParamsDict(docstring);

        foreach (var child in parametersNode.NamedChildren)
        {
            if (child.Type is "required_parameter" or "optional_parameter" or "identifier")
            {
                var param = ExtractTypeScriptParameter(child, docParams);
                if (param != null)
                {
                    parameters.Add(param);
                }
            }
        }

        return parameters;
    }

    private static ParameterInfo? ExtractTypeScriptParameter(Node paramNode, Dictionary<string, string> docParams)
    {
        string? name = null;
        string? type = null;
        string? defaultValue = null;

        if (paramNode.Type == "identifier")
        {
            name = paramNode.Text;
        }
        else
        {
            // required_parameter or optional_parameter
            var pattern = paramNode.NamedChildren.FirstOrDefault(c =>
                c.Type is "identifier" or "object_pattern" or "array_pattern");
            name = pattern?.Type == "identifier" ? pattern.Text : pattern?.Text.Split('\n').FirstOrDefault();

            var typeAnnotation = paramNode.NamedChildren.FirstOrDefault(c => c.Type == "type_annotation");
            if (typeAnnotation != null)
            {
                type = typeAnnotation.NamedChildren.FirstOrDefault()?.Text;
            }

            // Check for default value (in optional_parameter)
            var assignmentPattern = paramNode.NamedChildren.FirstOrDefault(c => c.Type == "assignment_pattern");
            if (assignmentPattern != null)
            {
                defaultValue = assignmentPattern.NamedChildren.LastOrDefault()?.Text;
            }
        }

        if (name == null)
        {
            return null;
        }

        return new ParameterInfo
        {
            Name = name,
            Type = type,
            DefaultValue = defaultValue,
            Description = docParams.GetValueOrDefault(name),
        };
    }

    private static string CleanJsDocComment(string comment)
    {
        // Remove /** */ and leading asterisks
        var lines = comment.Split('\n')
            .Select(l => l.Trim())
            .Select(l => l.TrimStart('/', '*').TrimEnd('/', '*').Trim())
            .Where(l => !string.IsNullOrEmpty(l) && !l.StartsWith('@'))
            .ToList();

        return string.Join(" ", lines).Trim();
    }

    private static List<ParameterInfo> ParseJsDocParams(string jsdoc)
    {
        var parameters = new List<ParameterInfo>();
        foreach (Match match in JsDocParamRegex().Matches(jsdoc))
        {
            parameters.Add(new ParameterInfo
            {
                Name = match.Groups[2].Value,
                Type = match.Groups[1].Success ? match.Groups[1].Value : null,
                Description = match.Groups[3].Value.Trim(),
            });
        }

        return parameters;
    }

    private static Dictionary<string, string> ParseJsDocParamsDict(string? jsdoc)
    {
        var result = new Dictionary<string, string>();
        if (jsdoc == null)
        {
            return result;
        }

        foreach (Match match in JsDocParamRegex().Matches(jsdoc))
        {
            result[match.Groups[2].Value] = match.Groups[3].Value.Trim();
        }

        return result;
    }

    private static string? ParseJsDocReturnType(string jsdoc)
    {
        var match = JsDocReturnsRegex().Match(jsdoc);
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex(@"@param\s+(?:\{([^}]+)\})?\s*(\w+)\s+(.+?)(?=@|\*/|\z)", RegexOptions.Singleline)]
    private static partial Regex JsDocParamRegex();

    [GeneratedRegex(@"@returns?\s+\{([^}]+)\}", RegexOptions.Singleline)]
    private static partial Regex JsDocReturnsRegex();

    #endregion

    #region Go Semantic Extraction

    private SemanticExtraction ExtractGoSemantics(Node node)
    {
        var extraction = new SemanticExtraction();

        // Go doc comments are immediately preceding // comments
        var docComment = ExtractPrecedingComment(node);
        if (docComment != null)
        {
            extraction.Docstring = CleanGoDocComment(docComment);
            extraction.Summary = ExtractFirstSentence(extraction.Docstring);
        }

        // Extract signature (first line)
        extraction.Signature = node.Text.Split('\n').FirstOrDefault()?.Trim();

        // Extract parameters
        var parameters = node.NamedChildren.FirstOrDefault(c => c.Type == "parameter_list");
        if (parameters != null)
        {
            extraction.Parameters = ExtractGoParameters(parameters);
            extraction.TypeReferences = ExtractTypeReferencesFromParameters(extraction.Parameters);
        }

        // Extract return type
        var result = node.NamedChildren.FirstOrDefault(c =>
            c.Type is "type_identifier" or "pointer_type" or "slice_type" or "map_type" or "parameter_list" &&
            c.StartPosition.Row == node.NamedChildren.FirstOrDefault(p => p.Type == "parameter_list")?.EndPosition.Row);

        // Simple return type after parameters
        var funcBody = node.NamedChildren.FirstOrDefault(c => c.Type == "block");
        if (funcBody != null)
        {
            var beforeBody = node.Text[..(funcBody.StartPosition.Column > 0 ? funcBody.StartPosition.Column : node.Text.IndexOf('{'))];
            var returnMatch = GoReturnTypeRegex().Match(beforeBody);
            if (returnMatch.Success)
            {
                extraction.ReturnType = returnMatch.Groups[1].Value.Trim();
            }
        }

        return extraction;
    }

    private static List<ParameterInfo> ExtractGoParameters(Node paramListNode)
    {
        var parameters = new List<ParameterInfo>();

        foreach (var child in paramListNode.NamedChildren)
        {
            if (child.Type == "parameter_declaration")
            {
                var names = child.NamedChildren.Where(c => c.Type == "identifier").ToList();
                var typeNode = child.NamedChildren.FirstOrDefault(c =>
                    c.Type is "type_identifier" or "pointer_type" or "slice_type" or "map_type" or "qualified_type" or "array_type");

                var type = typeNode?.Text;
                foreach (var nameNode in names)
                {
                    parameters.Add(new ParameterInfo
                    {
                        Name = nameNode.Text,
                        Type = type,
                    });
                }
            }
        }

        return parameters;
    }

    private static string CleanGoDocComment(string comment)
    {
        var lines = comment.Split('\n')
            .Select(l => l.TrimStart().TrimStart('/').Trim())
            .Where(l => !string.IsNullOrEmpty(l));
        return string.Join(" ", lines);
    }

    [GeneratedRegex(@"\)\s*([^{]+?)\s*\{")]
    private static partial Regex GoReturnTypeRegex();

    #endregion

    #region Rust Semantic Extraction

    private SemanticExtraction ExtractRustSemantics(Node node)
    {
        var extraction = new SemanticExtraction();

        // Rust doc comments are /// or //!
        var docComment = ExtractPrecedingComment(node);
        if (docComment != null)
        {
            extraction.Docstring = CleanRustDocComment(docComment);
            extraction.Summary = ExtractFirstSentence(extraction.Docstring);
        }

        // Extract attributes as decorators
        extraction.Decorators = ExtractRustAttributes(node);

        // Extract signature
        if (node.Type == "function_item")
        {
            extraction.Signature = ExtractRustFunctionSignature(node);

            // Parameters
            var parameters = node.NamedChildren.FirstOrDefault(c => c.Type == "parameters");
            if (parameters != null)
            {
                extraction.Parameters = ExtractRustParameters(parameters);
                extraction.TypeReferences = ExtractTypeReferencesFromParameters(extraction.Parameters);
            }

            // Return type
            var returnType = node.NamedChildren.FirstOrDefault(c => c.Type == "type_identifier" || c.Type == "generic_type");
            if (returnType != null)
            {
                extraction.ReturnType = returnType.Text;
            }
        }

        return extraction;
    }

    private static List<string> ExtractRustAttributes(Node node)
    {
        var attributes = new List<string>();
        // Look for attribute_item siblings before the node (simplified - look within parent)
        var parent = node.Parent;
        if (parent != null)
        {
            foreach (var sibling in parent.NamedChildren)
            {
                if (sibling.StartPosition.Row >= node.StartPosition.Row)
                {
                    break;
                }

                if (sibling.Type == "attribute_item")
                {
                    attributes.Add(sibling.Text.TrimStart('#').Trim('[', ']'));
                }
            }
        }

        return attributes;
    }

    private static string? ExtractRustFunctionSignature(Node node)
    {
        var text = node.Text;
        var braceIndex = text.IndexOf('{');
        if (braceIndex > 0)
        {
            return text[..braceIndex].Trim();
        }

        return text.Split('\n').FirstOrDefault()?.Trim();
    }

    private static List<ParameterInfo> ExtractRustParameters(Node parametersNode)
    {
        var parameters = new List<ParameterInfo>();

        foreach (var child in parametersNode.NamedChildren)
        {
            if (child.Type == "parameter")
            {
                var pattern = child.NamedChildren.FirstOrDefault(c => c.Type == "identifier");
                var typeNode = child.NamedChildren.FirstOrDefault(c =>
                    c.Type is "type_identifier" or "reference_type" or "generic_type" or "primitive_type");

                if (pattern != null)
                {
                    parameters.Add(new ParameterInfo
                    {
                        Name = pattern.Text,
                        Type = typeNode?.Text,
                    });
                }
            }
            else if (child.Type == "self_parameter")
            {
                // Skip self
                continue;
            }
        }

        return parameters;
    }

    private static string CleanRustDocComment(string comment)
    {
        var lines = comment.Split('\n')
            .Select(l => l.TrimStart().TrimStart('/').TrimStart('!').Trim())
            .Where(l => !string.IsNullOrEmpty(l));
        return string.Join(" ", lines);
    }

    #endregion

    #region Java Semantic Extraction

    private SemanticExtraction ExtractJavaSemantics(Node node)
    {
        var extraction = new SemanticExtraction();

        // Javadoc comments
        var javadoc = ExtractPrecedingComment(node);
        if (javadoc != null)
        {
            extraction.Docstring = CleanJsDocComment(javadoc); // Similar to JSDoc
            extraction.Summary = ExtractFirstSentence(extraction.Docstring);
        }

        // Extract annotations as decorators
        extraction.Decorators = ExtractJavaAnnotations(node);

        // Extract signature
        if (node.Type == "method_declaration")
        {
            extraction.Signature = ExtractJavaMethodSignature(node);

            var parameters = node.NamedChildren.FirstOrDefault(c => c.Type == "formal_parameters");
            if (parameters != null)
            {
                extraction.Parameters = ExtractJavaParameters(parameters);
                extraction.TypeReferences = ExtractTypeReferencesFromParameters(extraction.Parameters);
            }

            var returnType = node.NamedChildren.FirstOrDefault(c =>
                c.Type is "type_identifier" or "generic_type" or "void_type" or "array_type");
            if (returnType != null)
            {
                extraction.ReturnType = returnType.Text;
            }
        }

        return extraction;
    }

    private static List<string> ExtractJavaAnnotations(Node node)
    {
        var annotations = new List<string>();
        var parent = node.Parent;
        if (parent != null)
        {
            foreach (var sibling in parent.NamedChildren)
            {
                if (sibling.StartPosition.Row >= node.StartPosition.Row)
                {
                    break;
                }

                if (sibling.Type is "annotation" or "marker_annotation")
                {
                    annotations.Add(sibling.Text.TrimStart('@'));
                }
            }
        }

        // Also check for modifiers containing annotations
        var modifiers = node.NamedChildren.FirstOrDefault(c => c.Type == "modifiers");
        if (modifiers != null)
        {
            foreach (var child in modifiers.NamedChildren)
            {
                if (child.Type is "annotation" or "marker_annotation")
                {
                    annotations.Add(child.Text.TrimStart('@'));
                }
            }
        }

        return annotations;
    }

    private static string? ExtractJavaMethodSignature(Node node)
    {
        var text = node.Text;
        var braceIndex = text.IndexOf('{');
        if (braceIndex > 0)
        {
            return text[..braceIndex].Trim();
        }

        return text.Split('\n').FirstOrDefault()?.Trim();
    }

    private static List<ParameterInfo> ExtractJavaParameters(Node parametersNode)
    {
        var parameters = new List<ParameterInfo>();

        foreach (var child in parametersNode.NamedChildren)
        {
            if (child.Type == "formal_parameter" || child.Type == "spread_parameter")
            {
                var typeNode = child.NamedChildren.FirstOrDefault(c =>
                    c.Type is "type_identifier" or "generic_type" or "array_type" or "integral_type" or "floating_point_type" or "boolean_type");
                var nameNode = child.NamedChildren.FirstOrDefault(c => c.Type == "identifier");

                if (nameNode != null)
                {
                    parameters.Add(new ParameterInfo
                    {
                        Name = nameNode.Text,
                        Type = typeNode?.Text,
                    });
                }
            }
        }

        return parameters;
    }

    #endregion

    #region Shared Helpers

    private string? ExtractPrecedingComment(Node node)
    {
        // Look for a comment node immediately before this node in the parent's children
        var parent = node.Parent;
        if (parent == null)
        {
            return null;
        }

        Node? lastComment = null;

        // Use NamedChildren to find comments (they are named nodes in many grammars)
        foreach (var sibling in parent.NamedChildren)
        {
            if (sibling.StartPosition.Row >= node.StartPosition.Row)
            {
                break;
            }

            if (sibling.Type is "comment" or "block_comment" or "line_comment")
            {
                // Only consider if it's immediately before (within 2 lines to handle blank lines)
                if (node.StartPosition.Row - sibling.EndPosition.Row <= 2)
                {
                    lastComment = sibling;
                }
            }
        }

        // Also check non-named children (some grammars put comments there)
        if (lastComment == null)
        {
            foreach (var sibling in parent.Children)
            {
                if (sibling.StartPosition.Row >= node.StartPosition.Row)
                {
                    break;
                }

                if (sibling.Type is "comment" or "block_comment" or "line_comment")
                {
                    if (node.StartPosition.Row - sibling.EndPosition.Row <= 2)
                    {
                        lastComment = sibling;
                    }
                }
            }
        }

        return lastComment?.Text;
    }

    private static string ExtractFirstSentence(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        // Find first sentence (ends with . or first line)
        var periodIndex = text.IndexOf('.');
        if (periodIndex > 0 && periodIndex < 200)
        {
            return text[..(periodIndex + 1)].Trim();
        }

        var newlineIndex = text.IndexOf('\n');
        if (newlineIndex > 0)
        {
            return text[..newlineIndex].Trim();
        }

        return text.Length > 200 ? text[..200].Trim() + "..." : text.Trim();
    }

    private static List<string>? ExtractTypeReferencesFromParameters(IReadOnlyList<ParameterInfo>? parameters)
    {
        if (parameters == null || parameters.Count == 0)
        {
            return null;
        }

        var types = parameters
            .Where(p => !string.IsNullOrEmpty(p.Type))
            .Select(p => ExtractBaseType(p.Type!))
            .Where(t => !IsPrimitiveType(t))
            .Distinct()
            .ToList();

        return types.Count > 0 ? types : null;
    }

    private static List<string>? AddTypeReference(IReadOnlyList<string>? existing, string type)
    {
        var baseType = ExtractBaseType(type);
        if (IsPrimitiveType(baseType))
        {
            return existing?.ToList();
        }

        var list = existing?.ToList() ?? [];
        if (!list.Contains(baseType))
        {
            list.Add(baseType);
        }

        return list;
    }

    private static string ExtractBaseType(string type)
    {
        // Extract base type from generics, arrays, etc.
        // List<Item> -> List, Item
        // int[] -> int
        // *Item -> Item
        var cleaned = type.TrimStart('*', '&').TrimEnd('?');

        // Handle generics
        var genericIndex = cleaned.IndexOf('<');
        if (genericIndex > 0)
        {
            return cleaned[..genericIndex];
        }

        // Handle arrays
        var arrayIndex = cleaned.IndexOf('[');
        if (arrayIndex > 0)
        {
            return cleaned[..arrayIndex];
        }

        return cleaned;
    }

    private static bool IsPrimitiveType(string type) => type.ToLowerInvariant() is
        "int" or "float" or "double" or "bool" or "boolean" or "string" or "str" or
        "char" or "byte" or "short" or "long" or "void" or "any" or "object" or
        "number" or "i32" or "i64" or "u32" or "u64" or "f32" or "f64" or "usize" or "isize";

    #endregion

    /// <summary>
    /// Result of semantic extraction from a node.
    /// </summary>
    private sealed class SemanticExtraction
    {
        public static SemanticExtraction Empty => new();

        public string? Signature { get; set; }
        public string? Docstring { get; set; }
        public string? Summary { get; set; }
        public string? ReturnType { get; set; }
        public List<ParameterInfo>? Parameters { get; set; }
        public List<string>? Decorators { get; set; }
        public List<string>? TypeReferences { get; set; }
    }

    private static string GetSymbolName(Node node, string language)
    {
        // Language-specific handling
        if (language == "go")
        {
            // Go type_declaration has type_spec children with the actual name
            var typeSpec = node.NamedChildren.FirstOrDefault(c => c.Type == "type_spec");
            if (typeSpec != null)
            {
                var typeName = typeSpec.NamedChildren.FirstOrDefault(c => c.Type == "type_identifier");
                if (typeName != null)
                {
                    return typeName.Text;
                }
            }

            // Go method_declaration has receiver and name
            var funcName = node.NamedChildren.FirstOrDefault(c => c.Type == "field_identifier");
            if (funcName != null)
            {
                return funcName.Text;
            }
        }

        if (language == "ocaml")
        {
            // OCaml let bindings have a pattern with the name
            var letBinding = node.NamedChildren.FirstOrDefault(c => c.Type == "let_binding");
            if (letBinding != null)
            {
                var pattern = letBinding.NamedChildren.FirstOrDefault(c => c.Type == "value_name");
                if (pattern != null)
                {
                    return pattern.Text;
                }
            }

            // Module definitions
            var moduleName = node.NamedChildren.FirstOrDefault(c => c.Type == "module_name");
            if (moduleName != null)
            {
                return moduleName.Text;
            }

            // Type definitions
            var typeName = node.NamedChildren.FirstOrDefault(c => c.Type == "type_constructor");
            if (typeName != null)
            {
                return typeName.Text;
            }
        }

        // Find identifier child - different languages use different names
        var nameNode = node.NamedChildren.FirstOrDefault(c => c.Type is
            "identifier" or "property_identifier" or "name" or "type_identifier" or
            "constant" or "simple_identifier" or "word" or "field_identifier" or
            "value_name" or "module_name" or "type_constructor");

        if (nameNode != null)
        {
            return nameNode.Text;
        }

        // For some node types, the first child might be the name
        var firstNamed = node.NamedChildren.FirstOrDefault();
        if (firstNamed != null && firstNamed.Text.Length < 50 && !firstNamed.Text.Contains('\n'))
        {
            return firstNamed.Text;
        }

        return "anonymous";
    }

    private static string MapNodeTypeToChunkType(string nodeType) => nodeType switch
    {
        // Functions
        "function_definition" or "function_declaration" or "function_item" or "function" => "function",
        "async_function_definition" => "async_function",
        "method_definition" or "method_declaration" or "method" or "singleton_method" => "method",
        "arrow_function" => "arrow_function",

        // Classes and types
        "class_definition" or "class_declaration" or "class_specifier" or "class" => "class",
        "interface_declaration" => "interface",
        "struct_item" or "struct_specifier" or "struct_definition" or "struct_declaration" => "struct",
        "enum_declaration" or "enum_item" or "enum_specifier" => "enum",
        "trait_item" or "trait_declaration" or "trait_definition" => "trait",
        "protocol_declaration" => "protocol",
        "record_declaration" => "record",

        // Type definitions
        "type_alias_declaration" or "type_definition" or "type_signature" or "type_declaration" => "type",
        "impl_item" => "impl",

        // Modules
        "module_definition" or "module" or "mod_item" or "namespace_definition" => "module",
        "object_definition" => "object",

        // Decorated
        "decorated_definition" => "decorated",

        // Data types (Haskell)
        "data" or "newtype" => "data",
        "instance" => "instance",

        // Macros
        "macro_definition" => "macro",

        _ => nodeType.Replace("_", " "),
    };

    private sealed record LanguageConfig(string LanguageName, string[] ChunkTypes);
}
