---
title: "ADR-015: Build and Test Invocation"
status: "Accepted"
date: "2026-01-30"
authors: "Anvil Team"
tags: ["architecture", "validation", "languages"]
supersedes: ""
superseded_by: ""
---

# ADR-015: Build and Test Invocation

## Status

Accepted

## Context

Anvil validates that generated code builds and tests pass. Stories target multiple languages:

- C# / F#
- Python
- TypeScript
- Go
- Rust
- etc.

Each language has different build/test commands. The question: where does this knowledge live?

**Key insight:** Aura already maintains language definitions in `agents/languages/*.yaml` that specify build, test, and lint commands for each language. This is the source of truth.

## Decision

**Anvil delegates build/test command knowledge to Aura.** Stories specify only the target language; Aura knows how to validate that language.

### Story Format

Stories declare language, not validation commands:

```yaml
---
id: cli-hello-world
title: CLI Hello World
language: csharp  # or fsharp, python, typescript, go, rust
category: greenfield
---
```

### Validation Flow

```
1. Anvil tells Aura: "Validate worktree at /path"
2. Aura detects language from project files (or story metadata)
3. Aura runs appropriate build/test commands from language definition
4. Aura returns: { success: true/false, output: "...", errors: [...] }
5. Anvil records result
```

### Aura Language Definitions

Aura's `agents/languages/csharp.yaml` (example):

```yaml
name: csharp
extensions: [".cs", ".csproj", ".sln"]
build:
  command: dotnet build
  success_exit_code: 0
test:
  command: dotnet test
  success_exit_code: 0
lint:
  command: dotnet format --verify-no-changes
  success_exit_code: 0
```

### Validation API

Anvil calls Aura's validation endpoint:

```
POST /api/developer/validate
{
  "worktreePath": "/path/to/.worktrees/story-abc123",
  "language": "csharp",  // optional, auto-detect if omitted
  "checks": ["build", "test"]  // which validations to run
}

Response:
{
  "success": true,
  "results": [
    {
      "check": "build",
      "success": true,
      "duration": "PT5.2S",
      "output": "Build succeeded.\n0 Warning(s)\n0 Error(s)"
    },
    {
      "check": "test",
      "success": true,
      "duration": "PT12.1S",
      "output": "Passed! 42 tests"
    }
  ]
}
```

### Fallback: Direct Invocation

If Aura's validation API is unavailable, Anvil can shell out using known defaults:

```csharp
public static class LanguageDefaults
{
    public static readonly IReadOnlyDictionary<string, LanguageCommands> Commands = new Dictionary<string, LanguageCommands>
    {
        ["csharp"] = new("dotnet build", "dotnet test"),
        ["fsharp"] = new("dotnet build", "dotnet test"),
        ["python"] = new(null, "pytest"),
        ["typescript"] = new("npm run build", "npm test"),
        ["go"] = new("go build ./...", "go test ./..."),
        ["rust"] = new("cargo build", "cargo test"),
    };
}

public record LanguageCommands(string? BuildCommand, string? TestCommand);
```

This is a fallback—primary path is through Aura API.

### Why Delegate to Aura?

| Reason | Benefit |
|--------|---------|
| **Single source of truth** | Language definitions in one place |
| **Consistency** | Same commands Aura uses for code gen |
| **Extensibility** | Add languages in Aura, Anvil automatically works |
| **No duplication** | Don't maintain language knowledge in two places |

### Story Validation Section

Stories can specify which checks to run:

```yaml
---
id: cli-hello-world
language: csharp
validation:
  - build
  - test
  # - lint  # optional
---
```

Default if not specified: `[build, test]`

## Consequences

**Positive**

- **POS-001**: Single source of truth for language commands (Aura)
- **POS-002**: Stories are simple—just specify language
- **POS-003**: Adding new languages doesn't require Anvil changes
- **POS-004**: Consistent with how Aura generates code
- **POS-005**: Fallback allows offline validation

**Negative**

- **NEG-001**: Depends on Aura validation API
- **NEG-002**: Fallback may drift from Aura's definitions
- **NEG-003**: Network round-trip for validation

## Alternatives Considered

### Alternative 1: Hardcoded Per Language

- **Description**: Switch on language in Anvil, run known commands
- **Rejection Reason**: Duplicates Aura's language definitions

### Alternative 2: Story-Defined Commands

- **Description**: Each story specifies build/test commands
- **Rejection Reason**: Verbose, error-prone, duplicates knowledge

### Alternative 3: Auto-Detect from Project Files

- **Description**: Find `*.csproj`, `package.json`, etc.
- **Rejection Reason**: Still need command mapping; Aura already does this

## Implementation Notes

- **IMP-001**: Primary: call Aura's `/api/developer/validate` endpoint
- **IMP-002**: Fallback: `LanguageDefaults` dictionary for offline mode
- **IMP-003**: Stories specify `language:` and optionally `validation:` checks
- **IMP-004**: Default validation checks: `[build, test]`
- **IMP-005**: Consider syncing fallback from Aura's language YAMLs at build time

## Aura API Requirements

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/developer/validate` | POST | Run build/test/lint on a worktree |

Request body:
```json
{
  "worktreePath": "string",
  "language": "string (optional)",
  "checks": ["build", "test", "lint"]
}
```

## References

- [Aura Language Definitions](../../agents/languages/)
- [ADR-010: Workspace Isolation](ADR-010-workspace-isolation.md)
- [ADR-008: Story Source Strategy](ADR-008-story-source-strategy.md)
