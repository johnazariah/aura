# Remove Internal Agent Architecture

**Status:** ✅ Complete
**Completed:** 2026-02-06
**Created:** 2026-02-06
**Priority:** High
**Type:** Cleanup / Dead Code Removal

## Decision

Copilot Chat + MCP is the execution and chat model. The internal agent execution path and built-in chat UI are dead code. Remove them.

**What stays:**
- Agent framework (`IAgent`, `IAgentRegistry`, `ReActExecutor`, `ConfigurableAgent`) — used by analysis, research, ingester agents
- Roslyn services (`IRoslynRefactoringService`, `IRoslynWorkspaceService`) — exposed as MCP tools
- MCP server and all 8 meta-tools — how Copilot accesses Aura capabilities
- `IStepExecutor` interface + `StepExecutorRegistry` — used by `GitHubCopilotDispatcher`
- `GitHubCopilotDispatcher` — active execution path
- Story lifecycle, waves, quality gates — orchestration layer
- Agent list endpoints (`GET /api/agents`, `GET /api/agents/{id}`) — used by extension status view

**What goes:**

### Backend — Developer Module

| File | Reason |
|------|--------|
| `Agents/RoslynCodingAgent.cs` (1182 lines) | Internal LLM→Roslyn execution loop, replaced by MCP tools |
| `Agents/CSharpOperationDto.cs` | Only used by RoslynCodingAgent |
| `Services/InternalAgentExecutor.cs` (288 lines) | Internal step executor, replaced by CopilotDispatcher |
| `DeveloperAgentProvider.cs` | Remove `RoslynCodingAgent` yield (keep ingesters) |
| `DeveloperModule.cs` | Remove `InternalAgentExecutor` registration |

### Backend — API Endpoints

| File/Endpoint | Reason |
|---------------|--------|
| 8 `[Obsolete]` methods in `DeveloperEndpoints.cs` | Old step management model |
| `AgentEndpoints.cs`: `/execute`, `/chat/stream`, `/execute/rag`, `/execute/agentic` | Built-in chat, replaced by Copilot Chat + MCP |
| `ConversationEndpoints.cs` (entire file) | Chat persistence for dead UI |

### Backend — Foundation

| File | Reason |
|------|--------|
| `Conversations/IConversationService.cs` | Only used by ConversationEndpoints |
| `Conversations/ConversationService.cs` | Only used by ConversationEndpoints |
| `ServiceCollectionExtensions.cs` | Remove `AddConversationServices` call |

### Backend — Prompts

| File | Reason |
|------|--------|
| `prompts/roslyn-coding.prompt` | Only used by RoslynCodingAgent |
| `prompts/roslyn-coding-extract.prompt` | Only used by RoslynCodingAgent |

### Backend — Tests

| File | Reason |
|------|--------|
| `Agents/RoslynCodingAgentTests.cs` | Tests for removed agent |
| `Agents/DeveloperAgentProviderTests.cs` | Tests RoslynCodingAgent registration — update |

### Extension

| File | Reason |
|------|--------|
| `providers/chatPanelProvider.ts` | Dead code (not imported) |
| `providers/chatWindowProvider.ts` | Built-in chat UI, replaced by Copilot Chat |
| `providers/agentTreeProvider.ts` | Agent browser for chat — no longer needed |
| `extension.ts` | Remove chat commands, agent tree, ChatWindowProvider |
| `services/auraApiService.ts` | Remove chat/execute/approve/reject/skip/reset/reassign methods |
| `providers/storyPanelProvider.ts` | Remove step-level chat/approve/reject/skip/reassign UI |
| `package.json` | Remove `aura.openChat` command, chat walkthrough step |

### Database

- **No migration needed** — Conversation tables stay in the DB (harmless). Code just stops using them.
- If desired later, a migration can drop `conversations`, `conversation_messages`, `agent_executions` tables.

## Keep List (agents that stay)

| Agent | Where | Why |
|-------|-------|-----|
| `CSharpIngesterAgent` | `DeveloperAgentProvider` | RAG indexing |
| `TreeSitterIngesterAgent` | `DeveloperAgentProvider` | RAG indexing |
| `coding-agent.md` | `agents/` | Markdown agent for generic coding tasks (used by analysis) |
| `chat-agent.md` | `agents/` | Could be used by future Copilot SDK participants |
| All `agents/languages/*.yaml` | `agents/languages/` | Language configs for future use |

## Success Criteria

- [x] `dotnet build` succeeds with 0 errors
- [x] All remaining tests pass (849 tests)
- [x] Extension builds (`Build-Extension.ps1`)
- [x] MCP tools still work (verified via Copilot Chat)
- [x] Story create/analyze/plan/run flow still works
- [x] No references to removed code remain
