# Agentic Execution v2: Sub-Agents, Retry Loops, and Parallel Execution

> **Status:** ✅ Complete
> **Completed:** 2026-01-23
> **Created:** 2026-01-23
> **Author:** Aura Team

## Overview

Evolve Aura's agent execution model to address emerging patterns in AI-assisted software development:

1. **Sub-Agent Invocation** — Fresh context windows for isolated tasks
2. **Retry Loops (Ralph Loops)** — Iterative refinement until success
3. **Token Budget Awareness** — Track and manage context space

### Deferred (v2.1)

4. **Parallel Step Execution** — Independent steps run concurrently (see [Future Work](#future-work-parallel-step-execution))

These changes address the regression from multi-agent to **single-context** execution. The goal is lightweight multi-agent capability without the orchestration complexity of the original hve-hack architecture.

## Background

### Industry Patterns Emerging

From team discussions and industry observation:

| Pattern | Description | Aura Status |
|---------|-------------|-------------|
| Narrow Prompts | Specific, tightly-scoped instructions | ✅ Workflow steps + patterns |
| Ralph Loops | Iterative retry until convergence | ❌ Not present |
| State Access | Model can inspect build/test state | ✅ MCP tools |
| Sub-Agents | Isolated context per subtask | ❌ Regressed |
| Context Management | Preserve context space | ⚠️ Partial |
| Parallel Execution | Concurrent independent work | ⏳ Deferred to v2.1 |

### The Problem

Current ReAct executor uses a **single conversation context**:

```
Task → [Think → Act → Observe] × N → Final Answer
              ↑___________________|
              Same context window
```

As tasks get complex:
- Context fills with tool outputs (code, diffs, test results)
- Model loses earlier reasoning
- No opportunity to "start fresh" on a subtask
- No parallelism possible
- Failure at step N terminates the loop

### The Solution

**Multi-context execution with retry capability:**

```
Task → Planner → [Step₁, Step₂, Step₃]
                      │      │      │
                      ▼      ▼      ▼
                 SubAgent SubAgent SubAgent  ← Fresh contexts
                      │      │      │
                      └──────┴──────┘
                             │
                      ┌──────▼──────┐
                      │ Retry Loop  │ ← On failure, inject feedback
                      └─────────────┘
```

---

## Goals

1. **Isolated Subtasks** — Sub-agents get fresh context windows
2. **Graceful Failure** — Retry with feedback instead of immediate termination
3. **Parallelism** — Independent steps execute concurrently
4. **Token Efficiency** — Track usage, spawn sub-agent when context is exhausted
5. **Backward Compatible** — Existing workflows continue to work

## Non-Goals

1. Recreating hve-hack orchestration complexity
2. Autonomous multi-agent coordination (agent decides when to spawn)
3. Distributed execution across machines
4. Real-time collaboration between agents

---

## Architecture

### High-Level Design

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         WORKFLOW EXECUTOR                               │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  Workflow ────► StepExecutor ────► AgentExecutor ────► ReActExecutor   │
│                      │                   │                   │          │
│                      │                   │                   ▼          │
│                      │                   │            ┌──────────────┐  │
│                      │                   │            │ Retry Loop   │  │
│                      │                   │            │ (Ralph)      │  │
│                      │                   │            └──────────────┘  │
│                      │                   │                              │
│                      │                   ▼                              │
│                      │            ┌─────────────┐                       │
│                      │            │ Sub-Agent   │ ← spawn_subagent tool │
│                      │            │ Executor    │                       │
│                      │            └─────────────┘                       │
│                      │                                                  │
│                      ▼                                                  │
│              ┌───────────────┐                                          │
│              │ Parallel Step │ ← Fan-out independent steps              │
│              │ Executor      │                                          │
│              └───────────────┘                                          │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

| Component | Responsibility |
|-----------|----------------|
| **AgentExecutor** | Runs a single agent with retry capability |
| **ReActExecutor** | Existing Think/Act/Observe loop (unchanged) |
| **SubAgentExecutor** | Creates isolated ReAct context for subtasks |
| **RetryLoop** | Wraps execution with failure feedback injection |

---

## Feature 1: Sub-Agent Invocation

### Concept

Add `spawn_subagent` as a built-in tool. When an agent realizes a subtask is complex or context is filling up, it can spawn a sub-agent with:
- Fresh context window
- Specific task description
- Access to same MCP tools
- Returns summary to parent

### Tool Definition

```json
{
  "name": "spawn_subagent",
  "description": "Spawn an isolated sub-agent for a complex subtask. Use when: (1) task is self-contained, (2) context is filling up, (3) you need a fresh perspective. The sub-agent gets its own context window and returns a summary.",
  "parameters": {
    "agent": {
      "type": "string",
      "description": "Agent ID to spawn (e.g., 'code-review-agent', 'coding-agent')"
    },
    "task": {
      "type": "string", 
      "description": "Clear, self-contained task description for the sub-agent"
    },
    "context": {
      "type": "string",
      "description": "Optional context to pass (file contents, previous findings, etc.)"
    },
    "tools": {
      "type": "array",
      "items": { "type": "string" },
      "description": "Optional: limit tools available to sub-agent (default: all parent tools)"
    },
    "maxSteps": {
      "type": "integer",
      "description": "Optional: max ReAct steps for sub-agent (default: 10)"
    }
  },
  "required": ["agent", "task"]
}
```

### Execution Flow

```
Parent Agent                              Sub-Agent
     │                                         
     │ spawn_subagent(                        
     │   agent: "code-review-agent",          
     │   task: "Review diff for security issues"
     │ )                                       
     │                                         
     ├──────────────────────────────────────► │
     │                                         │ Fresh ReAct context
     │                                         │ Think → Act → Observe...
     │                                         │
     │ ◄──────────────────────────────────────┤
     │                                         
     │ Observation: {                         
     │   "success": true,                     
     │   "summary": "Found 2 issues: SQL injection in...",
     │   "stepsUsed": 4,                      
     │   "tokensUsed": 3200                   
     │ }                                      
     │                                         
     ▼ Continue with parent context           
```

### Implementation

```csharp
// New: Aura.Foundation/Tools/BuiltIn/SpawnSubAgentTool.cs

public class SpawnSubAgentTool : ITool
{
    public string Name => "spawn_subagent";
    
    private readonly IAgentRegistry _agentRegistry;
    private readonly IReActExecutor _reactExecutor;
    private readonly IToolRegistry _toolRegistry;
    
    public async Task<ToolResult> ExecuteAsync(ToolInput input, CancellationToken ct)
    {
        var agentId = input.GetString("agent");
        var task = input.GetString("task");
        var context = input.GetStringOrDefault("context", "");
        var maxSteps = input.GetIntOrDefault("maxSteps", 10);
        
        // Resolve agent
        var agent = await _agentRegistry.GetAgentAsync(agentId, ct);
        if (agent is null)
            return ToolResult.Failure($"Agent '{agentId}' not found");
        
        // Get tools (optionally filtered)
        var tools = GetToolsForSubAgent(input);
        
        // Build sub-agent prompt
        var fullTask = string.IsNullOrEmpty(context) 
            ? task 
            : $"{task}\n\n## Context\n{context}";
        
        // Execute in isolated context
        var result = await _reactExecutor.ExecuteAsync(
            fullTask,
            tools,
            _llmProvider,
            new ReActOptions 
            { 
                MaxSteps = maxSteps,
                Model = agent.PreferredModel,
                AdditionalContext = agent.SystemPrompt
            },
            ct);
        
        // Return summary to parent
        return ToolResult.Success(new
        {
            success = result.Success,
            summary = result.FinalAnswer,
            stepsUsed = result.Steps.Count,
            tokensUsed = result.TotalTokensUsed,
            error = result.Error
        });
    }
}
```

### Token Budget Consideration

Sub-agents should be spawned when:
1. **Task is self-contained** — Can be described in a single prompt
2. **Context is filling** — Parent has used >70% of context window
3. **Parallel opportunity** — Multiple independent subtasks

Add token tracking to `ReActExecutor`:

```csharp
public record ReActOptions
{
    // ... existing properties ...
    
    /// <summary>
    /// Approximate token budget for this execution.
    /// When usage exceeds 70%, agent may consider spawning sub-agents.
    /// </summary>
    public int? TokenBudget { get; init; }
    
    /// <summary>
    /// Callback when token budget threshold is reached.
    /// Returns true if execution should continue.
    /// </summary>
    public Func<int, int, Task<bool>>? OnBudgetThreshold { get; init; }
}

public record ReActStep
{
    // ... existing properties ...
    
    /// <summary>Cumulative tokens used up to this step</summary>
    public int CumulativeTokens { get; init; }
    
    /// <summary>Remaining budget (null if no budget set)</summary>
    public int? RemainingBudget { get; init; }
}
```

---

## Feature 2: Retry Loops (Ralph Loops)

### Concept

When a ReAct execution fails (build error, test failure, tool error), instead of immediately returning failure:
1. Capture the failure state
2. Inject it as feedback into a new attempt
3. Retry with fresh reasoning but informed by the failure
4. Continue until success or max attempts exhausted

### Configuration

```csharp
public record ReActOptions
{
    // ... existing properties ...
    
    /// <summary>
    /// Enable retry on failure. When true, failures trigger a new attempt
    /// with the error injected as context.
    /// </summary>
    public bool RetryOnFailure { get; init; } = false;
    
    /// <summary>
    /// Maximum retry attempts (default: 3).
    /// Total executions = 1 initial + MaxRetries.
    /// </summary>
    public int MaxRetries { get; init; } = 3;
    
    /// <summary>
    /// Conditions that trigger retry. Default: all failures.
    /// </summary>
    public RetryCondition RetryCondition { get; init; } = RetryCondition.AllFailures;
    
    /// <summary>
    /// Template for injecting failure context into retry prompt.
    /// Placeholders: {{error}}, {{lastThought}}, {{lastAction}}, {{observation}}
    /// </summary>
    public string? RetryPromptTemplate { get; init; }
}

[Flags]
public enum RetryCondition
{
    None = 0,
    BuildErrors = 1,
    TestFailures = 2,
    ToolErrors = 4,
    MaxStepsReached = 8,
    AllFailures = BuildErrors | TestFailures | ToolErrors | MaxStepsReached
}
```

### Execution Flow

```
Attempt 1: Task → Think → Act → Observe → ... → FAILURE (build error)
                                                      │
                                                      ▼
Attempt 2: Task + "Previous attempt failed: CS1002 missing semicolon at line 42"
           → Think (now aware of error) → Act (fix) → Observe → ... → SUCCESS
```

### Implementation

```csharp
// Modified ReActExecutor.ExecuteAsync

public async Task<ReActResult> ExecuteAsync(
    string task,
    IReadOnlyList<ToolDefinition> availableTools,
    ILlmProvider llm,
    ReActOptions? options = null,
    CancellationToken ct = default)
{
    options ??= new ReActOptions();
    
    var attempt = 0;
    var maxAttempts = options.RetryOnFailure ? options.MaxRetries + 1 : 1;
    var allSteps = new List<ReActStep>();
    ReActResult? lastResult = null;
    string currentTask = task;
    
    while (attempt < maxAttempts)
    {
        attempt++;
        ct.ThrowIfCancellationRequested();
        
        _logger.LogInformation(
            "[REACT] Attempt {Attempt}/{Max} for task", 
            attempt, maxAttempts);
        
        // Execute single attempt
        lastResult = await ExecuteSingleAttemptAsync(
            currentTask, 
            availableTools, 
            llm, 
            options, 
            ct);
        
        allSteps.AddRange(lastResult.Steps);
        
        // Success? We're done
        if (lastResult.Success)
        {
            return lastResult with { Steps = allSteps };
        }
        
        // Check if we should retry
        if (!ShouldRetry(lastResult, options, attempt, maxAttempts))
        {
            break;
        }
        
        // Inject failure context for next attempt
        currentTask = BuildRetryPrompt(task, lastResult, options);
        
        _logger.LogWarning(
            "[REACT] Attempt {Attempt} failed: {Error}. Retrying...",
            attempt, lastResult.Error);
    }
    
    // All attempts exhausted
    return lastResult! with 
    { 
        Steps = allSteps,
        Error = $"Failed after {attempt} attempts. Last error: {lastResult!.Error}"
    };
}

private string BuildRetryPrompt(string originalTask, ReActResult failure, ReActOptions options)
{
    var template = options.RetryPromptTemplate ?? DefaultRetryTemplate;
    
    var lastStep = failure.Steps.LastOrDefault();
    
    return template
        .Replace("{{originalTask}}", originalTask)
        .Replace("{{error}}", failure.Error ?? "Unknown error")
        .Replace("{{lastThought}}", lastStep?.Thought ?? "")
        .Replace("{{lastAction}}", lastStep?.Action ?? "")
        .Replace("{{observation}}", lastStep?.Observation ?? "");
}

private const string DefaultRetryTemplate = """
    ## Previous Attempt Failed
    
    I tried to complete this task but encountered an error:
    
    **Error:** {{error}}
    
    **Last thought:** {{lastThought}}
    **Last action:** {{lastAction}}
    **Observation:** {{observation}}
    
    Please try again with a different approach. Learn from the failure above.
    
    ## Original Task
    
    {{originalTask}}
    """;
```

### Retry Heuristics

| Failure Type | Retry Strategy |
|--------------|----------------|
| Build error | Inject error message + line number |
| Test failure | Inject test name + assertion failure |
| Tool error | Inject tool name + error, suggest alternative |
| Max steps | Inject summary of progress, ask to complete faster |

---

## Feature 3: Token Budget Awareness

### Concept

Track token usage throughout execution and expose it to the agent, enabling informed decisions about when to:
- Summarize and continue
- Spawn a sub-agent
- Complete early with partial results

### Token Tracking

```csharp
public class TokenTracker
{
    private readonly int _budget;
    private int _used;
    
    public int Budget => _budget;
    public int Used => _used;
    public int Remaining => _budget - _used;
    public double UsagePercent => (double)_used / _budget * 100;
    
    public void Add(int tokens)
    {
        _used += tokens;
    }
    
    public bool IsAboveThreshold(double thresholdPercent = 70)
    {
        return UsagePercent >= thresholdPercent;
    }
}
```

### Agent Awareness

Add a read-only tool for agents to check their budget:

```json
{
  "name": "check_token_budget",
  "description": "Check remaining context budget. Use this to decide whether to spawn a sub-agent or summarize.",
  "parameters": {},
  "returns": {
    "budget": "Total token budget",
    "used": "Tokens used so far", 
    "remaining": "Tokens remaining",
    "percentUsed": "Percentage of budget used",
    "recommendation": "continue | summarize | spawn_subagent"
  }
}
```

### Automatic Intervention

When budget exceeds threshold, inject a system message:

```csharp
if (_tokenTracker.IsAboveThreshold(70))
{
    chatMessages.Add(new ChatMessage(
        ChatRole.System,
        """
        ⚠️ Context budget is 70% used. Consider:
        1. Spawning a sub-agent for remaining work
        2. Summarizing findings and completing
        3. Focusing only on the most critical remaining task
        """
    ));
}
```

---

## Implementation Plan

### Phase 1: Sub-Agent Tool (Week 1)

#### Task 1.1: Create TokenTracker class (Day 1, ~2 hours)

Create `src/Aura.Foundation/Tools/TokenTracker.cs`:

```csharp
namespace Aura.Foundation.Tools;

/// <summary>
/// Tracks token usage during ReAct execution.
/// Thread-safe for use across async operations.
/// </summary>
public class TokenTracker
{
    private readonly int _budget;
    private int _used;
    private readonly object _lock = new();
    
    public TokenTracker(int budget) => _budget = budget;
    
    public int Budget => _budget;
    public int Used { get { lock (_lock) return _used; } }
    public int Remaining => Budget - Used;
    public double UsagePercent => Budget > 0 ? (double)Used / Budget * 100 : 0;
    
    public void Add(int tokens)
    {
        lock (_lock) _used += tokens;
    }
    
    public bool IsAboveThreshold(double thresholdPercent = 70) 
        => UsagePercent >= thresholdPercent;
    
    public string GetRecommendation() => UsagePercent switch
    {
        >= 90 => "complete_now",
        >= 70 => "spawn_subagent",
        >= 50 => "summarize",
        _ => "continue"
    };
}
```

**Acceptance criteria:**
- [ ] Class created with thread-safe token counting
- [ ] Unit tests for threshold detection
- [ ] Unit tests for recommendation logic

#### Task 1.2: Extend ReActOptions and ReActResult (Day 1, ~1 hour)

Modify `src/Aura.Foundation/Tools/ReActExecutor.cs`:

Add to `ReActOptions`:
```csharp
/// <summary>
/// Approximate token budget for this execution.
/// When usage exceeds 70%, agent may consider spawning sub-agents.
/// Default: 100,000 (typical context window).
/// </summary>
public int TokenBudget { get; init; } = 100_000;

/// <summary>
/// Threshold percentage at which to warn agent about budget.
/// </summary>
public double BudgetWarningThreshold { get; init; } = 70.0;
```

Add to `ReActStep`:
```csharp
/// <summary>Cumulative tokens used up to this step</summary>
public int CumulativeTokens { get; init; }
```

**Acceptance criteria:**
- [ ] Options extended with budget properties
- [ ] Backward compatible (defaults preserve existing behavior)

#### Task 1.3: Integrate TokenTracker into ReActExecutor (Day 2, ~2 hours)

Modify `ReActExecutor.ExecuteAsync`:
- Create `TokenTracker` at start of execution
- Call `tracker.Add(llmResponse.TokensUsed)` after each LLM call
- Include `CumulativeTokens` in each `ReActStep`
- Log budget warnings when threshold exceeded

**Acceptance criteria:**
- [ ] Token tracking integrated into main loop
- [ ] Cumulative tokens recorded per step
- [ ] Warning logged when threshold exceeded

#### Task 1.4: Create SpawnSubAgentTool (Day 2-3, ~4 hours)

Create `src/Aura.Foundation/Tools/BuiltIn/SpawnSubAgentTool.cs`:

```csharp
public record SpawnSubAgentInput
{
    /// <summary>Agent ID to spawn (e.g., "code-review-agent").</summary>
    public required string Agent { get; init; }
    
    /// <summary>Clear, self-contained task description.</summary>
    public required string Task { get; init; }
    
    /// <summary>Optional context to pass to sub-agent.</summary>
    public string? Context { get; init; }
    
    /// <summary>Max steps for sub-agent (default: 10).</summary>
    public int MaxSteps { get; init; } = 10;
    
    /// <summary>Working directory (injected by framework).</summary>
    public string? WorkingDirectory { get; init; }
}

public record SpawnSubAgentOutput
{
    /// <summary>Whether sub-agent completed successfully.</summary>
    public required bool Success { get; init; }
    
    /// <summary>Summary/answer from sub-agent.</summary>
    public required string Summary { get; init; }
    
    /// <summary>Number of steps used.</summary>
    public int StepsUsed { get; init; }
    
    /// <summary>Tokens consumed by sub-agent.</summary>
    public int TokensUsed { get; init; }
    
    /// <summary>Error message if failed.</summary>
    public string? Error { get; init; }
}

public class SpawnSubAgentTool : TypedToolBase<SpawnSubAgentInput, SpawnSubAgentOutput>
{
    private readonly IAgentRegistry _agentRegistry;
    private readonly IReActExecutor _reactExecutor;
    private readonly IToolRegistry _toolRegistry;
    private readonly ILlmProviderRegistry _llmProviderRegistry;
    
    public override string ToolId => "spawn_subagent";
    public override string Name => "Spawn Sub-Agent";
    public override string Description => """
        Spawn an isolated sub-agent for a complex subtask.
        Use when: (1) task is self-contained, (2) context is filling up, 
        (3) you need a fresh perspective.
        The sub-agent gets its own context window and returns a summary.
        """;
    public override IReadOnlyList<string> Categories => ["agent", "execution"];
    public override bool RequiresConfirmation => false;
    
    // ... ExecuteAsync implementation
}
```

**Key implementation details:**
1. Resolve agent from registry
2. Get default LLM provider
3. Get all tools from registry (same tools as parent)
4. Build task prompt with optional context
5. Execute new ReAct loop with `MaxSteps` from input
6. Return summary result

**Acceptance criteria:**
- [ ] Tool implements `TypedToolBase<SpawnSubAgentInput, SpawnSubAgentOutput>`
- [ ] Sub-agent gets fresh ReAct context (no shared state)
- [ ] Parent's `WorkingDirectory` passed to sub-agent
- [ ] Token usage from sub-agent returned in output
- [ ] Integration test: parent spawns sub-agent, gets result

#### Task 1.5: Register SpawnSubAgentTool (Day 3, ~1 hour)

Modify `src/Aura.Foundation/Tools/ToolRegistryInitializer.cs` or equivalent to register the tool at startup.

**Option A**: Add to `BuiltInTools.RegisterBuiltInTools`:
```csharp
public static void RegisterBuiltInTools(
    IToolRegistry registry, 
    IFileSystem fileSystem, 
    IProcessRunner processRunner, 
    IAgentRegistry agentRegistry,
    IReActExecutor reactExecutor,
    ILlmProviderRegistry llmProviderRegistry,
    ILogger logger)
{
    // ... existing registrations ...
    
    // Sub-agent tool
    var subAgentTool = new SpawnSubAgentTool(
        agentRegistry, reactExecutor, registry, llmProviderRegistry);
    registry.RegisterTool(subAgentTool.ToToolDefinition());
}
```

**Option B**: Create a startup task in `Aura.Foundation/Startup/`.

**Acceptance criteria:**
- [ ] Tool registered at startup
- [ ] Available to all agents using ReAct executor
- [ ] DI dependencies properly resolved

#### Task 1.6: Update Agent System Prompts (Day 4, ~2 hours)

Add guidance to key agent prompts about when to use `spawn_subagent`:

Update `prompts/roslyn-coding.prompt` and similar:
```handlebars
## Sub-Agent Spawning

When working on complex tasks, you can spawn sub-agents for isolated subtasks:

Use `spawn_subagent` when:
- A subtask is self-contained (code review, test generation, documentation)
- Context is filling up (>70% used, check with `check_token_budget`)
- You need a "fresh perspective" on a problem
- Parallel-style work: review while implementing

Do NOT spawn sub-agents for:
- Simple single-step operations
- Tasks requiring shared state with current work
- When you're close to finishing
```

**Acceptance criteria:**
- [ ] At least 3 key agent prompts updated
- [ ] Guidance is clear and actionable

#### Task 1.7: Write Tests (Day 4-5, ~4 hours)

Create `tests/Aura.Foundation.Tests/Tools/SpawnSubAgentToolTests.cs`:

| Test | Scenario |
|------|----------|
| `SpawnSubAgent_ValidAgent_ExecutesAndReturnsSummary` | Happy path |
| `SpawnSubAgent_InvalidAgent_ReturnsError` | Agent not found |
| `SpawnSubAgent_SubAgentFails_ReturnsFailure` | Sub-agent ReAct fails |
| `SpawnSubAgent_WithContext_PassesContextToSubAgent` | Context forwarding |
| `SpawnSubAgent_TokensTracked_ReturnsTokenCount` | Token counting |
| `SpawnSubAgent_WorkingDirectory_InheritedFromParent` | Path inheritance |

Create `tests/Aura.Foundation.Tests/Tools/TokenTrackerTests.cs`:

| Test | Scenario |
|------|----------|
| `Add_Tokens_IncreasesUsed` | Basic counting |
| `IsAboveThreshold_At70Percent_ReturnsTrue` | Threshold detection |
| `GetRecommendation_ByUsageLevel_ReturnsCorrectAction` | Recommendation logic |
| `ThreadSafety_ConcurrentAdds_CorrectTotal` | Thread safety |

**Acceptance criteria:**
- [ ] All tests pass
- [ ] Coverage for happy path and error cases
- [ ] No flaky tests

---

### Phase 2: Retry Loops (Week 2)

#### Task 2.1: Extend ReActOptions with Retry Configuration (Day 1, ~1 hour)

Add to `ReActOptions`:
```csharp
public bool RetryOnFailure { get; init; } = false;
public int MaxRetries { get; init; } = 3;
public RetryCondition RetryCondition { get; init; } = RetryCondition.AllFailures;
public string? RetryPromptTemplate { get; init; }
```

Create `RetryCondition` enum in same file or separate.

#### Task 2.2: Implement Retry Loop in ReActExecutor (Day 1-2, ~4 hours)

Refactor `ExecuteAsync` to:
1. Extract current logic to `ExecuteSingleAttemptAsync`
2. Wrap with retry loop
3. Build retry prompts with failure context injection

#### Task 2.3: Create Default Retry Prompt Template (Day 2, ~1 hour)

Create `prompts/react-retry.prompt`:
```handlebars
## Previous Attempt Failed

I tried to complete this task but encountered an error:

**Error:** {{error}}
**Last thought:** {{lastThought}}
**Last action:** {{lastAction}}
**Observation:** {{observation}}

Please try again with a different approach. Learn from the failure above.

## Original Task

{{originalTask}}
```

#### Task 2.4: Implement RetryCondition Detection (Day 3, ~2 hours)

Parse failure reasons from:
- Build errors (look for "error CS" in observations)
- Test failures (look for "failed" + test name patterns)
- Tool errors (check `ToolResult.Success`)
- Max steps (check step count)

#### Task 2.5: Write Retry Tests (Day 3-4, ~4 hours)

| Test | Scenario |
|------|----------|
| `Retry_BuildError_InjectsErrorAndSucceedsOnSecondAttempt` | Build fix |
| `Retry_TestFailure_InjectsTestNameAndFixes` | Test fix |
| `Retry_MaxRetriesExhausted_ReturnsFailure` | Exhaustion |
| `Retry_Disabled_FailsImmediately` | Opt-out |
| `Retry_AllStepsRecorded_AcrossAttempts` | Step continuity |

---

### Phase 3: Token Budget & Polish (Week 3)

#### Task 3.1: Create CheckTokenBudgetTool (Day 1, ~2 hours)

Create `src/Aura.Foundation/Tools/BuiltIn/CheckTokenBudgetTool.cs`:

Read-only tool that returns current budget status.
Requires passing `TokenTracker` reference via `ToolInput.Context` or similar mechanism.

#### Task 3.2: Inject Budget Warnings into ReAct Loop (Day 1, ~2 hours)

When `TokenTracker.IsAboveThreshold()`, inject system message before next LLM call.

#### Task 3.3: Wire CheckTokenBudgetTool (Day 2, ~2 hours)

Challenge: Tool needs access to current `TokenTracker` instance.

**Solution**: Add `TokenTracker` to `ToolInput` context:
```csharp
public record ToolInput
{
    // ... existing ...
    public TokenTracker? TokenTracker { get; init; }
}
```

#### Task 3.4: Documentation (Day 3, ~2 hours)

- [ ] Update `.project/STATUS.md` with new capabilities
- [ ] Add ADR for sub-agent pattern
- [ ] Update `prompts/mcp-tools-instructions.md` with new tools

#### Task 3.5: Integration Testing (Day 4-5, ~4 hours)

End-to-end tests:
- [ ] Workflow step spawns sub-agent for code review
- [ ] Retry loop fixes build error
- [ ] Token budget warning triggers sub-agent spawn

---

## API Changes

### New Endpoints

None — all changes are internal to existing step execution.

### Modified Endpoints

```
POST /api/developer/workflows/{id}/steps/{stepId}/execute
```

New optional body parameters:

```json
{
  "retryOnFailure": true,
  "maxRetries": 3
}
```

### New Tool Definitions

| Tool | Category | Description |
|------|----------|-------------|
| `spawn_subagent` | Built-in | Spawn isolated sub-agent for subtask |
| `check_token_budget` | Built-in | Query remaining context budget |

---

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Sub-agent context isolation incomplete | Medium | Strict tool filtering, no shared state |
| Retry loops increase costs | Medium | Hard caps on retries, token limits |
| Complex debugging with sub-agents | Medium | Full trace logging, step-level output capture |
| Token counting inaccuracy | Low | Use conservative estimates, validate with provider |

---

## Success Metrics

| Metric | Current | Target |
|--------|---------|--------|
| Complex workflow success rate | ~60% | >85% |
| Average retries needed | N/A | <2 |
| Context exhaustion failures | ~15% | <5% |
| Sub-agent spawn rate | N/A | 1-2 per complex workflow |

---

## Open Questions

1. **Sub-agent nesting** — Should sub-agents be allowed to spawn their own sub-agents? (Proposed: No, single level only)

2. **Retry budget** — Should retries share the parent's token budget or get fresh allocations? (Proposed: Fresh, but counted toward workflow total)

3. **Token estimation** — How to estimate tokens before sending to provider? (Proposed: tiktoken for OpenAI, character-based heuristic for Ollama)

4. **Sub-agent tool inheritance** — Should sub-agents get all parent tools or a curated subset? (Proposed: All by default, with optional filtering)

---

## Future Work: Parallel Step Execution

Deferred to v2.1. This feature will allow workflow steps to execute concurrently when they have no dependencies on each other.

### Concept

When planning a workflow, mark step dependencies:

```json
{
  "steps": [
    { "id": "step-1", "description": "Read existing code", "dependsOn": [] },
    { "id": "step-2", "description": "Write unit tests", "dependsOn": ["step-1"] },
    { "id": "step-3", "description": "Write integration tests", "dependsOn": ["step-1"] },
    { "id": "step-4", "description": "Run all tests", "dependsOn": ["step-2", "step-3"] }
  ]
}
```

Steps 2 and 3 can run in parallel (both depend only on step 1).

### Prerequisites (from this spec)

- Sub-agent invocation (each parallel step runs in its own sub-agent)
- Token budget tracking (parallel steps share workflow budget)

### Key Decisions for v2.1

- Add `DependsOn` to `WorkflowStep` schema
- Implement `ParallelStepExecutor` 
- Update planning prompts to generate dependencies
- UI: Show parallel steps in swim lanes

---

## References

- [ADR-012: Tool-Using Agents with ReAct Loop](../adr/012-tool-using-agents.md)
- [Origin Story: Why hve-hack Was Rewritten](../archive/origin-story.md)
- [Layered Fleet Architecture](layered-fleet-architecture.md) — Related orchestration concepts
- Industry discussion: "Ralph loops", "narrow prompts", "context-space management"
