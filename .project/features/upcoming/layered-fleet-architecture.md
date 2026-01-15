# Layered Fleet Architecture

> **Status:** Draft  
> **Created:** 2026-01-16  
> **Author:** Aura Team

## Overview

This specification defines a two-fleet agent architecture for Aura:

1. **Guardian Fleet** — Repository-scoped agents that maintain invariants on the main branch
2. **Development Fleet** — Worktree-scoped agents that complete tasks in isolated branches

The architecture supports polyglot codebases with tiered language capabilities, providing enhanced tooling for supported languages while maintaining useful functionality for all languages.

---

## Goals

1. **Autonomous maintenance** — Keep documentation, tests, and build health without human intervention
2. **Isolated development** — Dev agents work in worktrees without affecting main
3. **Quality gates** — Guardians enforce standards before code reaches main
4. **Polyglot support** — Work with any language; excel with supported languages
5. **Composable agents** — Mix and match agents based on project needs

## Non-Goals

1. Replacing human code review judgment
2. Fully autonomous deployment to production
3. Supporting every programming language with deep tooling

---

## Architecture

### System Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                           AURA CORE                                 │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │                     UNIFIED AURA TOOLS                      │   │
│  │  aura_navigate │ aura_refactor │ aura_generate │ aura_validate  │
│  │  aura_search   │ aura_inspect  │ aura_workflow │ aura_architect │
│  └─────────────────────────┬───────────────────────────────────┘   │
│                            │                                        │
│  ┌─────────────────────────▼───────────────────────────────────┐   │
│  │                  LANGUAGE ADAPTER LAYER                     │   │
│  ├────────┬────────┬────────┬────────┬────────┬───────────────┤   │
│  │  C#    │  F#    │ Python │  TS    │  Go    │  TreeSitter   │   │
│  │ Roslyn │  FCS   │  rope  │ts-morph│ gopls  │  (fallback)   │   │
│  └────────┴────────┴────────┴────────┴────────┴───────────────┘   │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
                                  │
            ┌─────────────────────┴─────────────────────┐
            │                                           │
            ▼                                           ▼
┌───────────────────────────────┐       ┌───────────────────────────────┐
│       GUARDIAN FLEET          │       │     DEVELOPMENT FLEET         │
│     (Repository Scope)        │       │     (Worktree Scope)          │
├───────────────────────────────┤       ├───────────────────────────────┤
│                               │       │                               │
│  • Doc Guardian               │       │  ┌─────────┐  ┌─────────┐    │
│  • Build Guardian             │       │  │Worktree │  │Worktree │    │
│  • Test Guardian              │       │  │   A     │  │   B     │    │
│  • Dependency Guardian        │       │  ├─────────┤  ├─────────┤    │
│  • Security Guardian          │       │  │• Coding │  │• Coding │    │
│  • API Compat Guardian        │       │  │• Docs   │  │• Review │    │
│                               │       │  │• Review │  │• Test   │    │
│  Scope: main branch           │       │  └─────────┘  └─────────┘    │
│  Lifetime: Persistent         │       │                               │
│  Trigger: Events, schedules   │       │  Scope: feature branch        │
│                               │       │  Lifetime: Per-workflow       │
└───────────────────────────────┘       │  Trigger: User/workflow       │
                                        └───────────────────────────────┘
```

### Fleet Comparison

| Aspect | Guardian Fleet | Development Fleet |
|--------|----------------|-------------------|
| **Scope** | Repository (main branch) | Worktree (feature branch) |
| **Lifetime** | Persistent, always active | Per-workflow, ephemeral |
| **Trigger** | Events, schedules, PR gates | User requests, workflow steps |
| **Goal** | Maintain invariants | Complete tasks |
| **State** | Stateless (check/fix) | Stateful (workflow context) |
| **Autonomy** | Fully autonomous | Human-in-the-loop |
| **Output** | Fixes, reports, gates | Code, docs, PRs |

---

## Language Support Tiers

### Tier 1: Full Semantic Support

Languages with compiler API access enabling precise refactoring, navigation, and generation.

| Language | Adapter | Capabilities |
|----------|---------|--------------|
| **C#** | Roslyn | Full refactoring, code generation, semantic analysis, implement interface, extract method, rename with overload awareness |
| **F#** | FSharp.Compiler.Service | Full refactoring, type-aware generation, semantic analysis |
| **TypeScript** | ts-morph / TS Compiler API | Full refactoring, type-aware navigation, JSDoc generation |

### Tier 2: LSP-Based Support

Languages with mature Language Server Protocol implementations.

| Language | Adapter | Capabilities |
|----------|---------|--------------|
| **Go** | gopls | Rename, find references, implement interface, go to definition |
| **Rust** | rust-analyzer | Rename, find references, semantic analysis, trait implementation |
| **Python** | pylsp + jedi + rope | Rename, find references, extract method (limited type info) |

### Tier 3: TreeSitter + LLM Fallback

Any language with TreeSitter grammar support. Uses structural parsing + LLM for intelligent transforms.

| Capability | Implementation |
|------------|----------------|
| **Navigation** | TreeSitter AST queries for definitions, references |
| **Refactoring** | TreeSitter parse → LLM suggests edit → validate syntax |
| **Generation** | LLM generates code, TreeSitter validates structure |

### Capability Matrix by Tier

| Capability | Tier 1 | Tier 2 | Tier 3 |
|------------|:------:|:------:|:------:|
| Rename symbol | ✅ Precise | ✅ Precise | ⚠️ LLM-assisted |
| Find references | ✅ Complete | ✅ Complete | ⚠️ Heuristic |
| Find implementations | ✅ Semantic | ✅ LSP | ❌ Limited |
| Extract method | ✅ Full | ⚠️ Limited | ⚠️ LLM-assisted |
| Change signature | ✅ Full | ⚠️ Limited | ⚠️ LLM-assisted |
| Implement interface | ✅ Full | ✅ LSP | ⚠️ LLM-assisted |
| Code generation | ✅ Type-aware | ⚠️ Template | ⚠️ LLM |
| Semantic analysis | ✅ Full | ✅ LSP | ❌ Syntax only |

---

## Guardian Fleet Specification

### Guardian Interface

```csharp
public interface IGuardian
{
    /// <summary>Unique identifier for this guardian.</summary>
    string GuardianId { get; }
    
    /// <summary>Human-readable name.</summary>
    string Name { get; }
    
    /// <summary>Languages this guardian applies to. Empty = all languages.</summary>
    string[] ApplicableLanguages { get; }
    
    /// <summary>Events that trigger this guardian.</summary>
    GuardianTrigger[] Triggers { get; }
    
    /// <summary>Check if the invariant holds.</summary>
    Task<GuardianCheckResult> CheckAsync(
        GuardianContext context, 
        CancellationToken ct);
    
    /// <summary>Attempt to fix violations.</summary>
    Task<GuardianFixResult> FixAsync(
        IReadOnlyList<Violation> violations,
        GuardianContext context,
        CancellationToken ct);
}
```

### Guardian Context

```csharp
public record GuardianContext(
    /// <summary>Repository root path.</summary>
    string RepositoryPath,
    
    /// <summary>Files changed (for incremental checks). Null = check all.</summary>
    IReadOnlyList<string>? ChangedFiles,
    
    /// <summary>What triggered this check.</summary>
    TriggerEvent Trigger,
    
    /// <summary>Languages detected in the repository.</summary>
    IReadOnlyList<LanguageInfo> DetectedLanguages,
    
    /// <summary>Available tool capabilities for each language.</summary>
    IReadOnlyDictionary<string, LanguageTier> LanguageCapabilities,
    
    /// <summary>Pull request context (if triggered by PR).</summary>
    PullRequestContext? PullRequest
);
```

### Guardian Result Types

```csharp
public record GuardianCheckResult(
    bool IsCompliant,
    IReadOnlyList<Violation> Violations,
    GuardianMetrics Metrics
);

public record Violation(
    string RuleId,
    string FilePath,
    int? LineNumber,
    string Message,
    ViolationSeverity Severity,
    bool AutoFixable,
    string? SuggestedFix
);

public record GuardianFixResult(
    bool Success,
    IReadOnlyList<string> ModifiedFiles,
    IReadOnlyList<Violation> RemainingViolations,
    string? ErrorMessage
);

public enum ViolationSeverity { Info, Warning, Error, Critical }
```

### Standard Guardians

#### Documentation Guardian

```yaml
id: doc-guardian
name: Documentation Guardian
description: Ensures public APIs have documentation

applicable_languages: [csharp, fsharp, typescript, python, go, rust]

invariant:
  csharp: "All public/protected members have XML documentation"
  fsharp: "All public members have XML documentation"
  typescript: "All exported members have JSDoc comments"
  python: "All public functions/classes have docstrings"
  go: "All exported identifiers have comments"
  rust: "All pub items have /// documentation"

triggers:
  - type: file_changed
    patterns: ["**/*.cs", "**/*.fs", "**/*.ts", "**/*.py", "**/*.go", "**/*.rs"]
  - type: pull_request
    events: [opened, synchronize]
  - type: schedule
    cron: "0 6 * * 1"  # Weekly Monday 6 AM

check:
  strategy: incremental  # Only check changed files on file_changed trigger
  
fix:
  tier_1_languages:  # C#, F#, TypeScript
    - Use aura_inspect to get member signature and context
    - Use aura_search to find similar documented members
    - Use aura_generate to create documentation
  
  tier_2_3_languages:  # Go, Rust, Python
    - Use aura_inspect for basic signature info
    - Use LLM to generate idiomatic documentation
    - Apply via text edit with syntax validation

escalate:
  after_failures: 3
  action: create_issue
  assign_to: code_owner
```

#### Build Guardian

```yaml
id: build-guardian
name: Build Guardian
description: Ensures the codebase compiles without errors

applicable_languages: []  # All languages

triggers:
  - type: pull_request
    events: [opened, synchronize]
  - type: push
    branches: [main, develop]
  - type: schedule
    cron: "*/30 * * * *"  # Every 30 minutes

check:
  polyglot_strategy:
    - Detect project types in repository
    - Run appropriate build command for each:
        dotnet: "dotnet build"
        node: "npm run build"
        python: "python -m py_compile + mypy"
        go: "go build ./..."
        rust: "cargo check"
        
fix:
  common_errors:
    missing_import:
      tier_1: Use aura_refactor(add_using)
      tier_2_3: LLM suggests import, apply text edit
    
    type_mismatch:
      tier_1: Use aura_refactor(change_signature) or aura_navigate to understand
      tier_2_3: LLM analyzes error, suggests fix
    
    missing_implementation:
      tier_1: Use aura_generate(implement_interface)
      tier_2_3: LLM generates implementation

escalate:
  after_failures: 2
  action: block_pr
  message: "Build guardian could not auto-fix: {errors}"
```

#### Test Coverage Guardian

```yaml
id: test-guardian
name: Test Coverage Guardian
description: Maintains minimum test coverage thresholds

triggers:
  - type: pull_request
    events: [opened, synchronize]
  - type: schedule
    cron: "0 2 * * *"  # Daily 2 AM

thresholds:
  overall_minimum: 70
  new_code_minimum: 80
  critical_paths: 90  # Marked with [Critical] or similar

check:
  - Run test suite with coverage
  - Parse coverage report
  - Calculate delta for changed files
  - Identify under-covered new code

fix:
  strategy: generate_test_stubs
  
  tier_1_languages:
    - Use aura_inspect to get method signatures
    - Use aura_search to find similar test patterns
    - Use aura_generate to create test file/class
    - Generate test stubs for uncovered methods
  
  tier_2_3_languages:
    - Use LLM to generate test stubs
    - Follow project's existing test patterns
    - Validate generated tests compile/parse

output:
  pr_comment: |
    ## Test Coverage Report
    
    | Metric | Value | Threshold | Status |
    |--------|-------|-----------|--------|
    | Overall | {overall}% | {overall_min}% | {status} |
    | New Code | {new_code}% | {new_code_min}% | {status} |
    
    {generated_tests_summary}
```

#### Dependency Guardian

```yaml
id: dependency-guardian
name: Dependency Guardian
description: Monitors dependencies for security and architecture

triggers:
  - type: file_changed
    patterns: 
      - "**/package.json"
      - "**/packages.lock.json"
      - "**/*.csproj"
      - "**/go.mod"
      - "**/Cargo.toml"
      - "**/requirements.txt"
      - "**/pyproject.toml"
  - type: schedule
    cron: "0 3 * * *"  # Daily 3 AM

checks:
  security:
    - Scan for known CVEs (via OSV, GitHub Advisory)
    - Flag deprecated packages
    - Check for typosquatting
    
  architecture:
    - Detect circular project dependencies (Tier 1 only)
    - Flag unexpected cross-layer dependencies
    - Warn on dependency version conflicts

fix:
  security:
    auto_update: patch  # Only auto-update patch versions
    major_minor: create_issue
    
  architecture:
    circular: suggest_extraction
    layer_violation: block_pr
```

---

## Development Fleet Specification

### Development Agent Interface

```csharp
public interface IDevAgent
{
    /// <summary>Unique identifier for this agent type.</summary>
    string AgentId { get; }
    
    /// <summary>Role this agent plays in development.</summary>
    DevAgentRole Role { get; }
    
    /// <summary>Execute the agent within a workflow context.</summary>
    Task<AgentOutput> ExecuteAsync(
        DevAgentContext context,
        CancellationToken ct);
}

public enum DevAgentRole
{
    Coding,
    Documentation,
    Review,
    Testing,
    Planning,
    Research
}
```

### Development Agent Context

```csharp
public record DevAgentContext(
    /// <summary>The workflow this agent is executing within.</summary>
    Workflow Workflow,
    
    /// <summary>The worktree path (isolated branch).</summary>
    string WorktreePath,
    
    /// <summary>Current step being executed.</summary>
    WorkflowStep CurrentStep,
    
    /// <summary>Outputs from previous steps.</summary>
    IReadOnlyList<StepOutput> PreviousOutputs,
    
    /// <summary>Languages in this worktree.</summary>
    IReadOnlyList<LanguageInfo> Languages,
    
    /// <summary>User's original prompt/request.</summary>
    string UserPrompt,
    
    /// <summary>RAG context from codebase search.</summary>
    string? RagContext,
    
    /// <summary>Code graph context for Tier 1 languages.</summary>
    string? CodeGraphContext
);
```

### Standard Development Agents

#### Coding Agent

```yaml
id: coding-agent
role: Coding
description: Implements code changes based on requirements

capabilities:
  tier_1_languages:
    - Use aura_navigate to understand existing code
    - Use aura_refactor for precise transformations
    - Use aura_generate for new code with correct patterns
    - Use aura_validate to verify changes compile
    
  tier_2_languages:
    - Use LSP-based navigation
    - Use aura_refactor for supported operations
    - Use LLM + validation for complex generation
    
  tier_3_languages:
    - Use TreeSitter for structural understanding
    - Use LLM for code generation
    - Validate syntax after generation

workflow_integration:
  - Receives requirements from planning step
  - Outputs: modified files, summary of changes
  - Triggers: documentation agent for new public APIs
```

#### Documentation Agent

```yaml
id: documentation-agent
role: Documentation
description: Creates and updates documentation

capabilities:
  - Generate API documentation (language-appropriate format)
  - Update README files
  - Create/update architecture docs
  - Write inline code comments

language_formats:
  csharp: XML documentation comments
  fsharp: XML documentation comments
  typescript: JSDoc comments
  python: Docstrings (Google/NumPy style)
  go: Godoc comments
  rust: Rustdoc comments (///)
  markdown: Standard markdown

integration:
  - Triggered after coding agent adds public APIs
  - Uses aura_inspect (Tier 1) or LLM analysis (Tier 2/3)
  - Outputs: documentation additions/updates
```

#### Review Agent

```yaml
id: review-agent
role: Review
description: Reviews code changes before PR

capabilities:
  - Analyze code changes for issues
  - Check against project conventions
  - Verify tests exist for new code
  - Suggest improvements

enhanced_for_tier_1:
  - Use aura_navigate to check call sites affected
  - Use aura_inspect to verify interface contracts
  - Use aura_validate for compile-time checks
  
all_languages:
  - LLM-based code review
  - Pattern matching for common issues
  - Style guide compliance

output:
  - Review comments (inline)
  - Summary of concerns
  - Approval recommendation
```

---

## Worktree-Guardian Integration

### Local Guardians

Guardians can run in "local mode" within a worktree for fast feedback:

```yaml
local_guardian_mode:
  triggers:
    - pre_commit  # Git hook
    - file_save   # Editor integration
    
  behavior:
    auto_fix: true  # Fix issues automatically
    notify: inline  # Show in editor, not PR
    scope: changed_files_only
    
  guardians:
    - doc-guardian (local mode)
    - style-guardian (local mode)
    - build-guardian (local mode, current file only)
```

### PR Handoff

When dev agents complete work and open a PR:

```
┌─────────────────────────────────────────────────────────────┐
│                     PR HANDOFF FLOW                         │
└─────────────────────────────────────────────────────────────┘

1. DEV FLEET: Work complete in worktree
   │
   ├─► Local guardians have been running (pre-commit)
   └─► Self-review by review-agent

2. PR OPENED: worktree → main
   │
   └─► Repository guardians activated:
       │
       ├─► doc-guardian: Full check (not just changed files)
       ├─► build-guardian: Full build
       ├─► test-guardian: Full test suite + coverage
       └─► dependency-guardian: Check new deps

3. GUARDIAN RESULTS:
   │
   ├─► All pass → PR approved by guardians
   │
   ├─► Fixable violations:
   │   ├─► Guardian auto-fixes
   │   └─► Pushes fix commit to PR branch
   │
   └─► Unfixable violations:
       ├─► PR blocked
       ├─► Comments added explaining issues
       └─► Dev fleet notified to address

4. PR MERGED:
   │
   ├─► Worktree cleaned up
   ├─► Dev fleet released
   └─► Guardians continue monitoring main
```

---

## Configuration

### Repository Configuration

```yaml
# .aura/config.yaml
version: 1

repository:
  main_branch: main
  worktree_directory: .worktrees

languages:
  # Override auto-detection if needed
  primary: [csharp, typescript]
  detected: auto

guardians:
  enabled:
    - doc-guardian
    - build-guardian
    - test-guardian
    - dependency-guardian
    
  disabled:
    - api-compat-guardian  # Not needed for internal project
    
  config:
    doc-guardian:
      severity: warning  # Don't block, just warn
      
    test-guardian:
      thresholds:
        overall: 75
        new_code: 85

dev_fleet:
  default_agents:
    - coding-agent
    - documentation-agent
    - review-agent
    
  auto_assign: true  # Assign agents when workflow created

local_guardians:
  enabled: true
  pre_commit:
    - doc-guardian
    - style-guardian
  file_save:
    - build-guardian
```

### Per-Language Configuration

```yaml
# .aura/languages/csharp.yaml
language: csharp
tier: 1
adapter: roslyn

documentation:
  style: xml
  required_on: [public, protected]
  
refactoring:
  prefer_tools: true  # Use aura_refactor over LLM edits
  
generation:
  constructor_style: primary  # C# 12 primary constructors
  null_handling: annotations  # Use nullable annotations
```

```yaml
# .aura/languages/python.yaml
language: python
tier: 2
adapter: rope+jedi

documentation:
  style: google  # or numpy, sphinx
  required_on: [public]
  
type_hints:
  enforce: true
  tool: mypy  # or pyright
```

---

## API Endpoints

### Guardian API

```
GET  /api/guardians
     List all configured guardians

GET  /api/guardians/{id}
     Get guardian details and status

POST /api/guardians/{id}/check
     Trigger manual check
     Body: { "scope": "full" | "changed", "changedFiles": [...] }

POST /api/guardians/{id}/fix
     Attempt to fix violations
     Body: { "violations": [...], "dryRun": false }

GET  /api/guardians/{id}/history
     Get check/fix history
```

### Worktree API

```
POST /api/worktrees
     Create worktree with dev fleet
     Body: { "repositoryPath": "...", "branchName": "...", "agents": [...] }

GET  /api/worktrees
     List active worktrees

GET  /api/worktrees/{id}
     Get worktree details including assigned agents

DELETE /api/worktrees/{id}
       Clean up worktree and release agents
```

### Fleet Status API

```
GET  /api/fleet/guardians/status
     Status of all repository guardians

GET  /api/fleet/dev/status
     Status of all active dev agent assignments

GET  /api/fleet/languages
     Detected languages and their capability tiers
```

---

## Implementation Phases

### Phase 1: Foundation (Current + Near-term)

- [x] Unified aura tools (8 tools)
- [x] Worktree-based workflows
- [ ] Language adapter interface
- [ ] C# Roslyn adapter (refactoring)
- [ ] Python rope adapter (basic)

### Phase 2: Guardian Framework

- [ ] Guardian interface and registry
- [ ] Doc Guardian (C#, Python)
- [ ] Build Guardian (multi-language)
- [ ] Local guardian mode (pre-commit hooks)
- [ ] PR integration

### Phase 3: Language Expansion

- [ ] TypeScript adapter (ts-morph)
- [ ] Go adapter (gopls LSP)
- [ ] Rust adapter (rust-analyzer LSP)
- [ ] TreeSitter fallback adapter

### Phase 4: Full Fleet

- [ ] Test Guardian with coverage tracking
- [ ] Dependency Guardian with CVE scanning
- [ ] Dev fleet orchestration
- [ ] Guardian ↔ Dev fleet communication

### Phase 5: Intelligence

- [ ] Architectural analysis (aura_architect)
- [ ] Cross-codebase patterns
- [ ] Learning from fixes (which auto-fixes work)
- [ ] Predictive maintenance

---

## Success Metrics

| Metric | Target |
|--------|--------|
| Auto-fix success rate | >80% of fixable violations |
| PR first-pass rate | >70% pass guardians on first push |
| Documentation coverage | Maintain >90% on public APIs |
| Build break duration | <10 minutes to auto-fix or escalate |
| Developer satisfaction | Guardians helpful, not annoying |

---

## Open Questions

1. **Guardian conflict resolution** — What if two guardians suggest conflicting fixes?
2. **Resource limits** — How to limit guardian compute time/cost?
3. **Learning from overrides** — Should guardians learn when humans override their fixes?
4. **Cross-repo guardians** — Guardians that span multiple repositories (monorepo)?
5. **Custom guardians** — How do users define project-specific guardians?

---

## Appendix: Language Adapter Interface

```csharp
public interface ILanguageAdapter
{
    /// <summary>Language identifier (e.g., "csharp", "python").</summary>
    string LanguageId { get; }
    
    /// <summary>File extensions handled by this adapter.</summary>
    string[] FileExtensions { get; }
    
    /// <summary>Capability tier of this adapter.</summary>
    LanguageTier Tier { get; }
    
    /// <summary>Initialize adapter for a workspace.</summary>
    Task InitializeAsync(string workspacePath, CancellationToken ct);
    
    // Navigation
    Task<IReadOnlyList<Location>> FindReferencesAsync(SymbolQuery query, CancellationToken ct);
    Task<IReadOnlyList<TypeInfo>> FindImplementationsAsync(string interfaceName, CancellationToken ct);
    Task<IReadOnlyList<MemberInfo>> GetTypeMembersAsync(string typeName, CancellationToken ct);
    
    // Refactoring
    Task<RefactorResult> RenameAsync(RenameRequest request, CancellationToken ct);
    Task<RefactorResult> ExtractMethodAsync(ExtractMethodRequest request, CancellationToken ct);
    Task<RefactorResult> ChangeSignatureAsync(ChangeSignatureRequest request, CancellationToken ct);
    
    // Generation
    Task<GenerateResult> GenerateDocumentationAsync(GenerateDocRequest request, CancellationToken ct);
    Task<GenerateResult> ImplementInterfaceAsync(ImplementInterfaceRequest request, CancellationToken ct);
    
    // Validation
    Task<ValidationResult> ValidateSyntaxAsync(string filePath, CancellationToken ct);
    Task<ValidationResult> ValidateSemanticsAsync(string filePath, CancellationToken ct);
    Task<BuildResult> BuildAsync(BuildRequest request, CancellationToken ct);
    Task<TestResult> RunTestsAsync(TestRequest request, CancellationToken ct);
}

public enum LanguageTier
{
    /// <summary>Full compiler API support (C#, F#, TypeScript).</summary>
    Tier1_Full = 1,
    
    /// <summary>LSP-based support (Go, Rust, Python).</summary>
    Tier2_Lsp = 2,
    
    /// <summary>TreeSitter + LLM fallback.</summary>
    Tier3_Fallback = 3
}
```

---

## Related Documents

- [Demo Playbook](../../../docs/demo-playbook.md) — Demonstration scenarios
- [Aura Tools Consolidation](./aura-tools-consolidation.md) — The 8 unified tools
- [Roslyn Editing Tools](./roslyn-editing-tools.md) — Tier 1 C# capabilities
