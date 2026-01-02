# Unified Software Development Capability

**Status**: Draft  
**Author**: Copilot  
**Date**: 2024-12-09

## Summary

Consolidate multiple language-specific coding agents into a unified `software-development-{language}` capability model. Each language has a single capability that encompasses coding, testing, refactoring, and documentation tasks.

## Problem Statement

### Current State

We have multiple agents with overlapping capabilities:

| Agent | Capabilities |
|-------|--------------|

| RoslynCodingAgent | `csharp-coding`, `coding`, `refactoring`, `csharp-documentation`, `testing-csharp`, `testing` |
| TypeScriptCodingAgent | `typescript-coding`, `javascript-coding`, `coding` |
| PythonCodingAgent | `python-coding`, `coding` |
| GoCodingAgent | `go-coding`, `coding` |
| FSharpCodingAgent | `fsharp-coding`, `coding`, `functional-programming` |

**Issues:**

1. **Fragmentation** - Same language has multiple capabilities (`csharp-coding` vs `testing-csharp`)
2. **Inconsistency** - Some agents have testing capabilities, others don't
3. **Code duplication** - All agents follow identical ReAct pattern, differing only in tools and prompts
4. **Capability discovery** - Hard to know which capability to request

### Proposed State

Each language has ONE unified capability:

| Language | Capability | Agent Implementation |
|----------|------------|----------------------|

| C# | `software-development-csharp` | RoslynCodingAgent (class) |
| F# | `software-development-fsharp` | FSharpCodingAgent (class) or shared with C# |
| TypeScript | `software-development-typescript` | Configuration-based |
| JavaScript | `software-development-javascript` | Configuration-based |
| Python | `software-development-python` | Configuration-based |
| Go | `software-development-go` | Configuration-based |
| Rust | `software-development-rust` | Configuration-based |

## Design

### Capability Definition

A `software-development-{language}` capability includes:

- **Coding**: Implement features, fix bugs, write new code
- **Testing**: Write unit tests, run tests, fix failing tests
- **Refactoring**: Improve code structure, extract methods, rename symbols
- **Documentation**: Add/update code comments, generate API docs

### Agent Architecture

#### Tier 1: Native Agents (Class-based)

Languages with specialized tooling that require custom code:

```text
┌─────────────────────────────────────────────────────┐
│              RoslynCodingAgent                      │
│  - Roslyn semantic analysis                         │
│  - ValidateCompilationTool                          │
│  - RunTestsTool (dotnet test)                       │
│  - Full type-aware refactoring                      │
│  Capability: software-development-csharp            │
└─────────────────────────────────────────────────────┘
```

#### Tier 2: Configuration-based Agents

Languages that can use generic tools with language-specific prompts:

```yaml
# agents/languages/typescript.yaml
agent_id: typescript-developer
capability: software-development-typescript
languages: [typescript, javascript]
tools:
  - file.read
  - file.modify
  - file.write
  - shell.execute  # For npm/tsc/jest
prompt_template: prompts/software-development.prompt
prompt_variables:
  language: TypeScript
  package_manager: npm
  test_command: "npm test"
  type_check_command: "npx tsc --noEmit"
```

### Tool Mapping

| Capability | Roslyn Agent | Config-based Agent |
|------------|--------------|--------------------|

| Read code | file.read | file.read |
| Modify code | file.modify | file.modify |
| Validate compilation | ValidateCompilationTool | shell.execute (language-specific) |
| Run tests | RunTestsTool | shell.execute (language-specific) |
| Refactor | Roslyn-based tools | file.modify + LLM reasoning |

### Code Ingestion (Separate)

Code ingestion stays as a separate capability family:

| Capability | Agent | Purpose |
|------------|-------|---------|

| `code-parse:csharp` | CSharpIngesterAgent | Roslyn-based semantic parsing |
| `code-parse:*` | TreeSitterIngesterAgent | Multi-language parsing |

**Rationale**: Ingestion is stateless, LLM-free, and batch-oriented - fundamentally different from interactive development.

## Implementation Plan

### Phase 1: Unify C# Capabilities

1. Rename RoslynCodingAgent capabilities to single `software-development-csharp`
2. Remove redundant capabilities: `csharp-coding`, `testing-csharp`, `csharp-documentation`
3. Update workflow planner to use new capability name
4. Update any hardcoded capability references

### Phase 2: Configuration-based Agent Framework

1. Create `ConfigurableAgent` class that loads from YAML
2. Define YAML schema for agent configuration
3. Migrate TypeScript, Python, Go agents to configuration
4. Keep language-specific classes as fallback

### Phase 3: Prompt Unification

1. Create unified `software-development.prompt` template
2. Add language-specific sections via Handlebars partials
3. Test with each language

## Migration

### Capability Aliases (Backward Compatibility)

During transition, support old capability names:

```csharp
public static readonly Dictionary<string, string> CapabilityAliases = new()
{
    ["csharp-coding"] = "software-development-csharp",
    ["testing-csharp"] = "software-development-csharp",
    ["csharp-documentation"] = "software-development-csharp",
    ["typescript-coding"] = "software-development-typescript",
    // etc.
};
```

### Workflow Compatibility

Existing workflows with old capability names will continue to work via aliases.

## Success Criteria

1. Single capability per language for all development tasks
2. C# uses Roslyn for semantic operations
3. Other languages work via shell.execute with proper commands
4. Existing workflows continue to work
5. Reduced code duplication in agent implementations

## Open Questions

1. Should F# share Roslyn infrastructure with C# (single `software-development-dotnet`)?
2. How to handle multi-language projects (e.g., C# + TypeScript)?
3. Should we expose sub-capabilities for explicit task routing (e.g., `software-development-csharp:testing`)?

## References

- ADR-003: Agent Architecture
- STATUS.md: Current feature inventory
