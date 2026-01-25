# Agent Capability Comparison: Aura vs. Industry Tools

> **Status:** 📋 Unplanned
> **Created:** 2026-01-23
> **Author:** Aura Team
> **Category:** Analysis / Roadmap

## Overview

This document compares Aura's agent execution capabilities with leading AI coding CLI tools:
- **GitHub Copilot CLI** (`gh copilot`)
- **Claude Code** (Anthropic's coding agent)
- **Cursor** (AI-first IDE)
- **Aider** (open-source pair programming)

The goal is to identify where Aura excels, where it lags, and what capabilities to prioritize.

---

## Executive Summary

| Capability | Aura | GH Copilot CLI | Claude Code | Cursor | Aider |
|------------|:----:|:--------------:|:-----------:|:------:|:-----:|
| ReAct Loop | ✅ | ⚠️ | ✅ | ✅ | ✅ |
| Multi-Agent | ✅ | ❌ | ❌ | ❌ | ❌ |
| Sub-Agent Spawning | ✅ | ❌ | ⚠️ | ❌ | ❌ |
| Token Budget Awareness | ✅ | ❌ | ⚠️ | ❌ | ⚠️ |
| Retry Loops | ✅ | ❌ | ✅ | ⚠️ | ✅ |
| Semantic Code Search | ✅ | ⚠️ | ✅ | ✅ | ⚠️ |
| Code Graph Navigation | ✅ | ❌ | ❌ | ⚠️ | ❌ |
| Workflow Planning | ✅ | ❌ | ⚠️ | ❌ | ❌ |
| Human-in-the-Loop | ✅ | ✅ | ✅ | ✅ | ✅ |
| Local-First / Private | ✅ | ❌ | ❌ | ❌ | ✅ |
| File Editing UX | ⚠️ | ⚠️ | ✅ | ✅ | ✅ |
| Web Browsing | ❌ | ❌ | ✅ | ❌ | ❌ |
| Memory Across Sessions | ❌ | ❌ | ✅ | ⚠️ | ⚠️ |

**Legend:** ✅ Full support | ⚠️ Partial/Limited | ❌ Not supported

---

## Detailed Comparison

### 1. Reasoning Pattern (ReAct Loop)

**What it is:** The Think → Act → Observe cycle that allows agents to reason through complex tasks.

| Tool | Implementation | Notes |
|------|----------------|-------|
| **Aura** | Full ReActExecutor with configurable steps, token tracking, budget warnings | Steps logged, observable, debuggable |
| **GH Copilot CLI** | Single-turn inference | No multi-step reasoning visible |
| **Claude Code** | Full agentic loop | Polished UX, streaming output |
| **Cursor** | Composer mode with multi-step | Tightly integrated with editor |
| **Aider** | Chat-based with tool use | Transparent reasoning |

**Aura advantage:** Observable steps, structured output mode, token tracking per step.

---

### 2. Multi-Agent Architecture

**What it is:** Specialized agents for different tasks (coding, review, analysis, documentation).

| Tool | Agents | Notes |
|------|--------|-------|
| **Aura** | 10+ agents (roslyn-coding, code-review, business-analyst, documentation, build-fixer, etc.) | Capability-based selection, priority routing |
| **GH Copilot CLI** | Single agent | Different modes (explain, suggest, commit) |
| **Claude Code** | Single agent | All-in-one |
| **Cursor** | Single agent | Composer vs. Chat modes |
| **Aider** | Single agent | Architect + Coder personas |

**Aura advantage:** True specialization with dedicated system prompts, tool access, and capability tags.

---

### 3. Sub-Agent Spawning

**What it is:** Ability to delegate subtasks to isolated agents with fresh context windows.

| Tool | Support | Notes |
|------|---------|-------|
| **Aura** | ✅ `spawn_subagent` tool | Fresh context, returns summary, same tool access |
| **GH Copilot CLI** | ❌ | N/A |
| **Claude Code** | ⚠️ | Task delegation exists but not user-controllable |
| **Cursor** | ❌ | N/A |
| **Aider** | ❌ | N/A |

**Aura advantage:** Explicit tool that agents can invoke when context fills up.

---

### 4. Token Budget Awareness

**What it is:** Tracking context window usage and adapting behavior.

| Tool | Support | Notes |
|------|---------|-------|
| **Aura** | ✅ `check_token_budget` + automatic warnings at 70/80/90% | Agent can query and decide |
| **GH Copilot CLI** | ❌ | No visibility |
| **Claude Code** | ⚠️ | Internal management, auto-compaction |
| **Cursor** | ❌ | No visibility |
| **Aider** | ⚠️ | Shows token counts, no agent awareness |

**Aura advantage:** Agents can programmatically check budget and spawn sub-agents.

---

### 5. Retry Loops ("Ralph Loops")

**What it is:** Automatic retry with failure context injection when execution fails.

| Tool | Support | Notes |
|------|---------|-------|
| **Aura** | ✅ Configurable `RetryOnFailure`, `MaxRetries`, `RetryCondition` | Conditions: AllFailures, BuildErrors, TestFailures |
| **GH Copilot CLI** | ❌ | Manual retry only |
| **Claude Code** | ✅ | Auto-retry on errors |
| **Cursor** | ⚠️ | Can retry manually |
| **Aider** | ✅ | Lint/test loop built-in |

**Aura advantage:** Fine-grained control over retry conditions.

---

### 6. Semantic Code Search (RAG)

**What it is:** Finding relevant code by meaning, not just keywords.

| Tool | Support | Notes |
|------|---------|-------|
| **Aura** | ✅ Vector embeddings + PostgreSQL pgvector | Multi-language, configurable chunk size |
| **GH Copilot CLI** | ⚠️ | Basic workspace search |
| **Claude Code** | ✅ | Full codebase indexing |
| **Cursor** | ✅ | @codebase semantic search |
| **Aider** | ⚠️ | Repo map, limited semantic |

**Aura advantage:** Local-first, persistent indexes, 30+ language support.

---

### 7. Code Graph Navigation

**What it is:** Understanding relationships between code elements (callers, implementations, usages).

| Tool | Support | Notes |
|------|---------|-------|
| **Aura** | ✅ Roslyn-based code graph | `aura_navigate`: callers, implementations, derived_types, usages |
| **GH Copilot CLI** | ❌ | No graph |
| **Claude Code** | ❌ | No explicit graph (relies on search) |
| **Cursor** | ⚠️ | LSP integration |
| **Aider** | ❌ | No graph |

**Aura advantage:** True semantic code navigation with Roslyn analysis.

---

### 8. Workflow Planning

**What it is:** Breaking down tasks into executable steps with human approval.

| Tool | Support | Notes |
|------|---------|-------|
| **Aura** | ✅ Business Analyst agent → Workflow steps → Step execution | GitHub issue integration, worktree isolation |
| **GH Copilot CLI** | ❌ | No planning |
| **Claude Code** | ⚠️ | Internal planning, not exposed |
| **Cursor** | ❌ | No multi-step workflows |
| **Aider** | ❌ | No planning |

**Aura advantage:** Explicit workflow lifecycle with visibility and approval gates.

---

### 9. Human-in-the-Loop

**What it is:** Requiring user confirmation for changes.

| Tool | Support | Notes |
|------|---------|-------|
| **Aura** | ✅ Tool confirmation, step approval, reject with feedback | Configurable per-tool |
| **GH Copilot CLI** | ✅ | Confirm before execution |
| **Claude Code** | ✅ | Approve/reject edits |
| **Cursor** | ✅ | Accept/reject in diff view |
| **Aider** | ✅ | Auto-commit optional |

**Parity:** All tools support this well.

---

### 10. Local-First / Privacy

**What it is:** Code never leaves your machine.

| Tool | Support | Notes |
|------|---------|-------|
| **Aura** | ✅ | Ollama (local) or Azure OpenAI (your tenant) |
| **GH Copilot CLI** | ❌ | Cloud only (GitHub) |
| **Claude Code** | ❌ | Cloud only (Anthropic) |
| **Cursor** | ❌ | Cloud only |
| **Aider** | ✅ | Supports local models |

**Aura advantage:** Enterprise-ready with tenant isolation.

---

### 11. File Editing UX

**What it is:** Quality of the file modification experience.

| Tool | Support | Notes |
|------|---------|-------|
| **Aura** | ⚠️ | MCP tools (aura_edit, aura_generate), extension panel | Functional but not as polished |
| **GH Copilot CLI** | ⚠️ | Suggestions only, manual apply |
| **Claude Code** | ✅ | Excellent diff view, streaming edits |
| **Cursor** | ✅ | Native editor integration |
| **Aider** | ✅ | Auto-apply with git commit |

**Aura gap:** File editing UX could be more polished. Consider streaming diffs in extension.

---

### 12. Web Browsing

**What it is:** Fetching documentation or information from the web.

| Tool | Support | Notes |
|------|---------|-------|
| **Aura** | ❌ | Not implemented |
| **GH Copilot CLI** | ❌ | No |
| **Claude Code** | ✅ | Can browse docs |
| **Cursor** | ❌ | No |
| **Aider** | ❌ | No |

**Aura gap:** Consider adding a `fetch_url` tool for documentation lookup.

---

### 13. Memory Across Sessions

**What it is:** Remembering context from previous conversations.

| Tool | Support | Notes |
|------|---------|-------|
| **Aura** | ❌ | Each workflow/chat is isolated |
| **GH Copilot CLI** | ❌ | Stateless |
| **Claude Code** | ✅ | Project memory, CLAUDE.md |
| **Cursor** | ⚠️ | .cursorrules |
| **Aider** | ⚠️ | .aider files, conventions |

**Aura gap:** Consider adding project memory or learning from past workflows.

---

## Aura Strengths

1. **Multi-Agent Architecture** - No other tool has true specialized agents
2. **Sub-Agent Spawning** - Unique capability for context management
3. **Token Budget Awareness** - Programmatic access to context state
4. **Code Graph Navigation** - Roslyn-powered semantic understanding
5. **Workflow Planning** - Explicit lifecycle with GitHub integration
6. **Local-First Privacy** - Enterprise-ready, no cloud dependency

## Aura Gaps to Address

| Gap | Priority | Effort | Notes |
|-----|----------|--------|-------|
| File editing UX | High | Medium | Streaming diffs, inline preview |
| Web browsing | Medium | Low | `fetch_url` tool |
| Session memory | Medium | High | AURA.md or DB-backed memory |
| MCP parity with Claude Code | Medium | Medium | More polished tool outputs |

---

## Recommendations

### Short-Term (v1.5)

1. **Improve file editing UX** - Stream diffs in extension, better error display
2. **Add `fetch_url` tool** - Simple web fetching for documentation

### Medium-Term (v1.6)

3. **Project memory** - AURA.md file for project conventions and learnings
4. **Parallel step execution** - Already specced in agentic-execution-v2.md

### Long-Term (v2.0)

5. **Learning from workflows** - Extract patterns from successful completions
6. **Cross-session context** - Persistent agent memory

---

## Appendix: Agent Inventory

### Aura Agents (Current)

| Agent | Purpose | Capabilities |
|-------|---------|--------------|
| `roslyn-coding` | C# development with Roslyn tools | coding, refactoring |
| `coding-agent` | Polyglot development | coding |
| `code-review-agent` | Code review and feedback | review |
| `business-analyst-agent` | Planning and analysis | analysis |
| `documentation-agent` | Documentation generation | documentation |
| `build-fixer-agent` | Fix compilation errors | coding, debugging |
| `issue-enrichment-agent` | Gather context for issues | analysis |
| `chat-agent` | General conversation | chat |
| `echo-agent` | Testing/debugging | testing |
| Language ingesters | Parse 30+ languages | indexing |

### Comparison: Single-Agent Tools

GH Copilot CLI, Claude Code, Cursor, and Aider all use a single agent with mode switches or persona variations. Aura's multi-agent approach enables:

- **Specialized prompts** per task type
- **Capability-based routing** (coding vs. review vs. docs)
- **Priority ordering** for agent selection
- **Tool filtering** per agent

This is a unique architectural advantage that should be emphasized in marketing and documentation.
