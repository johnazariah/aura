# Process: Adding a New Agent

This document describes how to add new agents to Aura. There are three approaches depending on your needs.

## Agent Types Overview

| Type | Config | Best For | Hot-Reload | Implementation |
|------|--------|----------|------------|----------------|
| **Markdown Agent** | `.md` file | General purpose, non-coding | ✅ Yes | Drop in `agents/` |
| **Language Specialist** | `.yaml` file | Coding with CLI tools | ✅ Yes | Drop in `agents/languages/` |
| **Hardcoded Agent** | C# class | Compiler API integration | ❌ No | Code in module |

## Quick Decision Guide

```
What does your agent need to do?
│
├─► General tasks (chat, analysis, docs) ──────► Markdown Agent
│
├─► Code in language with CLI tools ───────────► Language Specialist
│   (Python, Go, Rust, Terraform, etc.)
│
└─► Code needing compiler API ─────────────────► Hardcoded Agent
    (C# with Roslyn, complex refactoring)
```

---

# Option A: Markdown Agent

**Use for:** Chat, analysis, documentation, digestion, general-purpose tasks.

## Create the Agent

Create `agents/{agent-name}.md`:

```markdown
---
name: My Agent
description: What this agent does
capabilities:
  - chat           # or: analysis, coding, digestion, documentation, fixing, review
priority: 50       # Lower = higher priority (10=specialist, 50=general, 70=fallback)
provider: ollama
model: llama3:8b
temperature: 0.7
tools: []          # Optional: file.read, file.write, shell.execute, etc.
tags:
  - conversational
---

# System Prompt

You are an AI assistant that specializes in...

## Your Role

Explain what the agent should do.

## Guidelines

1. First guideline
2. Second guideline
3. Third guideline

## Output Format

Describe expected output format.
```

## Available Fields

| Field | Required | Description |
|-------|----------|-------------|
| `name` | ✅ | Display name |
| `description` | ✅ | What the agent does |
| `capabilities` | ✅ | What tasks it can handle |
| `priority` | ❌ | Selection priority (default: 50) |
| `provider` | ❌ | LLM provider (default: ollama) |
| `model` | ❌ | Model name (default: llama3:8b) |
| `temperature` | ❌ | Creativity (default: 0.7) |
| `tools` | ❌ | List of tool IDs |
| `tags` | ❌ | Searchable tags |

## Verify

```powershell
# Agent is auto-discovered on startup or hot-reloaded
curl http://localhost:5000/agents | jq '.[] | select(.agentId == "my-agent")'
```

---

# Option B: Language Specialist Agent

**Use for:** Coding tasks where the language has CLI tools for build, test, format, lint.

**Key principle:** Everything in ONE file - tools, prompt, best practices. No separate `.prompt` file needed.

## Create the Language Config

Create `agents/languages/{language}.yaml`:

```yaml
# Complete language definition - everything in one file

language:
  id: rust                    # Used in agent ID: rust-coding
  name: Rust                  # Display name
  extensions: [".rs"]         # File extensions
  projectFiles: ["Cargo.toml"] # Project markers

capabilities:
  - rust-coding
  - coding
  - systems-programming

priority: 10                  # 10 = specialist priority

agent:
  provider: ollama
  model: qwen2.5-coder:14b
  temperature: 0.1
  maxSteps: 15

tools:
  build:
    id: rust.build
    name: Build Rust Project
    command: cargo
    args: ["build"]
    description: "Build Rust project with Cargo"
    categories: [rust, compilation]
    requiresConfirmation: false
    outputParsers:              # Optional: structured output parsing
      errors:
        type: lineMatch
        pattern: "error["
        ignoreCase: false

  test:
    id: rust.test
    name: Run Rust Tests
    command: cargo
    args: ["test"]
    description: "Run Rust tests"
    categories: [rust, testing]
    requiresConfirmation: false
    outputParsers:
      testResults:
        type: regex
        pattern: "(\\d+) passed.*?(\\d+)? failed"
        groups: [passed, failed]

  format:
    id: rust.format
    name: Format Rust Code
    command: cargo
    args: ["fmt"]
    description: "Format Rust code with rustfmt"
    categories: [rust, formatting]
    requiresConfirmation: true    # Modifies files

  lint:
    id: rust.lint
    name: Lint Rust Code
    command: cargo
    args: ["clippy"]
    description: "Lint Rust code with Clippy"
    categories: [rust, linting]
    requiresConfirmation: false

  check:
    id: rust.check
    name: Type Check Rust
    command: cargo
    args: ["check"]
    description: "Type-check without building"
    categories: [rust, compilation]
    requiresConfirmation: false

# Prompt template - replaces separate .prompt file
prompt:
  workflow: |
    Follow this workflow:
    1. **Understand**: Read relevant files, understand the module structure
    2. **Plan**: Think through the changes needed
    3. **Implement**: Make targeted edits using file.modify or file.write
    4. **Check**: Use rust.check for fast type feedback
    5. **Build**: Use rust.build to compile
    6. **Lint**: Use rust.lint (Clippy) for best practices
    7. **Format**: Use rust.format (rustfmt) to ensure proper formatting
    8. **Test**: Run tests with rust.test if applicable

  bestPractices: |
    - Use Result<T, E> for error handling, not panics
    - Prefer &str over String for function parameters
    - Use clippy to catch common mistakes
    - Prefer iterators over manual loops
    - Use Option<T> instead of nullable types
    - ...

  syntaxReminders: |
    - Function: `fn add(x: i32, y: i32) -> i32 { x + y }`
    - Struct: `struct Point { x: f64, y: f64 }`
    - Enum: `enum Option<T> { Some(T), None }`
    - Match: `match x { Some(v) => v, None => 0 }`
    - ...

  projectStructure: |
    - Cargo.toml defines package and dependencies
    - src/main.rs for binaries, src/lib.rs for libraries
    - tests/ for integration tests
    - ...
```

## Tool Definition Fields

| Field | Required | Description |
|-------|----------|-------------|
| `id` | ✅ | Unique tool ID (e.g., `rust.build`) |
| `command` | ✅ | CLI command to run |
| `args` | ✅ | Default arguments |
| `description` | ✅ | What the tool does |
| `categories` | ✅ | Tool categories |
| `requiresConfirmation` | ❌ | Needs user approval (default: false) |
| `pathArg` | ❌ | Position for file path in args |
| `fallback` | ❌ | Alternative command if primary fails |

## Fallback Commands

For tools with alternatives (e.g., Stack vs Cabal for Haskell):

```yaml
tools:
  build:
    id: haskell.build
    command: stack
    args: ["build"]
    fallback:
      command: cabal
      args: ["build"]
    description: "Build with Stack (or Cabal)"
```

## Output Parsers

Output parsers provide structured extraction from CLI output. This replaces hardcoded parsing functions like `ParseMsBuildErrors()`:

| Type | Purpose | Example Pattern |
|------|---------|-----------------|
| `lineMatch` | Extract lines containing pattern | `: error ` |
| `regex` | Extract named capture groups | `Passed: (\d+).*Failed: (\d+)` |
| `json` | Parse JSON output | `$.summary.coverage` |
| `exitCode` | Simple pass/fail | Exit code 0 = success |

```yaml
tools:
  build:
    id: dotnet.build
    command: dotnet
    args: ["build"]
    outputParsers:
      errors:
        type: lineMatch
        pattern: ": error "
        ignoreCase: true
      warnings:
        type: lineMatch  
        pattern: ": warning "
        ignoreCase: true

  test:
    id: dotnet.test
    command: dotnet
    args: ["test"]
    outputParsers:
      testResults:
        type: regex
        pattern: "Passed:\\s*(\\d+).*Failed:\\s*(\\d+).*Skipped:\\s*(\\d+)"
        groups: [passed, failed, skipped]
```

## Verify

```powershell
# Agent and tools are auto-discovered
curl http://localhost:5000/agents | jq '.[] | select(.agentId == "rust-coding")'
curl http://localhost:5000/tools | jq '.[] | select(.toolId | startswith("rust."))'
```

---

# Option C: Hardcoded Agent

**Use for:** Complex scenarios requiring compiler APIs, custom logic, or deep integrations.

## When to Use

- Need Roslyn for C# semantic analysis
- Need rust-analyzer for advanced Rust tooling
- Custom orchestration logic
- Complex output parsing

## Step 1: Create Tools (if needed)

Create `src/Aura.Module.Developer/Tools/{Language}Tools.cs`:

```csharp
public static class RustTools
{
    public static void RegisterRustTools(
        IToolRegistry registry,
        IProcessRunner processRunner,
        ILogger logger)
    {
        registry.RegisterTool(new ToolDefinition
        {
            ToolId = "rust.analyze",
            Name = "Analyze Rust Code",
            Description = "Deep analysis using rust-analyzer",
            // ... custom implementation
        });
        
        logger.LogInformation("Registered Rust tools");
    }
}
```

## Step 2: Create Agent

Create `src/Aura.Module.Developer/Agents/{Language}CodingAgent.cs`:

```csharp
public class RustCodingAgent : IAgent
{
    public string AgentId => "rust-coding";
    
    public AgentMetadata Metadata { get; } = new(
        Name: "Rust Coding Agent",
        Description: "Advanced Rust agent with rust-analyzer integration",
        Capabilities: ["rust-coding", "coding"],
        Priority: 10,
        Languages: ["rust"],
        Provider: "ollama",
        Model: "qwen2.5-coder:14b",
        Temperature: 0.1f,
        Tools: ["file.read", "file.modify", "rust.analyze", "rust.build"],
        Tags: ["coding", "rust", "agentic"]);

    public async Task<AgentOutput> ExecuteAsync(AgentContext context, CancellationToken ct)
    {
        // Custom implementation
    }
}
```

## Step 3: Register

Update `DeveloperModule.cs`:

```csharp
private void RegisterTools(IServiceProvider sp)
{
    RustTools.RegisterRustTools(toolRegistry, processRunner, logger);
}
```

Update `DeveloperAgentProvider.cs`:

```csharp
public IEnumerable<IAgent> GetAgents()
{
    yield return new RustCodingAgent(...);
}
```

## Step 4: Add Tests

Create tests in `tests/Aura.Module.Developer.Tests/`:

- `Tools/RustToolsTests.cs`
- `Agents/RustCodingAgentTests.cs`

---

# Naming Conventions

## Agent IDs

| Pattern | Example | Description |
|---------|---------|-------------|
| `{name}-agent` | `chat-agent` | General purpose |
| `{language}-coding` | `rust-coding` | Language specialist |
| `{language}-ingester` | `csharp-ingester` | Code ingester |

## Tool IDs

| Pattern | Example | Description |
|---------|---------|-------------|
| `{language}.{action}` | `rust.build` | Language-specific |
| `{category}.{action}` | `file.read` | General tools |

## Priority Guidelines

| Priority | Use Case | Examples |
|----------|----------|----------|
| 10 | Language specialist | `rust-coding`, `python-coding` |
| 25 | Fallback coding | `generic-coding` |
| 50 | General purpose | `chat-agent` |
| 70 | Last resort fallback | `fallback-ingester` |

---

# Examples

## Existing Language Specialists

Located in `agents/languages/`:

| File | Language | Tools |
|------|----------|-------|
| `python.yaml` | Python | build, test, lint, format, typecheck |
| `typescript.yaml` | TypeScript | compile, test, lint, format |
| `go.yaml` | Go | build, test, vet, fmt |
| `elm.yaml` | Elm | build, format, test, review |
| `haskell.yaml` | Haskell | build, test, format, lint |
| `terraform.yaml` | Terraform | init, plan, apply, fmt, validate |
| `bicep.yaml` | Bicep | build, validate, lint, format |
| `arm.yaml` | ARM Templates | validate, deploy, what-if |
| `bash.yaml` | Bash | run, check, lint, format |
| `powershell.yaml` | PowerShell | run, lint, test |

## Existing Markdown Agents

Located in `agents/`:

| File | Purpose |
|------|---------|
| `chat-agent.md` | General conversation |
| `coding-agent.md` | Generic coding assistance |
| `documentation-agent.md` | Documentation tasks |
| `code-review-agent.md` | Code review |

## Existing Hardcoded Agents

| Agent | Why Hardcoded |
|-------|--------------|
| `RoslynCodingAgent` | Uses Roslyn compiler service |
| `CSharpIngesterAgent` | Complex Roslyn-based indexing |

---

# Checklist

## Markdown Agent

- [ ] Create `agents/{name}.md` with frontmatter and system prompt
- [ ] Verify agent appears in `/agents` endpoint

## Language Specialist

- [ ] Create `agents/languages/{language}.yaml`
- [ ] Include: language info, capabilities, tools, bestPractices, syntaxReminders
- [ ] Update `docs/TOOL-PREREQUISITES.md` with installation instructions
- [ ] Verify agent and tools appear in API

## Hardcoded Agent

- [ ] Create `{Language}Tools.cs` (if needed)
- [ ] Create `{Language}CodingAgent.cs`
- [ ] Create `{language}-coding.prompt` (if using prompts)
- [ ] Update `DeveloperModule.cs` to register tools
- [ ] Update `DeveloperAgentProvider.cs` to yield agent
- [ ] Create `{Language}ToolsTests.cs`
- [ ] Create `{Language}CodingAgentTests.cs`
- [ ] Update `docs/TOOL-PREREQUISITES.md`
- [ ] Build and run all tests
