---
agent: agent
description: Update the root README.md and docs/README.md to accurately reflect Aura's current capabilities and architecture.
---

# Update README Files

You are updating the project's root `README.md` and `docs/README.md` to accurately reflect the current state of Aura.

## Architecture Context

Aura is an AI-powered development assistant with:
- **Local code intelligence**: Roslyn, TreeSitter, pgvector RAG — all code analysis runs locally
- **Cloud LLM inference**: Azure OpenAI (default), Ollama (optional local alternative)
- **MCP integration**: 13 tools exposed to GitHub Copilot via Model Context Protocol
- **Windows Service**: Port 5300, deployed via `Update-LocalInstall.ps1`

See `.project/adr/024-hybrid-architecture.md` for the authoritative architecture decision.

## README Sections

### Root README.md

Must include (in this order):
1. **Project title and tagline** — accurate one-liner about what Aura does
2. **Quick start** — prerequisites, install, first run
3. **Architecture overview** — hybrid model, MCP tools, key components
4. **Features** — organized by category, concise (1-2 lines each)
5. **Project structure** — solution layout
6. **Contributing** — build, test, development workflow
7. **License**

### docs/README.md

Serves as the documentation home page / index. Must:
- Link to all docs subdirectories with brief descriptions
- Provide navigation to getting-started, user-guide, configuration, troubleshooting
- Accurately describe what Aura is

## Source of Truth (verify all claims)

| Claim | Verify Against |
|-------|---------------|
| Features | `src/Aura.Api/Endpoints/*.cs`, `src/Aura.Api/Mcp/McpHandler*.cs` |
| Agents | `agents/*.md` (list actual agent files) |
| Extension features | `extension/src/` |
| API endpoints | `src/Aura.Api/Endpoints/*.cs` |
| MCP tools | `src/Aura.Api/Mcp/McpHandler.cs` (see `_tools` dictionary) |
| Configuration | `src/Aura.Api/appsettings.json` |
| Test count | `dotnet test --list-tests 2>$null \| Measure-Object` |

## Do NOT Reference

- ❌ "Local-first" as primary positioning (it's hybrid)
- ❌ "Privacy-safe" / "never leaves your machine" (LLM calls go to Azure)
- ❌ Plugin system, IAgentPlugin, hot-reload plugins
- ❌ IAgentExecutor, code-based agents
- ❌ AgentOrchestrator.* namespace
- ❌ Port 5258
- ❌ `.NET 10` (project uses .NET 9)
- ❌ Non-existent doc files (PLUGIN_SYSTEM.md, CONFIGURATION.md, USAGE.md, etc.)
- ❌ Removed extension views (Agent Hub, Task Monitor, Insights)

## Quality Criteria

- ✅ A new user understands what Aura does in 30 seconds
- ✅ Quick start works on a fresh clone
- ✅ Every feature mentioned actually exists in code
- ✅ All links point to existing files
- ✅ No stale terminology or removed features
- ✅ Architecture section references ADR-024
