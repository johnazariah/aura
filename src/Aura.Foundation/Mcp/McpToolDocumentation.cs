// <copyright file="McpToolDocumentation.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Mcp;

/// <summary>
/// Single source of truth for MCP tool documentation.
/// Used by both the MCP server and copilot-instructions generation.
/// </summary>
public static class McpToolDocumentation
{
    /// <summary>
    /// Markdown documentation for Aura MCP tools, suitable for copilot-instructions.md.
    /// </summary>
    public const string CopilotInstructionsMarkdown = """
        ## Aura MCP Tools

        This workspace has Aura MCP tools available. **Prefer these over file-based exploration:**

        | Tool | Purpose | Example |
        |------|---------|---------|
        | `aura_search` | Semantic code search | `aura_search(query: "authentication middleware")` |
        | `aura_navigate` | Find code relationships | `aura_navigate(operation: "callers", symbolName: "UserService.GetAsync")` |
        | `aura_inspect` | Explore type structure | `aura_inspect(operation: "type_members", typeName: "UserService")` |
        | `aura_validate` | Check compilation/tests | `aura_validate(operation: "compilation", solutionPath: "...")` |
        | `aura_refactor` | Transform code | `aura_refactor(operation: "rename", filePath: "...", oldName: "x", newName: "y")` |
        | `aura_generate` | Generate code | `aura_generate(operation: "implement_interface", ...)` |
        | `aura_workflow` | Manage dev workflows | `aura_workflow(operation: "list")` |

        ### Operation Quick Reference

        **aura_navigate operations:**
        - `callers` - Find all callers of a method
        - `implementations` - Find types implementing an interface
        - `derived_types` - Find subclasses of a type
        - `usages` - Find all usages of a symbol
        - `references` - Find references (Python)
        - `definition` - Go to definition (Python)
        - `by_attribute` - Find symbols with attribute (e.g., `[HttpGet]`)
        - `extension_methods` - Find extension methods for a type

        **aura_inspect operations:**
        - `type_members` - Get all members of a type
        - `list_types` - List types in a project/namespace

        **aura_refactor operations:**
        - `rename` - Rename a symbol
        - `extract_method` - Extract code to new method
        - `extract_variable` - Extract expression to variable
        - `extract_interface` - Create interface from class
        - `change_signature` - Add/remove parameters
        - `safe_delete` - Delete with usage check

        **aura_generate operations:**
        - `implement_interface` - Implement interface members
        - `constructor` - Generate constructor
        - `property` - Add property
        - `method` - Add method

        **aura_validate operations:**
        - `compilation` - Check if code compiles
        - `tests` - Run unit tests

        Use `aura_search` first to understand the codebase, then `aura_navigate`/`aura_inspect` for specifics.
        """;

    /// <summary>
    /// Tool names for validation and discoverability.
    /// </summary>
    public static readonly IReadOnlyList<string> ToolNames = new[]
    {
        "aura_search",
        "aura_navigate",
        "aura_inspect",
        "aura_refactor",
        "aura_generate",
        "aura_validate",
        "aura_workflow",
        "aura_architect",
    };
}
