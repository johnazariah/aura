# Documentation Audit: "Local-First" Claims vs. Reality

> **Date:** 2026-02-07
> **Scope:** All documentation, prompts, agents, ADRs, extension metadata
> **Purpose:** Identify claims that no longer reflect the actual architecture

---

## Executive Summary

Aura was designed as a local-first, privacy-safe system where "your data never leaves your machine." This was true during the Ollama-only era. The architecture has since evolved through three pivotal shifts:

1. **Cloud LLM providers added** (Dec 2025) — Azure OpenAI and OpenAI became supported, and Azure OpenAI is now the default in `appsettings.json`.
2. **Copilot CLI became the execution path** (Jan 2026) — Stories execute via GitHub Copilot CLI in YOLO mode, requiring internet + Copilot subscription.
3. **Internal agents removed** (Feb 6, 2026) — 7,093 lines deleted. "Copilot Chat + MCP is now the only execution path."

**The documentation has not kept pace.** At least 30 files across the codebase still claim "local-first", "no cloud uploads", "works offline", or "your data never leaves your machine." These claims are now inaccurate.

---

## 1. What Is Actually Local vs. Cloud

### Still Local

| Component | Implementation |
|-----------|---------------|
| PostgreSQL database | Local container or native install |
| RAG vector index | pgvector, stored locally |
| Code graph (Roslyn, TreeSitter) | Runs on local machine |
| Git operations | Local worktrees and branches |
| Agent definitions | Local markdown files, hot-reloadable |
| File system access | Direct, no remote |

### Now Cloud-Dependent

| Component | Implementation | Implication |
|-----------|---------------|-------------|
| **LLM inference** | Azure OpenAI (default) | All prompts — including code context — sent to Azure |
| **Code execution** | GitHub Copilot CLI (YOLO mode) | Requires internet, Copilot subscription, GitHub auth |
| **Step execution** | Copilot Chat + MCP | Copilot's cloud LLM processes all tool calls |
| **Story execution** | Full pipeline requires Copilot | No offline story execution path exists |

### The Accurate Description

Aura is a **hybrid system**: local infrastructure (database, index, code graph, git) with cloud-hosted LLM inference and code generation. Users' code context is sent to cloud LLMs as part of prompts. The system requires internet connectivity for its core workflow.

Ollama remains a *supported* LLM provider, but it is no longer the default and cannot be used for Copilot-mediated story execution.

---

## 2. Inventory of Inaccurate Claims

### Tier 1: High-Visibility / User-Facing — Must Fix

| File | Claim | Line(s) |
|------|-------|---------|
| `extension/package.json` | `"Local-first AI assistant for knowledge work"` | 4 |
| `README.md` | `"Aura is an AI coding assistant that runs on your machine"` | 7 |
| `.github/copilot-instructions.md` | `"local-first, privacy-safe AI foundation"` | ~66 |
| `.project/STATUS.md` | `"Local-First, Privacy-Safe — No cloud uploads, works offline"` | 367 |
| `.project/STATUS.md` | `"local-first, privacy-safe AI foundation"` | 10 |
| `appsettings.json` | Default provider is Azure OpenAI with hardcoded API key | — |

### Tier 2: Feature Specs — Should Fix

| File | Claim |
|------|-------|
| `features/completed/overview.md` | "local-first, privacy-safe…your data never leaves your machine" |
| `features/completed/foundation.md` | "❌ No cloud uploads ❌ No telemetry ❌ No API keys" |
| `features/completed/foundation.md` | "Your data NEVER leaves your machine." |
| `features/completed/developer-module.md` | "local-first workflow for automating software development" |
| `features/completed/semantic-indexing.md` | "100% local infrastructure…No cloud required" |
| `features/completed/llm-providers.md` | "flexibility between local-first privacy and cloud-based performance" |
| `features/completed/llm-providers.md` | "Ollama provider (default, local-first)" |
| `features/completed/unified-indexing-backend.md` | "local-first, privacy-safe AI knowledge infrastructure" |
| `features/completed/workspace-onboarding.md` | "✅ Works offline — no internet required" |
| `features/completed/workspace-onboarding.md` | "Local-First, Always" |
| `features/completed/ingester-agents.md` | "All local. No external dependencies. Works offline." |
| `features/completed/overview.md` | "local-first, privacy-safe AI foundation" |

### Tier 3: ADRs — Should Supersede

| File | Claim |
|------|-------|
| `adr/001-local-first-architecture.md` | "Aura is local-first by design. Your data never leaves your machine." |
| `adr/001-local-first-architecture.md` | "Complete privacy — User data never leaves the machine" |
| `adr/001-local-first-architecture.md` | "Works offline — No internet dependency" |
| `adr/010-no-external-agent-registration.md` | "This conflicts with our local-first architecture" |

### Tier 4: Documentation Generation Prompts — Must Fix

These are especially important because they *actively instruct AI* to propagate the local-first claim into newly generated documentation:

| File | Issue |
|------|-------|
| `prompts/step-execute-documentation.prompt` | 12 references to "local-first" — instructs generators to emphasize it as Aura's "superpower" |
| `features/completed/end-user-documentation.md` | 5 references — "Local-first is the default" |
| `features/design/end-user-docs-content-plan.md` | Plans a `local-first.md` concepts document |

### Tier 5: Demo & Marketing Materials

| File | Claim |
|------|-------|
| `docs/demo-playbook.md` | "everything runs on your machine" (talking points) |
| `docs/demo-script-40min.md` | "Your code never leaves your control" |
| `docs/getting-started/` | "Code never leaves your machine" |
| Various feature-parity specs | "Local-First / Private" as competitive advantage |

---

## 3. Specific Claims vs. Reality

| Claim | Verdict | Explanation |
|-------|---------|-------------|
| "Works offline" | ❌ **False** | Core workflow requires Copilot CLI + Azure OpenAI |
| "No API keys" | ❌ **False** | Azure OpenAI API key hardcoded in appsettings.json |
| "No cloud uploads" | ❌ **False** | Code context sent to Azure OpenAI and Copilot's LLM |
| "Your data never leaves your machine" | ❌ **False** | Prompts containing code snippets go to cloud LLMs |
| "Ollama (default)" | ❌ **False** | Default provider is AzureOpenAI |
| "100% local infrastructure" | ❌ **False** | LLM is cloud, execution is via Copilot |
| "No internet dependency" | ❌ **False** | Internet required for LLM calls and Copilot |
| "Hot-reloadable agents" | ✅ **True** | Markdown agent files reload without restart |
| "Human-in-the-loop" | ✅ **True** | Users approve/reject steps |
| "Git worktree isolation" | ✅ **True** | Each story gets its own worktree |
| "PostgreSQL runs locally" | ✅ **True** | Database is fully local |
| "Indexing is local" | ✅ **True** | Code graph and RAG index stay on-device |

---

## 4. Security Issue

`appsettings.json` contains a hardcoded Azure OpenAI API key:

```json
"ApiKey": "329098070e6e4b35a701275bd3add667"
```

This is committed to source control. Regardless of the local-first discussion, this should be moved to user secrets or environment variables.

---

## 5. Recommended Actions

### 5.1 Write ADR-024: Hybrid Architecture

Supersede ADR-001. The new ADR should:

1. Acknowledge the evolution from local-first to hybrid
2. Define what remains local (database, index, code graph, git) vs. cloud (LLM inference, Copilot execution)
3. State the privacy model clearly: "code context is sent to cloud LLMs as part of prompts"
4. Position Ollama as a supported alternative, not the default
5. Mark ADR-001 status as `Superseded by ADR-024`

### 5.2 Update Root-Level Documentation

| File | Action |
|------|--------|
| `README.md` | Rewrite opening paragraph. Keep "runs on your machine" for infrastructure but clarify LLM is cloud. |
| `extension/package.json` | Change description from "Local-first AI assistant" to something accurate. |
| `.github/copilot-instructions.md` | Update the tagline and Quick Context section. |
| `.project/STATUS.md` | Update Quick Summary (line 10) and Principles section (line 367). |
| `appsettings.json` | Remove hardcoded API key. Default to Ollama if preserving any local-first positioning, or blank if not. |

### 5.3 Fix Documentation Generation Prompts

`prompts/step-execute-documentation.prompt` actively instructs AI to describe Aura as local-first. This is the most insidious issue — it causes all *future* generated documentation to propagate the outdated claim. Update the prompt to reflect the hybrid architecture.

### 5.4 Update Completed Feature Specs (Batch)

These are historical documents, so a light touch is appropriate. Add a note at the top of affected specs:

```markdown
> **Note (2026-02):** This spec was written when Aura used local-only LLM inference.
> The architecture has since evolved to a hybrid model with cloud LLM providers.
> See ADR-024 for current architecture.
```

### 5.5 Suggested New Tagline

Instead of "local-first, privacy-safe AI foundation", consider:

- **"AI-powered development assistant with local code intelligence"** — emphasizes what IS local (indexing, code graph) without overclaiming
- **"AI coding assistant that understands your codebase"** — focuses on value proposition, avoids architecture claims
- **"Your codebase, indexed locally. AI-powered, cloud-accelerated."** — honest about both sides

---

## 6. What NOT to Change

- **ADR-001** should not be deleted or edited — it's a historical decision record. Add `Status: Superseded by ADR-024` at the top.
- **Completed feature specs** are historical records. Add a note but don't rewrite them.
- **The Ollama provider code** should stay — some users genuinely want local inference.
- **The "local infrastructure" claims** about PostgreSQL, pgvector, code graph, and git are still accurate and should be preserved.
