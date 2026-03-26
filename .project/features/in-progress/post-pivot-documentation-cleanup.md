# Spec: Post-Pivot Documentation Cleanup

## Goal

Align all documentation with the personal knowledge MCP server architecture.
After the pivot (ADR-025), ~60% of docs reference deleted concepts (agents,
workflows, stories, extension, guardians, patterns, orchestration). This
spec defines exactly what to delete, archive, and rewrite.

## Principles

1. **Delete** artifacts with no historical value (build outputs, stale scripts)
2. **Archive** completed feature specs to `.project/archive/v1-agent-era/` (they document decisions that led to the pivot — historical value)
3. **Rewrite** docs that users/developers will actually read (README, getting-started, user-guide, reference)
4. **Keep** ADRs (immutable by convention), coding standards, test strategy

---

## Phase 1: Delete stale directories and files

Remove from disk and git:

### Directories
- `extension/` — VS Code extension build artifacts, screenshots, .vsix files (24K+ files; source already git rm'd but dir persists)
- `src/Aura.AppHost/` — Aspire host bin/obj remnants
- `samples/` — Language samples for deleted agent system (fsharp/, go/, python/, rust/, typescript/)

### Files
- `docs/Services/StoryProgressCommentService.md` — deleted service
- `docs/demo-playbook.md` — agent demo playbook
- `docs/demo-script-40min.md` — agent demo script
- `docs/TOOL-PREREQUISITES.md` — prereqs for deleted language agent tools
- `docs/mcp-tools/aura_docs.md` — deleted MCP tool
- `docs/user-guide/patterns.md` — documents deleted pattern system
- `setup/install-windows.ps1` — old manual installer superseded by Inno Setup
- `setup/install-mac.sh` — old manual macOS installer (needs full rewrite for launchd)
- `scripts/Validate-Features.ps1` — validates old feature lifecycle conventions
- `.project/processes/add-specialist-coding-agent.md` — deleted agent process

---

## Phase 2: Archive completed feature specs

Move to `.project/archive/v1-agent-era/features/`:

### Agent system features (move all)
- `agent-discovery.md`, `agent-reflection.md`, `agentic-execution-v2.md`
- `agents.md`, `hardcoded-agents.md`, `tool-execution-for-agents.md`
- `generic-language-agent.md`, `unified-capability-model.md`
- `coding-agent-v2-mcp-validation.md`, `chat-context-modes.md`
- `code-aware-chat.md`

### Workflow/Story features (move all)
- `story-model.md`, `story-chat.md`, `pattern-driven-stories.md`
- `pattern-driven-ux-gaps.md`, `unified-wave-orchestration.md`
- `workflow-pr-creation.md`, `workflow-step-debugging-ux.md`
- `workflow-verification-stage.md`, `sdd-artifact-export.md`

### Extension features (move all)
- `extension.md`, `bundled-extension.md`, `assisted-workflow-ui.md`
- `code-graph-status-panel.md`, `index-health-dashboard.md`

### Orchestration features (move all)
- `orchestrator-parallel-dispatch.md`, `copilot-cli-parity.md`
- `remove-internal-agent-architecture.md`

### Infrastructure features that are now irrelevant
- `aspire-architecture.md`, `composable-modules.md`
- `cloud-llm-providers.md`, `streaming-responses.md`
- `structured-output.md`, `react-post-code-validation.md`
- `tech-debt-stringly-typed-code.md`, `technical-debt-cleanup.md`

### Move from in-progress/
- `layered-fleet-architecture.md`, `internationalization.md`, `condensed-export.md`

### Move from proposed/
- `agent-capability-comparison.md`, `orchestrator-ghcp-integration.md`
- `pattern-catalog.md`, `web-ui.md`, `quick-actions-bar.md`
- `azure-devops-jira-integration.md`

Also archive:
- `.project/analysis/` — all 4 files (2026-02-07 reviews, SDD methodology, gap analysis)
- `.project/explore/` — both files (SELF-BOOTSTRAPPING.md, thoughts.md)
- `.project/sessions/` — old session data

---

## Phase 3: Rewrite key documentation

### `README.md` (root)
Rewrite to describe the personal knowledge MCP server:
- What Aura is (one paragraph)
- Architecture diagram (text)
- Quick start (install Ollama, install PostgreSQL, run service)
- MCP tools table (10 tools)
- Supported content types
- Configuration (embedding provider, batch size)
- Development (build, test, deploy)

### `CHANGELOG.md`
Add a `## v2.0.0 — Personal Knowledge MCP Server` entry summarizing the pivot.

### `docs/README.md`
Rewrite documentation index for current architecture.

### `docs/getting-started/installation.md`
Rewrite for: Windows installer, macOS manual install, prerequisites (Ollama, PostgreSQL).

### `docs/getting-started/quick-start.md`
Rewrite for: register a workspace, index it, search via Copilot.

### `docs/getting-started/first-run.md`
Rewrite for: verify health, check MCP tools, index first folder.

### `docs/user-guide/mcp-tools.md`
Rewrite with the current 10 tools, their operations, and examples.

### `docs/user-guide/indexing.md`
Update to describe all 7 ingestors and supported file types.

### `docs/user-guide/use-cases.md`
Rewrite with personal knowledge use cases: code search, PDF research, tax receipts, config search.

### `docs/user-guide/cheat-sheet.md`
Rewrite with current REST endpoints and MCP tool quick reference.

### `docs/configuration/settings.md`
Rewrite for current config: embedding provider, Ollama, OpenAI, RAG options, watcher options.

### `docs/configuration/llm-providers.md`
Rewrite for: Ollama (local embeddings) and OpenAI (hosted embeddings) only.

### `docs/mcp-tools/api-reference.md`
Rewrite with current 10 MCP tools and their JSON schemas.

### `docs/troubleshooting/common-issues.md`
Update for current architecture (remove agent/extension references).

### `.project/reference/api-cheat-sheet.md`
Rewrite with current REST endpoints only.

### `.project/reference/architecture-quick-reference.md`
Rewrite for current architecture.

### `.project/reference/functional-patterns.md`
Archive (describes agent execution patterns).

### `.project/features/README.md`
Rewrite feature index for post-pivot features only.

### `.github/prompts/aura.create-release.prompt.md`
Update to remove extension build steps.

### `.github/prompts/aura.onboard.prompt.md`
Rewrite for MCP workspace onboarding (not agent/workflow onboarding).

---

## Phase 4: Verify

- `dotnet build` passes
- `dotnet test` passes (388 tests)
- `dotnet format --verify-no-changes` passes
- No files reference deleted concepts without [ARCHIVED] or [HISTORICAL] context
- `git status` is clean after commit
