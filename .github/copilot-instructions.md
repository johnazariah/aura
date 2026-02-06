# Aura Project - Copilot Instructions

> **Read First**: `.project/STATUS.md` for current project state and feature inventory.

## Primary Goal

**Build Aura as a production-ready product**, not just fix local development issues. Every change should consider:

- Will this work for all users on a clean install?
- Is the UX clear without requiring technical knowledge?
- Are edge cases handled gracefully?
- Is this documented for future maintainers?

When encountering issues, fix them properly in the product—don't apply quick workarounds that only help the current developer.

## Current Phase: Agent Quality Testing

**We are in an intensive testing phase.** Stories will be run through Aura to generate code. Your job is:

- ❌ **NOT** to fix the generated code directly
- ✅ **FIX AURA ITSELF** - improve agents, prompts, tools, and patterns so future code generation is correct

When agents produce incorrect, inefficient, or useless code:
1. Identify the root cause (wrong prompt? missing context? bad tool output?)
2. Fix the underlying Aura component (agent definition, prompt template, MCP tool, pattern)
3. The goal is that re-running the same story produces correct code without manual intervention

This means changes to:
- `agents/*.md` - Agent behavior and instructions
- `prompts/*.prompt` - Handlebars templates
- `patterns/*.md` - Step-by-step operational patterns
- `src/Aura.*/` - Tool implementations, services, APIs

## Core Principles

1. **NEVER implement without a spec** - All changes require documented requirements and context
2. **Design before coding** - Seek approval before implementing; prefer planning over spontaneous coding
3. **User controls the service** - Aura runs as a Windows Service; ask user to restart service or run `Update-LocalInstall.ps1` if needed
4. **Document all decisions** - Update `.project/STATUS.md` after significant changes
5. **Complete features properly** - Follow the ceremony in `.github/prompts/aura.complete-feature.prompt.md`
6. **Product-first mindset** - Fix issues for all users, not just the current environment
7. **Investigate failures properly** - Never dismiss errors as "transient" without evidence (see below)

## Failure Investigation Protocol

**Every failure is a bug until proven otherwise.** When Aura operations fail:

1. **Check the logs first** - `C:\ProgramData\Aura\logs\aura-YYYYMMDD.log` contains actual error details
2. **Reproduce the exact command** - Run the same command manually to see if it's truly transient
3. **Look for root cause** - Error messages often hide the real issue (e.g., "Restore failed" may mean SDK resolution failure)
4. **Fix in product, not environment** - If a workaround is needed, that's a bug in Aura

**Common pitfalls to avoid:**
- ❌ "This is a transient network issue" - Check logs; it may be a configuration bug
- ❌ "Works when I run it manually" - The service runs in a different context (LocalSystem, different PATH)
- ❌ "Just retry" - Understand WHY it failed before retrying
- ✅ Read actual error output from logs or captured output
- ✅ Consider all users: what if they have SDK workloads, different NuGet configs, etc.?

**Recent examples of "transient" issues that were real bugs:**
- `dotnet restore --source nuget.org` broke SDK workload resolution (Aspire SDK)
- HTTP client disconnects permanently failed stories instead of allowing retry

## Quick Context

Aura is a **local-first, privacy-safe AI foundation** for knowledge work. The Developer Module MVP is **complete** with full workflow UI, multi-language code indexing, and cloud LLM support.

## Key Documents (Source of Truth)

| Document | Purpose |
|----------|---------|
| `.project/STATUS.md` | **Start here** - Current state, feature inventory, open items |
| `.project/features/README.md` | Feature index with completion dates |
| `.project/reference/` | API cheat sheet, architecture, coding standards |
| `.project/adr/*.md` | Architecture decisions |
| `.github/prompts/aura.complete-feature.prompt.md` | **Ceremony for completing features** |

## Development Protocol

### What You CAN Do

```powershell
# Test API (Aura service must be running)
curl http://localhost:5300/health
curl http://localhost:5300/api/developer/workflows

# Build extension after changes
.\scripts\Build-Extension.ps1

# Run tests
.\scripts\Run-UnitTests.ps1
dotnet test

# Build solution
dotnet build
```

### What You Must NOT Do

```powershell
# NEVER run these - require elevation, ask user to run
.\scripts\Update-LocalInstall.ps1  # Rebuilds and deploys to C:\Program Files\Aura
```

### Production Environment

When user runs the **production install** (not dev mode), they use:

```powershell
# User runs this ELEVATED (Run as Administrator) - NEVER run this yourself
.\scripts\Update-LocalInstall.ps1
```

This rebuilds and deploys to `C:\Program Files\Aura`. After changes:
1. Ask user to run `Update-LocalInstall.ps1` **as Administrator**
2. Wait for them to confirm the update completed
3. Then test via `curl http://localhost:5300/health`

### Workflow

1. **Understand** - Read relevant spec/ADR before making changes
2. **Propose** - Describe planned changes and get approval
3. **Implement** - Make code changes
4. **Build** - If extension changed → `Build-Extension.ps1`
5. **Ask for update** - If server code changed → ask user to run `Update-LocalInstall.ps1` as Administrator
6. **Test** - Use curl to verify
7. **Document** - Update STATUS.md if needed

## Project Structure

```
src/
├── Aura.Foundation/          # Core: agents, LLM, RAG, data, tools
├── Aura.Module.Developer/    # Developer vertical: workflows, git, Roslyn
├── Aura.Api/                 # API host (all endpoints in Program.cs)
└── Aura.AppHost/             # Aspire orchestration

extension/src/                # VS Code extension
agents/                       # Markdown agent definitions
prompts/                      # Handlebars prompt templates
tests/                        # Unit and integration tests
.project/                     # Specs, ADRs, status, standards
```

## Coding Standards

See `.project/standards/coding-standards.md`. Key rules:

- **Strongly-typed** - No `Dictionary<string, object>` for known schemas
- **Use `nameof()`** - Never string literals for member names
- **Records for DTOs** - Immutable by default
- **Nullable reference types** - Handle nulls explicitly

## File Creation Rules

**CRITICAL**: This project uses LF line endings (see `.editorconfig`).

When creating files:
- Use **LF (`\n`)** line endings, NOT CRLF (`\r\n`)
- Use **UTF-8** encoding without BOM
- Include a **final newline**
- Follow `.editorconfig` indent rules (4 spaces for C#, 2 for JSON/YAML)

The pre-commit hook will reject files with CRLF line endings.

## When Making Changes

| Change Type | Location | Notes |
|-------------|----------|-------|
| API endpoints | `src/Aura.Api/Program.cs` | Single file for all endpoints |
| Agent behavior | `agents/*.md` or `prompts/*.prompt` | Hot-reloadable |
| Extension UI | `extension/src/providers/` | Run Build-Extension after |
| New feature | `.project/features/upcoming/` | Create spec first |
| Complete feature | `.project/features/completed/` | **Follow ceremony** (see below) |

## Blast Radius Protocol (Renames, Extract, Large Changes)

`aura_refactor` defaults to **analyze mode** (`analyze: true`). Before executing any refactoring:

1. **Always analyze first** - Call the operation without `analyze: false` to see the blast radius
2. **Review the response** - Shows related symbols discovered by naming convention and a suggested plan
3. **Present to user** - Show the blast radius (files affected, references, related symbols)
4. **Wait for confirmation** - Do NOT proceed without explicit user approval
5. **Execute with `analyze: false`** - Only after user confirms, run each step in the suggested order
6. **Build after each step** - Run `dotnet build` after each operation
7. **Sweep for residuals** - Use `grep_search` to find any missed occurrences

Example:
```
# Step 1: Analyze (default behavior)
aura_refactor(operation: "rename", symbolName: "Workflow", newName: "Story", ...)
# → Returns blast radius with 64 refs across 12 files, related: WorkflowService, IWorkflowRepository

# Step 2: Present to user and get confirmation

# Step 3: Execute after confirmation
aura_refactor(operation: "rename", symbolName: "Workflow", newName: "Story", analyze: false, ...)
```

**Never assume a refactor tool caught everything.** Roslyn workspace may be stale or incomplete.

## Feature Completion Ceremony

When a feature is complete, you MUST follow the ceremony in `.github/prompts/aura.complete-feature.prompt.md`:

1. Move file from `features/upcoming/` → `features/completed/`
2. Add header with `**Status:** ✅ Complete` and `**Completed:** YYYY-MM-DD`
3. Update `features/README.md` index with link and date
4. Commit with `docs(features): complete {feature-name}`

**Validation**: Run `.\scripts\Validate-Features.ps1` to check conventions.
This script can be installed as a pre-commit hook: `.\scripts\Validate-Features.ps1 -Install`

## MCP Tools & Aura Codebase Tools

### Tool Selection: Aura Tools vs. Text Manipulation

**CRITICAL**: When Aura MCP tools are available, prefer them over text manipulation (`replace_string_in_file`, `create_file`) for C# code changes. Aura tools understand code semantics; text tools don't.

**Polyglot support**: `aura_navigate` and `aura_refactor` support **C#, TypeScript, and Python**. Language is auto-detected from `filePath` extension, `solutionPath` (C#), or `projectPath` (TypeScript/Python). `aura_inspect` and `aura_validate` are C#-only with graceful error messages for other languages.

| Task | Use This | NOT This |
|------|----------|----------|
| Add method to class | `aura_generate(operation: "method")` | `replace_string_in_file` |
| Add property to class | `aura_generate(operation: "property")` | `replace_string_in_file` |
| Rename symbol (C#) | `aura_refactor(operation: "rename", solutionPath: ...)` | Find/replace across files |
| Rename symbol (TS) | `aura_refactor(operation: "rename", filePath: "*.ts", projectPath: ...)` | Find/replace across files |
| Create new C# type | `aura_generate(operation: "create_type")` | `create_file` |
| Implement interface | `aura_generate(operation: "implement_interface")` | Manual stub writing |
| Generate constructor | `aura_generate(operation: "constructor")` | `replace_string_in_file` |
| Generate tests | `aura_generate(operation: "tests")` | Manual test writing |
| Extract method/variable | `aura_refactor(operation: "extract_*")` | Cut/paste with text tools |
| Find usages/callers | `aura_navigate` | `grep_search` |
| Find references (TS) | `aura_navigate(operation: "references", filePath: "*.ts", projectPath: ...)` | `grep_search` |
| Explore type structure | `aura_inspect` | `read_file` + manual parsing |
| Search codebase semantically | `aura_search` | `semantic_search` or `grep_search` |

**When text manipulation IS appropriate:**
- Files in languages not yet supported by Aura tools (e.g., Go, Rust)
- Simple one-line fixes where Aura overhead isn't justified
- Files outside the solution (scripts, docs, config)
- When Aura tools fail and fallback is needed

### Aura Tool Capabilities

| Tool | Operations | Language Support | When to Use |
|------|------------|-----------------|-------------|
| `aura_search` | Semantic code search | All | First step to understand codebase |
| `aura_navigate` | `callers`, `implementations`, `derived_types`, `usages`, `references`, `definition`, `by_attribute` | C#, TypeScript, Python | Understanding code relationships |
| `aura_inspect` | `type_members`, `list_types` | C# only | Exploring class structure |
| `aura_validate` | `compilation`, `tests` | C# only | After changes to verify correctness |
| `aura_refactor` | `rename`, `extract_method`, `extract_variable`, `extract_interface`, `change_signature`, `safe_delete`, `move_type_to_file` | C#, TypeScript, Python | Semantic code transformations |
| `aura_generate` | `create_type`, `tests`, `implement_interface`, `constructor`, `property`, `method` | C# only | Adding new code elements |
| `aura_pattern` | `list`, `get` | All | Load step-by-step operational patterns |
| `aura_workflow` | `list`, `get`, `get_by_path`, `create`, `enrich`, `update_step` | All | Manage development stories |

### Workflow: Using Aura Tools Effectively

1. **Explore first**: Use `aura_search` or `aura_inspect` to understand existing code
2. **Navigate relationships**: Use `aura_navigate` to find callers, implementations, usages
3. **Generate or refactor**: Use `aura_generate` for new code, `aura_refactor` for changes
4. **Validate**: Run `aura_validate(operation: "compilation")` after changes
5. **Only then** fall back to text manipulation if Aura tools don't cover the case

### Test Generation: Hybrid Approach

`aura_generate(operation: "tests")` provides good scaffolding but may need minor fixes for complex classes.

#### What Aura Provides:

| Feature | Details |
|---------|---------|
| Testable method analysis | `analyzeOnly: true` for coverage gap discovery |
| Unique test names for overloads | Methods like `DoThing(string)` vs `DoThing(int)` get unique test names |
| Complete namespace collection | All parameter/return types get proper `using` statements |
| Return-type aware assertions | `bool` → `Assert.True`, `string` → `Assert.NotEmpty`, collections → `Assert.NotEmpty` |
| Optional compile validation | `validateCompilation: true` returns diagnostics before you see errors |
| Framework detection | xUnit, NUnit, MSTest |
| Mocking library detection | NSubstitute, Moq, FakeItEasy |

#### What Agent May Still Need to Fix:

| Issue | Fix |
|-------|-----|
| Wrong test folder placement | Move file to correct location |
| `IFileSystem` without full namespace | Add `using System.IO.Abstractions;` + use `MockFileSystem` |
| `Substitute.For<ILogger>()` | Replace with `NullLogger<T>.Instance` |
| Complex assertion logic | Enhance generated assertions for domain-specific validation |

#### Domain Knowledge Only the Agent Has:

- `IFileSystem` → `System.IO.Abstractions.IFileSystem` + `MockFileSystem` from TestingHelpers
- Loggers → `Microsoft.Extensions.Logging.Abstractions.NullLogger<T>.Instance`
- Domain-specific assertions → Must understand business logic
- Test data → Create YAML/JSON fixtures for the specific domain

**Recommended workflow:**
```
# 1. Analyze first
aura_generate(operation: "tests", target: "MyService", analyzeOnly: true)

# 2. Generate with validation
aura_generate(operation: "tests", target: "MyService", validateCompilation: true)

# 3. If compilesSuccessfully: false, fix reported diagnostics
# 4. Run tests
aura_validate(operation: "tests", projectPath: "...", filter: "MyServiceTests")
```

**For adding individual tests with full control:**
```
aura_generate(
  operation: "method",
  className: "MyServiceTests",
  methodName: "MyMethod_WhenCondition_ShouldResult",
  body: "// Arrange\nvar sut = new MyService();\n// Act\nvar result = sut.DoThing();\n// Assert\nresult.Should().BeTrue();"
)
```

This gives you Aura's file manipulation (correct insertion point, formatting) with your domain knowledge (assertions, fixtures).

### Path Resolution

When using MCP tools (prefixed with `mcp_aura_codebase_`) that require paths:

**CRITICAL**: Always use the **current workspace root**, not worktree paths or cached paths from previous context.

| Parameter | Value |
|-----------|-------|
| `solutionPath` | `c:\work\aura\Aura.sln` |
| `projectPath` | `c:\work\aura\src\{ProjectName}` |
| `workspacePath` | `c:\work\aura` |

**Common mistakes to avoid:**
- ❌ Using worktree paths like `c:\work\aura-workflow-xyz-abc123\Aura.sln`
- ❌ Inferring paths from conversation history that may reference other worktrees
- ❌ Using relative paths without anchoring to workspace root

**Before calling any `aura_*` MCP tool:**
1. Confirm the workspace root is `c:\work\aura`
2. Use absolute paths anchored to that root
3. For `solutionPath`, always use `c:\work\aura\Aura.sln`

## Container Runtime

- **Windows**: Podman
- **macOS**: OrbStack

Both are Docker-compatible for Aspire.