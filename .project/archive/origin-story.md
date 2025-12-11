# Aura: Origin Story

## The Before Times: hve-hack

It started as **Agent Orchestrator** (codename: hve-hack) - an ambitious system to automate software development using AI agents. The vision was clear: pull GitHub issues, break them into steps, execute agents to write code, and push PRs.

Over months, it grew:
- 17 projects
- ~38,000 lines of C#
- Complex orchestration layers
- Elaborate validation systems
- Plugin architectures
- Multiple abstraction layers

**It worked.** Kind of. But every new feature required touching 5 projects. Every bug fix cascaded through layers. The orchestration engine had become a hydra - cut off one head, two more appeared.

## The Pivot Moment: November 25, 2025

During a late-night session, we stepped back and asked: *"What are we actually building?"*

The answer wasn't "an orchestration engine" - it was something bigger:

> **A local-first, privacy-safe AI foundation for knowledge work.**

Like Windows Recall, but *your data never leaves your machine*. No cloud uploads. No telemetry. Works offline.

hve-hack had become technical debt incarnate. It was optimized for the wrong goal - complex orchestration rather than simple, composable capability.

## The Rewrite Decision

We made a bold call: **Start fresh. Port the good parts. Delete the rest.**

Key insights that drove the decision:

1. **Simplicity over sophistication** - The best code is code that doesn't exist
2. **Composable modules** - Enable/disable capabilities, not monolithic features  
3. **Hot-reloadable agents** - Drop a markdown file, get an AI capability
4. **Local-first architecture** - Privacy as a feature, not an afterthought

## One Night, One Silver Thread

In a single session (November 25-26, 2025), we built:

### Phase 1: Core Infrastructure
- `IAgent`, `AgentRegistry`, `MarkdownAgentLoader`
- `ConfigurableAgent` that actually calls LLMs
- 37 unit tests, all passing

### Phase 2: LLM Providers
- `OllamaProvider` - real HTTP calls to local Ollama
- Provider registry with fallback
- Result<T,E> error handling throughout

### Phase 3: VS Code Extension (Silver Thread)
- Status panel showing Ollama, DB, API health
- Agents panel listing available agents
- Execute command that calls the LLM and shows results
- *End-to-end working in one session*

### Phase 4: Aspire Orchestration
- `Aura.AppHost` with Aspire 13
- PostgreSQL container ready for Phase 3 (Data Layer)
- OpenTelemetry, health checks, service discovery
- One command starts everything

## What Got Ported

From hve-hack, we kept the *patterns*, not the code:

| Kept | Deleted |
|------|---------|
| Markdown agent format | Complex orchestration engine |
| Ollama HTTP protocol | Execution planner |
| Result<T,E> pattern | Agent output validator |
| Provider abstraction | Workflow state machines |
| Agent metadata | Plugin discovery service |

## The Philosophy Shift

### Before: "How do we orchestrate everything?"
Complex state machines, execution planners, retry policies, validation layers.

### After: "How do we make each piece excellent?"
Simple agents. Clean composition. Let the user orchestrate.

## What's Next

- **Phase 3: Data Layer** - EF Core, PostgreSQL, conversation history
- **Phase 5: Developer Module** - Git worktrees, workflow automation
- **Phase 6: Self-Bootstrapping** - Aura develops Aura

## The Name

**Aura** - from Latin *aura* ("breeze, breath") 

A gentle presence. Local. Private. Always there when you need it.

Not an orchestrator demanding control, but a foundation enabling capability.

---

*"The best software is built not by adding features until it works, but by removing complexity until it can't fail."*

— Lessons from the rewrite
