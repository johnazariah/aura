---
agent: agent
description: Update user-facing documentation (getting-started, user-guide, troubleshooting) to reflect current Aura capabilities.
---

# Update User-Facing Documentation

You are updating Aura's user-facing documentation in `docs/`. These docs help end users install, configure, and use Aura effectively.

## Architecture Context

Aura is an AI-powered development assistant with **local code intelligence** (Roslyn, TreeSitter, pgvector RAG) and **cloud-accelerated LLM inference** (Azure OpenAI by default, Ollama supported for local inference). It runs as a **Windows Service** on port **5300** and integrates with **GitHub Copilot** via MCP (Model Context Protocol). See `ADR-024` for architecture details.

**Key facts for accuracy:**
- Default LLM: Azure OpenAI (not Ollama — Ollama is optional/local alternative)
- Port: 5300 (not 5258)
- Stories (not workflows): the primary work unit is a "story"
- Code execution: GitHub Copilot CLI is the sole execution path for story steps
- Internal agents were removed 2026-02-06; the system now uses markdown agent definitions for prompt-based delegation
- No plugin system exists (IAgentPlugin, DynamicAgentRegistry for code-based plugins — removed)
- Namespace: `Aura.*` (not `AgentOrchestrator.*`)

## Documentation Structure

```
docs/
├── README.md                    # Documentation home
├── getting-started/
│   ├── installation.md          # Prerequisites and setup
│   ├── first-run.md             # Initial configuration
│   └── quick-start.md           # 5-minute quickstart
├── user-guide/
│   ├── indexing.md              # Code indexing
│   ├── cheat-sheet.md           # Quick reference
│   ├── mcp-tools.md             # MCP tool reference
│   ├── patterns.md              # Operational patterns
│   └── use-cases.md             # Common scenarios
├── configuration/
│   ├── llm-providers.md         # LLM provider setup
│   └── settings.md              # Configuration options
├── troubleshooting/
│   ├── common-issues.md         # FAQ and fixes
│   ├── logs.md                  # Log analysis
│   └── support.md               # Getting help
└── mcp-tools/
    ├── api-reference.md         # MCP tool API reference
    └── aura_docs.md             # Docs tool reference
```

## What to Review Before Updating

### Source of Truth (verify all claims against these)
- `src/Aura.Api/Endpoints/*.cs` — actual API endpoints
- `src/Aura.Api/Mcp/McpHandler*.cs` — actual MCP tool implementations
- `extension/src/` — actual VS Code extension features
- `agents/*.md` — actual agent definitions
- `src/Aura.Api/appsettings.json` — actual configuration structure
- `.github/ARCHITECTURE-QUICK-REFERENCE.md` — up-to-date architecture summary

### Do NOT reference these (removed/non-existent)
- ❌ `docs/USAGE.md`, `docs/CONFIGURATION.md`, `docs/AURA-DEVELOPER-GUIDE.md`
- ❌ `docs/PLUGIN_SYSTEM.md`, `docs/MULTI-PROVIDER-AGENTS.md`
- ❌ `docs/NEW-MACHINE-SETUP.md`
- ❌ `extension/QUICKSTART.md`, `extension/MVP-DEMO.md`
- ❌ `src/AgentOrchestrator.*` (old namespace)
- ❌ `IAgentExecutor`, `DynamicAgentRegistry`, `AgentHub`, `Task Monitor`, `Insights` views

## Writing Guidelines

1. **Verify before writing** — check actual source code, don't trust old docs
2. **Azure OpenAI is default** — mention Ollama as alternative, not primary
3. **Stories, not workflows** — the API uses `/api/developer/stories`
4. **MCP integration** — Aura surfaces tools to GitHub Copilot, not a standalone tool
5. **No vaporware** — only document features that exist in code
6. **Port 5300** — always use the correct port
7. **Copy-paste ready** — all commands and curl examples must work as-is
8. **Proactive troubleshooting** — address predictable issues before users hit them

## Execution

1. Read relevant source files to verify current state
2. Update each doc file to reflect accurate architecture
3. Verify all internal links point to existing files
4. Ensure consistent terminology across all docs
5. Remove references to removed features (plugin system, code-based agents, AgentOrchestrator namespace)

## Quality Criteria

- ✅ All API URLs use port 5300 and `/api/developer/stories` paths
- ✅ Azure OpenAI described as default provider
- ✅ No references to removed features or non-existent files
- ✅ Every command/example works without modification
- ✅ MCP tools accurately described with correct operation names
- ✅ Consistent terminology: "story" not "workflow" for work units
