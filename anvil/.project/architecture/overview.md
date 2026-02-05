---
title: Project Architecture - Anvil
description: Test harness for validating Aura agent quality through story execution
maturity: stable
---

# Project Architecture: Anvil

> Test harness for Aura. A .NET 10 CLI application for executing stories through Aura, validating generated code, and tracking regressions.

## SDD Workflow

This project uses Spec-Driven Development. Follow this workflow for any new feature:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           SDD WORKFLOW                                       │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  1. RESEARCH           2. PLAN              3. IMPLEMENT        4. VALIDATE │
│  ┌──────────┐         ┌──────────┐         ┌──────────┐        ┌──────────┐│
│  │ Context  │   ───►  │   Spec   │   ───►  │  Code    │  ───►  │  Check   ││
│  │ Gathered │         │ Created  │         │  Tests   │        │ Alignment││
│  └──────────┘         └──────────┘         └──────────┘        └──────────┘│
│                                                                             │
│  Prompts:              Prompts:             Prompts:            Prompts:    │
│  research.md           plan.md              implement.md        validate.md │
│                                                                             │
│  Output:               Output:              Output:             Output:     │
│  .project/    .project/   src/, tests/        .copilot-   │
│  research/             plans/               .project/  tracking/   │
│                                             changes/            reviews/    │
└─────────────────────────────────────────────────────────────────────────────┘
```

🔴 **Critical:** Always clear context or start a new chat between phases.

### Artifact Locations

| Phase | Output Location | Naming Convention |
|-------|-----------------|-------------------|
| Research | `.project/research/` | `{{YYYY-MM-DD}}-{topic}-research.md` |
| Plan | `.project/plans/` | `{{YYYY-MM-DD}}-{topic}-plan.md` |
| Implement | `src/`, `tests/`, `.project/changes/` | `{{YYYY-MM-DD}}-{topic}-changes.md` |
| Validate | `.project/reviews/` | `{{YYYY-MM-DD}}-{topic}-review.md` |

**Note:** Files in `.project/**` are exempt from repository linting rules.

---

## What is Anvil?

Anvil is a **test harness for Aura agent quality**:

```
Story (specification)
    → Aura generates code
    → PR/Code created (output)
    → Anvil validates (build, tests, expected files)
    → Results stored for regression tracking
```

### Purpose

When Aura agents produce incorrect, inefficient, or useless code, we don't fix the generated code directly—we **fix Aura itself** (agents, prompts, tools, patterns). Anvil provides:

- **Automated execution** of stories through Aura
- **Validation** that generated code builds, tests pass, expected files exist
- **Regression tracking** to detect when agent quality degrades
- **Multi-mode testing** (REST API, VS Code Extension, shell commands)

### Design Principles

| Principle | Implication |
|-----------|-------------|
| Black-box testing | Anvil calls Aura via HTTP/shell, no internal access |
| Story-driven | Tests are defined as human-readable story specs |
| Regression-aware | Track results over time, detect quality degradation |
| Multi-modal | Test REST API, VS Code Extension, and CLI paths |
| Extensible sources | Start with files, expand to GitHub Issues |

---

## Technology Stack

| Aspect | Choice | Rationale |
|--------|--------|-----------|
| **Language** | C# 14 / .NET 10 | Matches Aura stack |
| **CLI Framework** | System.CommandLine | Standard .NET CLI |
| **HTTP Client** | HttpClient | Calling Aura REST API |
| **VS Code Testing** | @vscode/test-electron | Extension automation |
| **Database** | SQLite (EF Core) | Regression tracking |
| **Testing** | xUnit + FluentAssertions + NSubstitute | Consistent with Aura |
| **Logging** | Serilog | Structured logging |
| **Console UI** | Spectre.Console | Rich terminal output |

---

## Folder Structure

```
anvil/
├── .sdd/                           # SDD Methodology
│   ├── philosophy.md               # SDD principles
│   ├── ADR/                        # Architecture decisions
│   ├── architecture/               # This file, principles
│   ├── coding-guidelines/          # C# conventions
│   ├── prompts/                    # Workflow prompts
│   └── templates/                  # AGENTS.md template
│
├── .project/              # SDD Workflow Artifacts
│   ├── research/                   # Research phase output
│   ├── plans/                      # Plan phase output
│   ├── changes/                    # Implementation change logs
│   └── reviews/                    # Validation review output
│
├── src/
│   ├── Anvil.Core/                 # Core Domain
│   │   ├── Stories/                # Story models
│   │   │   ├── Story.cs
│   │   │   ├── StoryContent.cs
│   │   │   └── StoryDescriptor.cs
│   │   ├── Results/                # Execution results
│   │   │   ├── StoryResult.cs
│   │   │   ├── ValidationResult.cs
│   │   │   └── TestRunResult.cs
│   │   ├── Regression/             # Regression detection
│   │   │   ├── RegressionDetector.cs
│   │   │   └── ComparisonResult.cs
│   │   └── Errors/                 # Error types
│   │       └── AnvilError.cs
│   │
│   ├── Anvil.Application/          # Application Layer
│   │   ├── Sources/                # Story sources
│   │   │   ├── IStorySource.cs
│   │   │   ├── FileStorySource.cs
│   │   │   └── GitHubIssueSource.cs
│   │   ├── Executors/              # Execution modes
│   │   │   ├── IStoryExecutor.cs
│   │   │   ├── AuraApiExecutor.cs
│   │   │   ├── VsCodeExecutor.cs
│   │   │   └── CopilotCliExecutor.cs
│   │   ├── Validators/             # Output validation
│   │   │   ├── IValidator.cs
│   │   │   ├── BuildValidator.cs
│   │   │   ├── TestValidator.cs
│   │   │   └── FileExistsValidator.cs
│   │   └── Services/
│   │       ├── StoryRunner.cs
│   │       └── RegressionService.cs
│   │
│   ├── Anvil.Infrastructure/       # Infrastructure Layer
│   │   ├── Aura/                   # Aura client
│   │   │   ├── AuraClient.cs
│   │   │   └── AuraOptions.cs
│   │   ├── VsCode/                 # VS Code testing
│   │   │   ├── VsCodeTestClient.cs
│   │   │   └── TestRunnerExtension/
│   │   ├── Persistence/            # SQLite storage
│   │   │   ├── AnvilDbContext.cs
│   │   │   └── Repositories/
│   │   ├── GitHub/                 # GitHub API (Phase 2)
│   │   │   └── GitHubClient.cs
│   │   └── Shell/                  # Shell command execution
│   │       └── ShellExecutor.cs
│   │
│   └── Anvil.Cli/                  # Presentation Layer
│       ├── Program.cs              # Entry point, DI wiring
│       ├── Commands/               # CLI command definitions
│       │   ├── RunCommand.cs
│       │   ├── ListCommand.cs
│       │   ├── CompareCommand.cs
│       │   └── ReportCommand.cs
│       └── Output/                 # Console output helpers
│           ├── ProgressRenderer.cs
│           └── ResultsTable.cs
│
├── stories/                        # Story Specifications
│   ├── README.md                   # How to write stories
│   ├── greenfield/
│   │   ├── cli-hello-world.md
│   │   ├── rest-api-basic.md
│   │   └── library-with-tests.md
│   └── brownfield/
│       ├── add-feature.md
│       └── refactor-rename.md
│
├── fixtures/                       # Test Fixtures
│   ├── sample-workspace/           # Workspace for VS Code tests
│   └── extensions/
│       └── anvil-test-runner/      # Test runner extension
│
├── tests/
│   ├── Anvil.Core.Tests/
│   ├── Anvil.Application.Tests/
│   └── Anvil.Infrastructure.Tests/
│
├── Anvil.sln
├── Directory.Build.props
└── README.md
```

---

## Execution Modes

Anvil tests Aura through three paths:

| Mode | Implementation | Description |
|------|----------------|-------------|
| **Aura API** | `AuraApiExecutor` | REST API at `localhost:5300` |
| **VS Code Extension** | `VsCodeExecutor` | Launch VS Code, automate extension |
| **Copilot CLI** | `CopilotCliExecutor` | Shell commands via `gh copilot` |

### Execution Flow

```
┌─────────────────┐
│  Story Source   │  (file or GitHub Issue)
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Story Runner   │  (orchestrates execution)
└────────┬────────┘
         │
    ┌────┴────┬──────────────┐
    ▼         ▼              ▼
┌────────┐ ┌────────┐ ┌────────────┐
│Aura API│ │VS Code │ │Copilot CLI │
└────┬───┘ └───┬────┘ └─────┬──────┘
     │         │            │
     └─────────┴────────────┘
               │
               ▼
┌─────────────────┐
│   Validators    │  (build, test, file checks)
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Store Result   │  (SQLite for regression tracking)
└─────────────────┘
```

---

## Layer Responsibilities

### Core Layer (`Anvil.Core`)

**Pure domain types, no dependencies.**

| Component | Responsibility |
|-----------|----------------|
| `Stories/` | Story models, content, descriptors |
| `Results/` | Execution result types |
| `Regression/` | Regression detection logic |
| `Errors/` | Domain error types |

### Application Layer (`Anvil.Application`)

**Use cases, orchestration.**

| Component | Responsibility |
|-----------|----------------|
| `Sources/` | Story source abstraction (file, GitHub) |
| `Executors/` | Execution mode abstraction (API, VS Code, CLI) |
| `Validators/` | Output validation (build, test, files) |
| `Services/` | Story runner, regression service |

### Infrastructure Layer (`Anvil.Infrastructure`)

**External I/O, implementations.**

| Component | Responsibility |
|-----------|----------------|
| `Aura/` | HTTP client for Aura API |
| `VsCode/` | VS Code test client, IPC |
| `Persistence/` | EF Core SQLite, repositories |
| `GitHub/` | GitHub API client (Phase 2) |
| `Shell/` | Shell command execution |

### Presentation Layer (`Anvil.Cli`)

**User interaction, DI wiring.**

| Component | Responsibility |
|-----------|----------------|
| `Commands/` | CLI command definitions |
| `Output/` | Rich console rendering |
| `Program.cs` | Entry point, DI composition root |

---

## CLI Commands (Planned)

```bash
# Run a single story
anvil run stories/greenfield/cli-hello-world.md

# Run all stories in a directory
anvil run stories/greenfield/

# Run with specific execution mode
anvil run stories/greenfield/ --mode vscode
anvil run stories/greenfield/ --mode aura-api
anvil run stories/greenfield/ --mode copilot-cli

# List available stories
anvil list

# Compare runs for regressions
anvil compare run-123 run-456

# Generate report
anvil report run-123 --format html --output ./reports/

# Database management
anvil db migrate
anvil db cleanup --keep-days 90
```

---

## Configuration

Configuration via `appsettings.json`, environment variables, or CLI arguments:

```json
{
  "Anvil": {
    "AuraBaseUrl": "http://localhost:5300",
    "Timeout": "00:01:00",
    "StoriesPath": "stories",
    "DatabasePath": "anvil.db"
  },
  "GitHub": {
    "PersonalAccessToken": null,
    "TestRepository": "aura-test/anvil-fixtures"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information"
    }
  }
}
```

See [ADR-006](../ADR/ADR-006-environment-configuration.md) for configuration precedence.

---

## Dependencies

### Core
- `Microsoft.EntityFrameworkCore.Sqlite` — Data access
- `System.CommandLine` — CLI framework
- `Spectre.Console` — Rich console output
- `Serilog` — Structured logging
- `YamlDotNet` — Story frontmatter parsing

### VS Code Testing
- `@vscode/test-electron` (npm) — VS Code automation

### GitHub Integration (Phase 2)
- `Octokit` — GitHub API client

### Testing
- `xunit` — Test framework
- `FluentAssertions` — Assertions
- `NSubstitute` — Mocking

---

## Key References

| ADR | Topic |
|-----|-------|
| [ADR-001](../ADR/ADR-001-testing.md) | Testing strategy |
| [ADR-005](../ADR/ADR-005-database-strategy.md) | SQLite for regression tracking |
| [ADR-007](../ADR/ADR-007-vscode-extension-testing.md) | VS Code extension testing |
| [ADR-008](../ADR/ADR-008-story-source-strategy.md) | Story source architecture |

---

## Next Steps

1. **Scaffold solution** — Create project structure
2. **File story source** — Parse markdown stories
3. **Aura API executor** — Basic story execution
4. **Build validator** — Verify generated code compiles
5. **SQLite storage** — Persist results
6. **First story** — `cli-hello-world` end-to-end
