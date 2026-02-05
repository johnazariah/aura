# Internal Agent Architecture Cleanup

**Status:** 📋 Planned
**Priority:** Medium
**Blocked By:** Decision to fully commit to Copilot CLI executor

## Context

With the successful validation of the Copilot CLI executor (12/12 calibration scenarios passing), the internal agent architecture is now redundant. This cleanup should be done if we commit to the external executor model.

## Scope

### Backend Code to Remove

| Component | Location | Description |
|-----------|----------|-------------|
| `RoslynCodingAgent` | `src/Aura.Module.Developer/Agents/` | Internal ReAct agent with Roslyn tools |
| `InternalAgentExecutor` | `src/Aura.Module.Developer/Services/` | Step executor using internal agents |
| `IStepExecutor` implementations | Various | Internal execution implementations |
| Obsolete endpoints | `DeveloperEndpoints.cs` | 8 endpoints marked `[Obsolete]` |

### Prompts to Remove

| File | Purpose |
|------|---------|
| `roslyn-coding.prompt` | Internal agent system prompt |
| `roslyn-coding-extract.prompt` | Extract method prompts |
| `step-execute.prompt` | May need review (used by both?) |

### Extension Code to Simplify

| Component | Change |
|-----------|--------|
| Step approval UI | Remove (no longer needed) |
| Agent selection | Remove (only Copilot executor) |
| Manual step execution | Remove internal agent option |
| Story detail view | Simplify step status display |

### Extension UI Rework for New Model

The extension needs to be updated to properly facilitate and visualize the Copilot CLI executor model:

| Component | New Behavior |
|-----------|--------------|
| Story creation | Clear flow: Create → Analyze → Decompose → Run |
| Wave visualization | Show waves with steps grouped, progress through waves |
| Step status | Display: Pending → Dispatched to Copilot → Completed/Failed |
| Quality gate | Show gate status between waves, final gate before completion |
| MCP tool visibility | Optional: Show which Aura MCP tools Copilot is calling |
| Copilot integration | Guide user to have Copilot CLI available and configured |
| Error handling | Better feedback when Copilot CLI fails or times out |
| Progress indication | Real-time updates as Copilot executes (SSE/polling) |

**Key UX Changes:**
1. Remove "Run Step" / "Approve Step" buttons (Copilot runs autonomously)
2. Add "Run All" button that triggers wave-by-wave execution
3. Show clear wave boundaries in step list
4. Indicate which executor is being used (always Copilot now)
5. Add MCP connection status indicator
6. Simplify from "agent orchestration" to "AI-assisted development"

### Configuration to Clean

| File | Change |
|------|--------|
| `agents/coding-agent.md` | Review if still needed |
| Agent hot-reload | May simplify if fewer agents |

## Migration Notes

1. **Keep MCP tools** - These are used by Copilot CLI
2. **Keep orchestration** - Story lifecycle, waves, quality gates
3. **Keep analysis agent** - Used for story analysis phase
4. **Review decomposition** - Currently uses LLM, may keep

## Estimated Effort

- Backend cleanup: 2-4 hours
- Extension cleanup: 2-4 hours
- Extension UI rework: 4-8 hours
- Testing: 2-3 hours
- Documentation: 1 hour

## Success Criteria

- [ ] All 12 calibration scenarios still pass
- [ ] No references to removed code
- [ ] Extension builds and works
- [ ] Extension UI clearly shows wave-based execution model
- [ ] User can run a story end-to-end from VS Code
- [ ] Reduced codebase complexity

## Dependencies

- Firm decision to abandon internal agent model
- No plans to support offline/local execution
