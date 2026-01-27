# Agents and Prompts Specification

**Status:** Reference Documentation
**Created:** 2026-01-28

This document specifies the agent definition format, prompt templates, and operational patterns used in Aura.

## 1. Agent Definitions

### 1.1 Markdown Agent Format

Agents are defined in markdown files in the `agents/` directory:

```markdown
# Agent Name

Description paragraph.

## Metadata

- **Priority**: 50
- **Reflection**: true

## Capabilities

- capability-one
- capability-two

## Languages

- csharp
- python

## Tags

- tag1
- tag2

## Tools

- file.read
- file.write
- shell.execute

## System Prompt

You are an expert...

The prompt can use Handlebars placeholders:
- {{context.WorkspacePath}}
- {{context.RepositoryPath}}
```

### 1.2 Metadata Fields

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Priority` | int | 50 | Lower = more specialized, selected first |
| `Reflection` | bool | false | Enable self-critique |
| `ReflectionPrompt` | string | null | Custom reflection template |
| `ReflectionModel` | string | null | Model for reflection (default: same as agent) |

### 1.3 Capabilities (Fixed Vocabulary)

Core capabilities used for routing:

| Capability | Description |
|------------|-------------|
| `coding` | General code generation |
| `testing` | Test writing |
| `documentation` | Documentation generation |
| `analysis` | Business/requirements analysis |
| `code-review` | Code review |
| `build-fixer` | Fix build errors |
| `refactoring` | Code refactoring |
| `ingestion` | Content indexing |

Language-specific capabilities:

| Capability | Description |
|------------|-------------|
| `csharp-coding` | C# development |
| `python-coding` | Python development |
| `typescript-coding` | TypeScript development |
| `rust-coding` | Rust development |
| `go-coding` | Go development |
| `fsharp-coding` | F# development |

---

## 2. Built-in Agents

### 2.1 Markdown Agents (Hot-Reloadable)

| File | Agent | Capabilities |
|------|-------|--------------|
| `coding-agent.md` | Polyglot Coding Agent | coding, testing |
| `chat-agent.md` | Chat Agent | general chat |
| `build-fixer-agent.md` | Build Fixer | build-fixer |
| `business-analyst-agent.md` | Business Analyst | analysis |
| `code-review-agent.md` | Code Review Agent | code-review |
| `documentation-agent.md` | Documentation Agent | documentation |
| `rust-coding-agent.md` | Rust Coding Agent | rust-coding |
| `issue-enrichment-agent.md` | Issue Enrichment | analysis |

### 2.2 Hardcoded Agents (Compile-Time)

| Agent | Class | Capabilities | Reason |
|-------|-------|--------------|--------|
| Roslyn Coding Agent | `RoslynCodingAgent` | csharp-coding | Requires Roslyn APIs |
| Fallback Ingester | `FallbackIngesterAgent` | ingestion | Fallback for unknown files |

### 2.3 Language Specialist Agents (YAML-Configured)

Located in `agents/languages/`:

| File | Language | Capabilities |
|------|----------|--------------|
| `csharp.yaml` | C# | csharp-coding, dotnet |
| `python.yaml` | Python | python-coding |
| `typescript.yaml` | TypeScript | typescript-coding |
| `rust.yaml` | Rust | rust-coding |
| `go.yaml` | Go | go-coding |
| `fsharp.yaml` | F# | fsharp-coding |
| `haskell.yaml` | Haskell | haskell-coding |
| `elm.yaml` | Elm | elm-coding |
| `powershell.yaml` | PowerShell | powershell-coding |
| `bash.yaml` | Bash | bash-coding |
| `terraform.yaml` | Terraform | terraform-coding |
| `bicep.yaml` | Bicep | bicep-coding |
| `arm.yaml` | ARM Templates | arm-coding |

---

## 3. Language Configuration Format (YAML)

```yaml
language:
  id: python
  name: Python
  extensions: [".py", ".pyx", ".pyi"]
  projectFiles: ["pyproject.toml", "setup.py", "requirements.txt"]

capabilities:
  - python-coding
  - coding
  - testing

priority: 20

agent:
  type: specialist  # or 'hardcoded' for C#
  provider: ollama
  model: qwen2.5-coder:14b
  temperature: 0.2
  maxSteps: 15

# CLI-based tools
tools:
  build:
    id: python.build
    name: Build Python Project
    command: python
    args: ["-m", "build"]
    description: "Build Python package"
    categories: [python, build]

  test:
    id: python.pytest
    name: Run Pytest
    command: pytest
    args: ["-v"]
    description: "Run Python tests with pytest"
    categories: [python, testing]

  format:
    id: python.black
    name: Format with Black
    command: black
    args: ["."]
    description: "Format Python code with Black"
    categories: [python, formatting]

  lint:
    id: python.ruff
    name: Lint with Ruff
    command: ruff
    args: ["check", "."]
    description: "Lint Python code with Ruff"
    categories: [python, linting]
```

---

## 4. Prompt Templates

### 4.1 Template Location

Templates are in the `prompts/` directory with `.prompt` extension.

### 4.2 Frontmatter Format

```handlebars
---
description: Template purpose
ragQueries:
  - "search query 1"
  - "search query 2"
tools:
  - file.read
  - file.write
  - shell.execute
---

Template body with {{placeholders}}
```

### 4.3 Built-in Templates

| Template | Purpose |
|----------|---------|
| `step-execute.prompt` | Execute a workflow step |
| `step-review.prompt` | Review step output |
| `workflow-plan.prompt` | Generate workflow steps |
| `workflow-enrich.prompt` | Enrich workflow with context |
| `workflow-chat-analyzed.prompt` | Chat with analyzed context |
| `workflow-chat-planning.prompt` | Chat during planning |
| `story-decompose.prompt` | Decompose using pattern |
| `task-execute.prompt` | Execute a task |
| `agent-reflection.prompt` | Self-critique template |
| `react-retry.prompt` | Retry after failure |
| `roslyn-coding.prompt` | C# specific coding |
| `step-execute-documentation.prompt` | Documentation step |

### 4.4 Template Variables

Common variables available in templates:

| Variable | Description |
|----------|-------------|
| `{{stepName}}` | Current step name |
| `{{stepDescription}}` | Step description |
| `{{issueTitle}}` | Story/issue title |
| `{{analysis}}` | Analyzed context |
| `{{revisionFeedback}}` | Previous rejection feedback |
| `{{context.WorkspacePath}}` | Workspace path |
| `{{context.RepositoryPath}}` | Git repository path |

### 4.5 Handlebars Helpers

Available helpers:

```handlebars
{{#if condition}}...{{/if}}
{{#each items}}{{this}}{{/each}}
{{#unless condition}}...{{/unless}}
```

---

## 5. Operational Patterns

### 5.1 Pattern Location

Patterns are in the `patterns/` directory with `.md` extension.

### 5.2 Pattern Structure

```markdown
# Pattern: Pattern Name

Description of when to use this pattern.

> **Language-specific guidance**: Use `aura_pattern(operation: "get", name: "pattern-name", language: "csharp")` to load with language overlay.

## When to Use

- Use case 1
- Use case 2

## Prerequisites

- Prerequisite 1
- Prerequisite 2

## Universal Workflow

### 1. Phase Name

Description of phase.

```
aura_tool(operation: "...", ...)
```

### 2. Next Phase

...

## Verification

How to verify the pattern completed successfully.
```

### 5.3 Built-in Patterns

| Pattern | Purpose |
|---------|---------|
| `generate-tests.md` | Generate comprehensive unit tests |

### 5.4 Language Overlays

Patterns can have language-specific overlays:

```
patterns/
├── generate-tests.md        # Base pattern
├── csharp/
│   └── generate-tests.md    # C# overlay
└── python/
    └── generate-tests.md    # Python overlay
```

When loaded with a language, overlays are merged with the base pattern.

---

## 6. Agent Reflection

### 6.1 Reflection Flow

When `Reflection: true`:

1. Agent generates initial response
2. Response passed to reflection template
3. Reflection critiques the response
4. If issues found, agent revises
5. Final response returned

### 6.2 Reflection Template

```handlebars
---
description: Agent self-critique
---

Review your response for:
1. Accuracy - Is the information correct?
2. Completeness - Did you address all requirements?
3. Best practices - Does the code follow conventions?
4. Edge cases - Are error cases handled?

Original prompt: {{originalPrompt}}

Your response:
{{response}}

If you find issues, explain what needs to change.
If the response is good, respond with "APPROVED".
```

---

## 7. Tool Declaration in Prompts

### 7.1 Tool Reference Format

In prompt frontmatter:

```yaml
tools:
  - file.read
  - file.write
  - aura.refactor
  - roslyn.list_classes
```

### 7.2 Tool Categories

| Category | Tools |
|----------|-------|
| File | `file.read`, `file.write`, `file.modify`, `file.list`, `file.exists` |
| Shell | `shell.execute` |
| Aura | `aura.refactor`, `aura.generate`, `aura.validate` |
| Roslyn | `roslyn.list_projects`, `roslyn.list_classes`, `roslyn.get_class_info`, `roslyn.find_usages`, `roslyn.validate_compilation` |
| Graph | `graph.find_callers`, `graph.find_implementations`, `graph.get_type_members` |
| Git | `git.status`, `git.commit`, `git.branch` |
| Patterns | `pattern.list`, `pattern.load` |
| Agent | `spawn_subagent`, `check_token_budget` |

### 7.3 Tool Selection Guidance

The `step-execute.prompt` template includes guidance:

```markdown
### Tool Selection: Use Semantic Tools for C#

For **C# projects**, prefer Aura and Roslyn semantic tools over basic file manipulation:

| Task | Use This | NOT This |
|------|----------|----------|
| Rename symbol | `aura.refactor(operation: "rename")` | Find/replace |
| Add method | `aura.generate(operation: "method")` | `file.modify` |
| Create type | `aura.generate(operation: "create_type")` | `file.write` |
```

---

## 8. Guardian Definitions

### 8.1 Guardian Format (YAML)

```yaml
id: ci-guardian
name: CI/CD Guardian
version: 1
description: Monitors CI pipelines and creates stories for failures

triggers:
  - type: schedule
    cron: "*/15 * * * *"
  - type: webhook
    events:
      - workflow_run.completed

detection:
  sources:
    - type: github_actions
      branches: [main, develop]
  
  failure_analysis:
    parse_logs: true
    identify_failing_tests: true
    identify_build_errors: true

workflow:
  title: "Fix CI: {failure_summary}"
  description: |
    CI pipeline failed on {branch}.
    **Error:** {error_summary}
  
  suggested_capability: build-fixer
  priority: high
  
  context_gathering:
    - failure_logs
    - recent_commits
    - affected_files
```

### 8.2 Built-in Guardians

| Guardian | Purpose |
|----------|---------|
| `ci-guardian.yaml` | Monitor CI/CD failures |
| `test-coverage-guardian.yaml` | Monitor test coverage drops |
| `documentation-guardian.yaml` | Monitor documentation freshness |

---

## 9. Agent Selection Algorithm

```csharp
public IAgent? SelectAgentForCapability(string capability, IReadOnlyList<string>? languages = null)
{
    var candidates = _agents.Values
        .Where(a => a.Metadata.Capabilities.Contains(capability))
        .ToList();
    
    if (languages?.Count > 0)
    {
        // Prefer language-specific agents
        var languageMatch = candidates
            .Where(a => a.Metadata.Languages.Count == 0  // Polyglot
                     || a.Metadata.Languages.Intersect(languages).Any())
            .ToList();
        
        if (languageMatch.Count > 0)
            candidates = languageMatch;
    }
    
    // Sort by priority (lower = more specialized)
    return candidates
        .OrderBy(a => a.Metadata.Priority)
        .FirstOrDefault();
}
```

---

## 10. Provider Configuration

### 10.1 Provider Specification

In agent definition:

```markdown
## Provider

ollama/qwen2.5-coder:14b

(or just)

openai/gpt-4o

(or just)

azure
```

### 10.2 Provider Resolution

1. Parse provider/model from agent definition
2. If no model specified, use provider's default
3. If no provider specified, use `ollama`

```csharp
var parts = providerSpec.Split('/');
var provider = parts[0];
var model = parts.Length > 1 ? parts[1] : null;
```
