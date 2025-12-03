# Language Specialist Agent Specification

## Overview

Rather than creating a separate hardcoded agent for every programming language, we create a **Language Specialist Agent** that reads its behavior from a YAML configuration file. Each language gets **ONE file** containing:

- Agent metadata (capabilities, priority)
- Tool definitions (CLI commands, input schemas, output parsers)
- Prompt template sections (workflow, best practices, syntax reminders)
- Everything needed to understand and code in that language

This follows the same pattern as markdown-defined agents but with structured tool definitions optimized for coding tasks.

## Agent Types Comparison

| Type | Config Format | Use Case | Example |
|------|--------------|----------|---------|
| **Markdown Agent** | `.md` file | General purpose, any capability | `echo-agent.md`, `chat-agent.md` |
| **Language Specialist** | `.yaml` file | Language-specific coding with CLI tools | `python.yaml`, `rust.yaml` |
| **Hardcoded Agent** | C# class | Complex logic, compiler APIs | `RoslynCodingAgent` |

### When to Use Each

- **Markdown Agent**: For non-coding tasks, simple agents, prototyping
- **Language Specialist**: For coding tasks where language has CLI tools (build, format, lint, test)
- **Hardcoded Agent**: When you need compiler service integration (Roslyn for C#, rust-analyzer for Rust)

### C# is the Exception

C# uses **RoslynCodingAgent** (hardcoded) because it needs Roslyn compiler APIs for:
- Semantic code analysis (not just syntax)
- Intelligent symbol navigation via code graph
- Precise refactoring with type awareness
- Real-time compilation validation with rich diagnostics

The `agents/languages/csharp.yaml` file exists for documentation and defines CLI fallback tools,
but the actual agent is `RoslynCodingAgent` in C#. This is the canonical example of when a
hardcoded agent is necessary.

### Language Specialist vs Compiler Service Agent

The fundamental difference:

| Aspect | Language Specialist (YAML) | Compiler Service Agent (Hardcoded) |
|--------|---------------------------|-----------------------------------|
| **Tools** | CLI wrappers (`python`, `cargo`) | Compiler APIs (`Microsoft.CodeAnalysis`) |
| **Understanding** | Text output parsing | Semantic symbol analysis |
| **Type awareness** | None - just runs commands | Full - knows types, inheritance, interfaces |
| **Navigation** | None | Find usages, callers, implementations |
| **Validation** | Parse CLI output for errors | Rich diagnostics with exact locations |
| **Refactoring** | Find/replace text | Type-safe symbol renaming |

### Compiler Service Integration Status

Languages that **could** have hardcoded agents with deep compiler integration:

| Language | Compiler Service | Status | Notes |
|----------|-----------------|--------|-------|
| **C#** | Roslyn | ✅ Implemented | `RoslynCodingAgent` with full semantic analysis |
| **F#** | FSharp.Compiler.Service | 📋 Planned | Could add semantic-aware F# agent |
| **Rust** | rust-analyzer (LSP) | 📋 Possible | Would need LSP client integration |
| **TypeScript** | tsserver (LSP) | 📋 Possible | Would need LSP client integration |
| **Go** | gopls (LSP) | 📋 Possible | Would need LSP client integration |
| **Python** | Pyright/Pylance (LSP) | 📋 Possible | Would need LSP client integration |

Languages that are **fine with YAML configs** (CLI tools are sufficient):

| Language | Why CLI is Enough |
|----------|------------------|
| Elm | Compiler has excellent error messages, no need for semantic API |
| Haskell | GHC errors are detailed, HLint covers linting |
| Terraform | Declarative - validate/plan/apply is the workflow |
| Bicep | Azure CLI + Bicep compiler covers all needs |
| Bash/PowerShell | Shellcheck/PSScriptAnalyzer are CLI-based |

### When to Add a Compiler Service Agent

Add a hardcoded agent with compiler service integration when you need:

1. **"Find all implementations of interface X"** - Requires semantic analysis
2. **"What calls this method?"** - Requires call graph from compiler
3. **"Rename this type across the codebase"** - Requires symbol resolution
4. **"Navigate to definition"** - Requires semantic model

If your needs are just "build, test, lint, format" - use a YAML config.

### Key Principle: One File Per Language

Previously we had confusion:
- `prompts/fsharp-coding.prompt` - Handlebars template with workflow + best practices
- `agents/languages/fsharp.yaml` - Tool definitions

Now **everything is in the YAML file**. The YAML includes a `prompt` section that contains the same content that was in the `.prompt` files. No separate prompt file needed.

## Problem Statement

Currently, adding a new language requires:

1. Creating `{Language}Tools.cs` - register shell-based tools
2. Creating `{Language}CodingAgent.cs` - hardcoded agent class
3. Creating `{language}-coding.prompt` - prompt template
4. Updating `DeveloperAgentProvider.cs` - add to agent list
5. Updating `DeveloperModule.cs` - register tools
6. Creating tests

This is ~500 lines of boilerplate per language, when the only real differences are:

- Tool names and CLI commands
- Language-specific best practices in the prompt
- File extensions and project structure

## Solution: Language Specialist Agent

### Complete Language Definition Schema (with Output Parsers)

```yaml
# agents/languages/fsharp.yaml
# COMPLETE definition - no separate .prompt file needed

language:
  id: fsharp
  name: F#
  extensions: [".fs", ".fsx", ".fsi"]
  projectFiles: ["*.fsproj", "*.sln"]

capabilities:
  - fsharp-coding
  - coding
  - functional-programming
  - dotnet

priority: 10  # Specialist

# Agent configuration
agent:
  provider: ollama
  model: qwen2.5-coder:14b
  temperature: 0.1
  maxSteps: 15

# Tool definitions with output parsers
tools:
  check_project:
    id: fsharp.check_project
    name: Check F# Project
    command: dotnet
    args: ["build", "--no-restore", "--verbosity", "quiet"]
    projectArg: 1  # Insert project path at position 1
    description: "Type-check F# project without full build (dotnet build --no-restore)"
    categories: [fsharp, compilation]
    requiresConfirmation: false
    outputParsers:
      errors:
        type: lineMatch
        pattern: ": error "
        ignoreCase: true
        
  build:
    id: fsharp.build
    name: Build F# Project
    command: dotnet
    args: ["build"]
    projectArg: 1
    configArg: ["--configuration"]
    description: "Build F# project with dotnet build"
    categories: [fsharp, compilation]
    requiresConfirmation: false
    outputParsers:
      errors:
        type: lineMatch
        pattern: ": error "
        ignoreCase: true
      warnings:
        type: lineMatch
        pattern: ": warning "
        ignoreCase: true

  format:
    id: fsharp.format
    name: Format F# Code
    command: fantomas
    args: []
    pathArg: 0
    description: "Format F# code using Fantomas"
    categories: [fsharp, formatting]
    requiresConfirmation: true

  test:
    id: fsharp.test
    name: Run F# Tests
    command: dotnet
    args: ["test"]
    projectArg: 1
    description: "Run F# tests with dotnet test"
    categories: [fsharp, testing]
    requiresConfirmation: false
    outputParsers:
      testResults:
        type: regex
        pattern: "Passed:\\s*(\\d+).*Failed:\\s*(\\d+).*Skipped:\\s*(\\d+)"
        ignoreCase: true
        groups: [passed, failed, skipped]

  fsi:
    id: fsharp.fsi
    name: Run F# Interactive
    command: dotnet
    args: ["fsi"]
    scriptArg: 1
    description: "Run F# script in F# Interactive"
    categories: [fsharp, repl]
    requiresConfirmation: false

# Prompt template - replaces the .prompt file
prompt:
  # Workflow instructions for the agent
  workflow: |
    Follow this workflow:
    1. **Understand**: Read relevant files, understand the module structure
    2. **Plan**: Think through the changes needed
    3. **Implement**: Make targeted edits using file.modify or file.write
    4. **Check**: Use fsharp.check_project to type-check without full build
    5. **Build**: Use fsharp.build to compile and catch errors
    6. **Format**: Use fsharp.format (Fantomas) to ensure proper formatting
    7. **Test**: Run tests with fsharp.test if applicable
    8. **Interactive**: Use fsharp.fsi to experiment with code snippets

  # Available tools (auto-generated from tools section, but can be customized)
  availableTools: |
    - `fsharp.check_project` - Type-check F# project (dotnet build --no-restore)
    - `fsharp.build` - Build F# project
    - `fsharp.format` - Format code with Fantomas
    - `fsharp.test` - Run tests (dotnet test)
    - `fsharp.fsi` - Run F# script in F# Interactive

  # Language best practices
  bestPractices: |
    - Use the |> pipeline operator for data transformations
    - Prefer immutable data (records, discriminated unions)
    - Use pattern matching extensively
    - Use Option<'T> instead of null
    - Use Result<'T,'E> for error handling
    - Prefer function composition over classes
    - Use computation expressions for async, result, etc.
    - Keep functions small and focused
    - Use type inference - don't over-annotate

  # Language syntax reminders
  syntaxReminders: |
    - Significant whitespace - indentation matters!
    - let bindings: `let x = 5`
    - Function definition: `let add x y = x + y`
    - Pattern matching: `match x with | Some v -> v | None -> 0`
    - Discriminated unions: `type Shape = Circle of float | Rectangle of float * float`
    - Records: `type Person = { Name: string; Age: int }`
    - Async: `async { let! result = asyncOp() return result }`
    - Pipeline: `[1;2;3] |> List.map (fun x -> x * 2) |> List.sum`

  # Project structure guidance
  projectStructure: |
    - .fsproj files define F# projects
    - File order matters in F# - dependencies must come first
    - Use namespaces and modules for organization
    - Tests often use Expecto, FsUnit, or Unquote
```

### Output Parser Types

Output parsers allow structured extraction from CLI tool output. This is how we replace the hardcoded `ParseMsBuildErrors()` and `ParseTestResults()` functions with declarative config:

| Type | Purpose | Example |
|------|---------|---------|
| `lineMatch` | Extract lines containing a pattern | Errors: `: error ` |
| `regex` | Extract named groups from regex | Test results: `Passed: 5, Failed: 2` |
| `json` | Parse JSON output | Tools that output `--format json` |
| `exitCode` | Just check exit code | Simple pass/fail |

```yaml
outputParsers:
  # LineMatch: collect all lines matching pattern
  errors:
    type: lineMatch
    pattern: ": error "
    ignoreCase: true
  
  # Regex: extract named capture groups
  testResults:
    type: regex
    pattern: "Passed:\\s*(\\d+).*Failed:\\s*(\\d+).*Skipped:\\s*(\\d+)"
    groups: [passed, failed, skipped]
  
  # JSON: parse entire output as JSON
  coverage:
    type: json
    path: "$.summary.lineCoverage"  # JSONPath to extract
  
  # ExitCode: simple success/failure
  success:
    type: exitCode
    successCodes: [0]
```

### Haskell Example

```yaml
# agents/languages/haskell.yaml
language:
  id: haskell
  name: Haskell
  extensions: [".hs", ".lhs"]
  projectFiles: ["package.yaml", "*.cabal", "stack.yaml"]
  
capabilities:
  - haskell-coding
  - coding
  - functional-programming

priority: 10

tools:
  build:
    id: haskell.build
    command: stack
    args: ["build"]
    fallback:
      command: cabal
      args: ["build"]
    description: "Build Haskell project"
    categories: [haskell, compilation]
    
  test:
    id: haskell.test
    command: stack
    args: ["test"]
    fallback:
      command: cabal
      args: ["test"]
    description: "Run Haskell tests"
    categories: [haskell, testing]
    
  format:
    id: haskell.format
    command: ormolu
    args: ["--mode", "inplace"]
    pathArg: -1  # path is last arg
    fallback:
      command: fourmolu
      args: ["--mode", "inplace"]
    description: "Format Haskell code with Ormolu"
    categories: [haskell, formatting]
    requiresConfirmation: true
    
  lint:
    id: haskell.lint
    command: hlint
    args: []
    pathArg: 0
    description: "Lint Haskell code with HLint"
    categories: [haskell, linting]
    
  typecheck:
    id: haskell.typecheck
    command: stack
    args: ["build", "--fast", "--no-run-tests"]
    fallback:
      command: ghc
      args: ["-fno-code"]
    description: "Type-check without full compilation"
    categories: [haskell, compilation]
    
  repl:
    id: haskell.ghci
    command: stack
    args: ["ghci"]
    fallback:
      command: ghci
      args: []
    description: "Start GHCi REPL"
    categories: [haskell, repl]

bestPractices: |
  - Haskell is lazy by default - be mindful of space leaks
  - Use strict data types when accumulating (`!` or `{-# UNPACK #-}`)
  - Prefer `Text` over `String` for text processing
  - Use `Maybe` and `Either` for error handling, not exceptions
  - Derive instances with `deriving` or `DerivingStrategies`
  - Use newtypes for type safety with zero runtime cost
  - Prefer `foldl'` over `foldl` (strict left fold)
  - Use `lens` or `optics` for nested record access
  - Keep IO at the edges, pure logic in the core

syntaxReminders: |
  - Type signature: `add :: Int -> Int -> Int`
  - Function definition: `add x y = x + y`
  - Pattern matching: `case x of Just v -> v; Nothing -> 0`
  - Guards: `abs x | x < 0 = -x | otherwise = x`
  - Where clause: `f x = y + 1 where y = x * 2`
  - Do notation: `do { x <- action; return (x + 1) }`
  - List comprehension: `[x * 2 | x <- [1..10], even x]`
  - Type classes: `class Eq a where (==) :: a -> a -> Bool`
  - Instance: `instance Eq Bool where True == True = True; ...`
  - Record syntax: `data Person = Person { name :: String, age :: Int }`
```

## Implementation

### LanguageSpecialistAgent

```csharp
public class LanguageSpecialistAgent : IAgent
{
    private readonly LanguageConfig _config;
    private readonly IReActExecutor _reactExecutor;
    private readonly IToolRegistry _toolRegistry;
    private readonly ILlmProviderRegistry _llmRegistry;
    private readonly IPromptRegistry _promptRegistry;
    private readonly ILogger _logger;

    public string AgentId => $"{_config.Language.Id}-coding";
    
    public AgentMetadata Metadata => new(
        Name: $"{_config.Language.Name} Coding Agent",
        Description: $"Configurable coding agent for {_config.Language.Name}",
        Capabilities: _config.Capabilities.ToList(),
        Priority: _config.Priority,
        Languages: [_config.Language.Id],
        Provider: "ollama",
        Model: "qwen2.5-coder:14b",
        Temperature: 0.1f,
        Tools: _config.Tools.Select(t => t.Value.Id).ToList(),
        Tags: ["coding", _config.Language.Id, "agentic", "configurable"]);

    public async Task<AgentOutput> ExecuteAsync(AgentContext context, CancellationToken ct = default)
    {
        // Build prompt from template + config
        var systemPrompt = _promptRegistry.Render("language-coding", new
        {
            language = _config.Language.Name,
            bestPractices = _config.BestPractices,
            syntaxReminders = _config.SyntaxReminders,
            prompt = context.Prompt,
            workspacePath = context.WorkspacePath,
            ragContext = context.RagContext,
        });

        // Get tools from registry
        var tools = GetConfiguredTools();

        // Execute via ReAct
        var result = await _reactExecutor.ExecuteAsync(
            systemPrompt,
            tools,
            _llmRegistry.GetDefaultProvider()!,
            new ReActOptions
            {
                MaxSteps = 15,
                WorkingDirectory = context.WorkspacePath,
            },
            ct);

        return BuildOutput(result);
    }
}
```

### Generic Language Prompt Template

```handlebars
---
description: System prompt for configurable language coding agent
---
# {{language}} Development Task

## Objective
{{prompt}}

## Instructions
You are an expert {{language}} developer using shell-based tools to make code changes.

Follow this workflow:
1. **Understand**: Read relevant files, understand the codebase structure
2. **Plan**: Think through the changes needed
3. **Implement**: Make targeted edits using file.modify or file.write
4. **Check**: Use {{language}}.build or {{language}}.typecheck to verify
5. **Format**: Use {{language}}.format to ensure proper formatting
6. **Test**: Run tests with {{language}}.test if applicable

## {{language}} Best Practices
{{bestPractices}}

## {{language}} Syntax Reminders
{{syntaxReminders}}

{{#if workspacePath}}
## Workspace
Working directory: {{workspacePath}}
{{/if}}

{{#if ragContext}}
## Relevant Code Context (from RAG)
{{ragContext}}
{{/if}}
```

### LanguageToolFactory

```csharp
public static class LanguageToolFactory
{
    public static void RegisterToolsFromConfig(
        IToolRegistry registry,
        IProcessRunner processRunner,
        LanguageConfig config,
        ILogger logger)
    {
        foreach (var (name, toolDef) in config.Tools)
        {
            var tool = CreateToolFromDefinition(toolDef, processRunner, logger);
            registry.RegisterTool(tool);
        }
        
        logger.LogInformation("Registered {Count} {Language} tools from config",
            config.Tools.Count, config.Language.Name);
    }

    private static ToolDefinition CreateToolFromDefinition(
        ToolConfig toolDef,
        IProcessRunner runner,
        ILogger logger)
    {
        return new ToolDefinition
        {
            ToolId = toolDef.Id,
            Name = toolDef.Name ?? toolDef.Id,
            Description = toolDef.Description,
            Categories = toolDef.Categories,
            RequiresConfirmation = toolDef.RequiresConfirmation,
            InputSchema = GenerateInputSchema(toolDef),
            Handler = async (input, ct) =>
            {
                var args = BuildArgs(toolDef, input);
                var result = await runner.RunAsync(
                    toolDef.Command,
                    args,
                    new ProcessOptions { WorkingDirectory = input.WorkingDirectory },
                    ct);

                // Try fallback if primary command failed
                if (result.ExitCode != 0 && toolDef.Fallback != null)
                {
                    args = BuildArgs(toolDef.Fallback, input);
                    result = await runner.RunAsync(
                        toolDef.Fallback.Command,
                        args,
                        new ProcessOptions { WorkingDirectory = input.WorkingDirectory },
                        ct);
                }

                return result.ExitCode == 0
                    ? ToolResult.Ok(new { output = result.StandardOutput })
                    : ToolResult.Fail(result.StandardError);
            },
        };
    }
}
```

## Directory Structure

```
agents/
  languages/
    elm.yaml
    haskell.yaml
    rust.yaml        # Future
    ocaml.yaml       # Future
    scala.yaml       # Future
    clojure.yaml     # Future
```

## Benefits

1. **Declarative**: New languages added via YAML, no C# code needed
2. **Consistent**: All language agents behave the same way
3. **Maintainable**: Single `ConfigurableLanguageAgent` to maintain
4. **Extensible**: Easy to add new tool types, fallbacks, etc.
5. **Testable**: Config validation catches errors early
6. **Hot-reloadable**: Just like markdown agents

## Migration Path

1. Keep existing hardcoded agents (Python, TypeScript, Go, F#)
2. Implement `ConfigurableLanguageAgent` for new languages
3. Optionally migrate existing agents to YAML config later

## Configuration Validation

```csharp
public record LanguageConfigValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors);

public static LanguageConfigValidationResult Validate(LanguageConfig config)
{
    var errors = new List<string>();
    
    if (string.IsNullOrEmpty(config.Language.Id))
        errors.Add("language.id is required");
    
    if (config.Tools.Count == 0)
        errors.Add("At least one tool must be defined");
    
    foreach (var (name, tool) in config.Tools)
    {
        if (string.IsNullOrEmpty(tool.Command))
            errors.Add($"Tool '{name}' must have a command");
    }
    
    return new(errors.Count == 0, errors);
}
```

## Prerequisites Discovery

The configurable agent can also generate prerequisites documentation:

```csharp
public string GeneratePrerequisitesMarkdown(LanguageConfig config)
{
    var sb = new StringBuilder();
    sb.AppendLine($"## {config.Language.Name}");
    sb.AppendLine();
    sb.AppendLine("| Tool | Command | Purpose |");
    sb.AppendLine("|------|---------|---------|");
    
    foreach (var (_, tool) in config.Tools)
    {
        sb.AppendLine($"| `{tool.Command}` | `{tool.Command} {string.Join(" ", tool.Args)}` | {tool.Description} |");
        if (tool.Fallback != null)
        {
            sb.AppendLine($"| `{tool.Fallback.Command}` (fallback) | ... | Alternative |");
        }
    }
    
    return sb.ToString();
}
```

## Next Steps

1. Implement `LanguageConfig` model classes
2. Implement YAML parsing with YamlDotNet
3. Create `ConfigurableLanguageAgent`
4. Create `LanguageToolFactory`
5. Add language configs for Elm and Haskell
6. Add tests for config validation
7. Update TOOL-PREREQUISITES.md generation
