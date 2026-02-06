# ADR-023: MCP Integration Over Copilot SDK

**Status:** Accepted  
**Date:** 2026-02-06  
**Deciders:** Architecture Review  
**Tags:** integration, llm, orchestration

## Context

During a review of multi-agent orchestration approaches, we evaluated whether Aura should adopt the [GitHub Copilot SDK](https://github.com/github/copilot-sdk) for agent execution, particularly after examining [TeamBot](https://github.com/glav/teambot)—a project that uses the SDK to orchestrate 6 specialized agents through a 13-stage development workflow.

### Investigation Scope

1. **TeamBot Architecture** - How TeamBot uses the Copilot SDK for autonomous multi-agent orchestration
2. **Copilot SDK Capabilities** - What the SDK provides vs. Aura's current MCP-based approach
3. **Alignment Assessment** - Whether adopting the SDK would benefit Aura

## TeamBot Analysis

TeamBot is a Python CLI tool that orchestrates a "team" of 6 AI agent personas:

| Persona | Role |
|---------|------|
| PM | Project Manager - planning, coordination |
| BA | Business Analyst - requirements, specs |
| Writer | Technical Writer - documentation |
| Builder-1 | Primary implementation |
| Builder-2 | Secondary implementation (parallel) |
| Reviewer | Code review, QA |

### TeamBot Workflow

TeamBot defines a **13-stage prescriptive workflow**:

```
SETUP → BUSINESS_PROBLEM → SPEC → SPEC_REVIEW → RESEARCH →
TEST_STRATEGY → PLAN → PLAN_REVIEW → IMPLEMENTATION →
IMPLEMENTATION_REVIEW → TEST → POST_REVIEW → COMPLETE
```

Key characteristics:
- **Autonomous execution**: Runs up to 8 hours without human intervention
- **Review iteration**: Max 4 cycles until reviewer approves
- **Parallel builders**: builder-1 and builder-2 execute concurrently
- **File-based artifacts**: `.teambot/artifacts/` contains `spec.md`, `plan.md`, `research.md`, etc.
- **State persistence**: JSON files in `.teambot/` directory

### TeamBot Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    ExecutionLoop                                │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │  ObjectiveParser  →  StageExecutor  →  ReviewIterator      ││
│  └─────────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│  Orchestrator ← AgentRunner ← CopilotSDKClient                 │
│       ↓              ↓              ↓                           │
│  WorkflowStateMachine  HistoryManager  MessageRouter           │
└─────────────────────────────────────────────────────────────────┘
```

TeamBot uses the SDK to **drive** Copilot—the app is the orchestrator, Copilot is the executor.

## Copilot SDK Analysis

The [GitHub Copilot SDK](https://github.com/github/copilot-sdk) provides programmatic control of the Copilot CLI:

### SDK Capabilities

| Feature | Description |
|---------|-------------|
| Session Management | Create, resume, list, delete conversation sessions |
| Custom Tools | Register tools that Copilot can invoke |
| Streaming | Real-time delta events for responses |
| Hooks | Pre/post tool execution interception |
| BYOK | Bring Your Own Key for alternative LLM providers |
| Multiple Languages | Python, TypeScript, Go, .NET SDKs |

### SDK Code Pattern

```csharp
// .NET example
var client = new CopilotClient();
await client.StartAsync();

var session = await client.CreateSessionAsync(new SessionConfig {
    Model = "gpt-5",
    Tools = [myCustomTool],
    Hooks = new SessionHooks {
        OnPreToolUse = async (input, inv) => {
            // Approve, deny, or modify tool execution
            return new PreToolUseHookOutput {
                PermissionDecision = "allow",
                ModifiedArgs = input.ToolArgs
            };
        },
        OnPostToolUse = async (input, inv) => {
            // Verify or log tool results
            return new PostToolUseHookOutput();
        }
    }
});

await session.SendAndWait({ Prompt = "Build the feature" });
```

### SDK Limitations

- **Technical Preview**: Not production-ready
- **Copilot CLI dependency**: Requires separate CLI installation
- **Copilot models only**: Cannot use Ollama, local models
- **Billing**: Counts against Copilot premium request quota

## Comparison: SDK vs. MCP

| Aspect | Copilot SDK | Aura's MCP |
|--------|-------------|------------|
| **Control Direction** | App orchestrates Copilot | Copilot calls Aura tools |
| **Who's in control** | Application | User (via Copilot) |
| **Human-in-loop** | Optional (hooks) | Built-in (user types prompts) |
| **Autonomy** | High (8-hour autonomous runs) | Low (step-by-step with user) |
| **Production Status** | ⚠️ Technical Preview | ✅ Stable |
| **LLM Flexibility** | Copilot models only | Any (Ollama, Azure, OpenAI) |
| **Composability** | Locked to Copilot | Works with any MCP client |
| **Deployment** | Requires Copilot CLI | Self-contained Windows Service |

### Control Flow Diagram

**Copilot SDK (TeamBot model):**
```
Your App → SDK Client → Copilot CLI (server mode)
                              ↓
                         LLM Backend
```

**Aura's MCP (current model):**
```
User → VS Code/Copilot → MCP Protocol → Aura API → Tools
                                            ↓
                                     Aura LLM Providers
                                     (Ollama/Azure/OpenAI)
```

## Decision

**Stay with MCP integration. Do not adopt the Copilot SDK.**

### Rationale

1. **Philosophy Alignment**: Aura's core principle is "human-in-the-loop"—users control execution. The SDK is designed for autonomous operation, which conflicts with this principle.

2. **Composability**: MCP is an open protocol. Aura's tools work with VS Code, Claude, and any MCP-compatible client. The SDK locks you to Copilot.

3. **LLM Provider Flexibility**: Aura supports Ollama (local, private), Azure OpenAI, and OpenAI. The SDK only accesses Copilot's models.

4. **Production Readiness**: The SDK is in Technical Preview with explicit warnings against production use. Aura's MCP integration is stable and deployed.

5. **Existing Investment**: Aura already has robust LLM provider infrastructure. Adding the SDK would be a third parallel path to LLM access.

6. **No Clear Benefit**: The SDK's main advantage—autonomous orchestration—isn't aligned with Aura's supervised workflow model.

## Patterns to Adopt

While rejecting the SDK, we identified valuable patterns from TeamBot and the SDK to incorporate:

### 1. Verification Degrees

Introduce configurable verification levels for story execution:

| Level | Name | What's Checked | Iterations |
|-------|------|----------------|------------|
| 0 | None | Nothing | 0 |
| 1 | Compile | Code compiles | 1 |
| 2 | Test | Compile + tests pass | 1 |
| 3 | Coverage | Test + new tests cover changes | 1-2 |
| 4 | Review | Coverage + AI reviews against spec | 2-4 |
| 5 | Acceptance | Review + acceptance criteria validated | 2-4 |

### 2. Review Iteration

Adopt TeamBot's review-feedback-retry loop:

```
┌─────────────────────────────────────────────┐
│  1. Execute step                            │
│  2. Verify output against criteria          │
│  3. If failed && iterations < max:          │
│     a. Inject failure context               │
│     b. Re-execute with feedback             │
│  4. If max iterations reached: fail step    │
└─────────────────────────────────────────────┘
```

### 3. Structured Tool Results

Adopt the SDK's tool result structure:

```csharp
public record ToolResult {
    string TextResultForLlm { get; init; }
    string ResultType { get; init; }  // "success" | "failure"
    string? SessionLog { get; init; }
    string? Error { get; init; }
}
```

### 4. File-Based Artifacts

TeamBot's `.teambot/artifacts/` pattern validates our [SDD Artifact Export](../features/upcoming/sdd-artifact-export.md) feature. Key insight: database primary + file export is superior to file-only storage.

## Consequences

### Positive

- Maintain Aura's core "human-in-the-loop" philosophy
- Preserve LLM provider flexibility (Ollama, Azure, OpenAI)
- Keep MCP's composability with multiple clients
- Avoid dependency on Technical Preview SDK
- Adopt proven patterns (verification, iteration) without SDK coupling

### Negative

- No autonomous "overnight execution" capability
- Cannot leverage SDK's session hooks directly (must implement ourselves)
- May need to revisit if SDK reaches production and demand for autonomy increases

### Neutral

- TeamBot and Aura serve different use cases (autonomous vs. supervised)
- Both approaches are valid for their respective goals

## Future Considerations

Revisit this decision if:

1. **SDK reaches GA**: Production-ready status removes stability concerns
2. **Autonomous mode demand**: Users want "run overnight" capability
3. **MCP limitations**: Encounter composability issues with current approach

## References

- [GitHub Copilot SDK](https://github.com/github/copilot-sdk)
- [TeamBot](https://github.com/glav/teambot)
- [Model Context Protocol](https://modelcontextprotocol.io/)
- [ADR-012: Tool-Using Agents](./012-tool-using-agents.md)
- [ADR-022: Multi-Agent Orchestration](./022-multi-agent-orchestration.md)
- [SDD Artifact Export Feature](../features/upcoming/sdd-artifact-export.md)
