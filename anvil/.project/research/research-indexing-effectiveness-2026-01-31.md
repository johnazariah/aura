# Research: Indexing System Effectiveness

**Backlog Item:** `.project/backlog/indexing-effectiveness.md`
**Researched:** 2026-01-31

## Open Questions Answered

### Q1: How to capture agent reasoning to detect "guessing"?

**Answer:** Tool call tracing is the key. The Aura codebase already captures tool calls through `AgentOutput.ToolCalls` (see [AgentOutput.cs](../../../src/Aura.Foundation/Agents/AgentOutput.cs#L17)). Each `ToolCall` record includes `ToolName`, `Arguments`, and `Result`. By analyzing the sequence and types of tool calls, we can detect behavioral patterns:

**Detection Approach:**
1. **Trace Capture**: During story execution, capture all MCP tool calls with timestamps
2. **Pattern Recognition**: Analyze sequences to detect:
   - **Guessing patterns**: Excessive `file.read`, `grep_search`, `list_dir` in sequence
   - **Knowing patterns**: Direct `aura_search`, `aura_navigate`, `aura_inspect` calls that find the right thing immediately
3. **Metrics Extraction**:
   - Count of Aura semantic tools vs. file-level tools
   - "Steps to target" - how many tool calls before finding relevant code
   - Backtracking ratio - how often the agent reads a file then abandons it

**Industry Practice (Langfuse, LangSmith):**
- Both platforms trace every LLM call and tool invocation
- LangSmith recommends tracking "intermediate steps (tool calls, LLM calls)" as part of evaluation
- Langfuse has dedicated "MCP Tracing" feature for this exact purpose
- Both support "trajectory evaluation" for agents - evaluating the path taken, not just the outcome

**Sources:**
- [Langfuse MCP Tracing](https://langfuse.com/docs/observability/features/mcp-tracing)
- [LangSmith Evaluation Concepts - Runs include intermediate steps](https://docs.langchain.com/langsmith/evaluation-concepts)
- Aura codebase: `src/Aura.Foundation/Agents/AgentOutput.cs`, `src/Aura.Foundation/Llm/FunctionCalling.cs`

---

### Q2: What's a reasonable baseline for index usage?

**Answer:** There's no universal standard, but industry guidance and our requirements suggest:

**Proposed Baseline Metrics:**

| Metric | Baseline | Rationale |
|--------|----------|-----------|
| Aura tool ratio | ≥60% | Majority of discovery should use semantic tools |
| First-try success | ≥70% | Agent should find relevant code without backtracking 7/10 times |
| Steps to target | ≤3 | Should find the right code within 3 tool calls |
| Excessive file reads | ≤5 per step | Reading >5 files suggests fishing expedition |
| grep fallback rate | ≤20% | Grep should be exception, not norm |

**How to Establish:**
1. **Initial Calibration Run**: Execute 10-20 known scenarios and measure current performance
2. **Manual Review**: Have a human review whether each tool call sequence was efficient
3. **Set Baseline**: Use calibration run as baseline, then measure improvements

**Industry Guidance:**
- LangSmith: "Start with manually curated examples. Create 5-10 examples of what 'good' looks like"
- Langfuse: Track metrics over time, detect regression vs. improvement trends
- Both emphasize relative comparison (current vs. previous) over absolute thresholds

**Sources:**
- [LangSmith Best Practices - Building datasets](https://docs.langchain.com/langsmith/evaluation-concepts#best-practices)
- [Langfuse Observability - Token & Cost Tracking](https://langfuse.com/docs/observability/features/token-and-cost-tracking)

---

### Q3: How to test index quality independent of agent behavior?

**Answer:** Separate index evaluation from agent evaluation using direct index queries and ground-truth datasets.

**Approach 1: Direct Index Retrieval Tests**
- Create a dataset of (query, expected_files, expected_symbols) tuples
- Query the index directly via `aura_search` MCP tool
- Measure recall and precision without involving agent reasoning

```yaml
# Example test case
- query: "file writing utilities"
  expected_results:
    - path: "src/Aura.Module.Developer/Tools/WriteFileTool.cs"
    - symbol: "WriteFileTool"
  min_recall: 0.8  # Expected file should appear in top-k results
```

**Approach 2: Ground Truth Navigation**
- Create scenarios where the correct file/symbol is known
- Use `aura_navigate(operation: "definition")` to verify the index finds it
- Measure success rate independent of agent

**Approach 3: Comparative Retrieval**
- Run same query through `aura_search` vs. `grep_search`
- `aura_search` should return more semantically relevant results
- Manual scoring of relevance (1-5 scale)

**What to Measure:**
| Metric | Description |
|--------|-------------|
| Recall@k | % of expected results in top-k |
| Precision@k | % of top-k results that are relevant |
| Semantic relevance | Human-scored relevance (1-5) |
| Latency | Time to return results |

**Sources:**
- RAG evaluation literature (RAGAS, BEIR benchmarks)
- Aura codebase: `src/Aura.Api/Mcp/McpHandler.cs` - `SearchAsync` implementation

---

## Technical Approach

### Recommended Approach

Implement a two-layer evaluation:

1. **Index Quality Layer** (agent-independent)
   - Ground-truth dataset of queries with expected results
   - Direct MCP tool calls to measure index performance
   - Run as part of Anvil's validation suite

2. **Agent Behavior Layer** (end-to-end)
   - Capture tool call sequences during story execution
   - Analyze patterns to detect guessing vs. knowing
   - Calculate effectiveness metrics

### Technology Choices

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Tool call capture | Extend `StoryResult` model | Already have `ExpectationResult`, add `ToolTrace` |
| Pattern detection | Heuristic rules first | LLM-as-judge later if needed |
| Metric storage | JSON in scenario output | Simple, no new infra needed |
| Baseline creation | Calibration run + manual review | Industry best practice |

### Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     Anvil CLI                                    │
├─────────────────────────────────────────────────────────────────┤
│  StoryRunner                                                     │
│    │                                                             │
│    ├── Execute story via Aura API                                │
│    │                                                             │
│    └── NEW: Capture tool trace via new API endpoint              │
│                                                                  │
│  IndexEffectivenessAnalyzer (NEW)                                │
│    │                                                             │
│    ├── Classify tools: semantic vs. file-level                   │
│    │                                                             │
│    ├── Detect patterns: fishing, backtracking, direct            │
│    │                                                             │
│    └── Calculate metrics: ratio, steps, success rate             │
│                                                                  │
│  ReportGenerator                                                 │
│    │                                                             │
│    └── Add index effectiveness section to report                 │
└─────────────────────────────────────────────────────────────────┘
```

### Integration Points

1. **Aura API**: New endpoint to retrieve step execution tool trace
   - `GET /api/developer/stories/{id}/steps/{stepId}/trace`
   - Returns list of tool calls with timestamps
   
2. **Anvil StoryResult**: Extend to include tool trace
   - Add `IReadOnlyList<ToolCallRecord> ToolTrace` property
   
3. **Anvil Scenario**: Add index effectiveness expectations
   ```yaml
   expectations:
     - type: index_usage
       min_aura_tool_ratio: 0.6
       max_steps_to_target: 3
   ```

### Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Aura API doesn't expose tool calls | Medium | Need to add endpoint; tool data exists in `AgentOutput.ToolCalls` |
| LLM non-determinism affects metrics | High | Use multiple runs, calculate confidence intervals |
| Pattern detection too simplistic | Medium | Start simple, iterate based on real data |
| Baseline calibration is time-consuming | Low | Can do incrementally with each scenario |

---

## Implementation Dependencies

1. **Aura API Enhancement**: Must expose tool call trace per step
2. **Story Execution Core**: Already complete ✅
3. **Regression Detection**: Synergizes - store index metrics over time

---

## Ready for Planning

This item is ready to proceed to the planning phase. Key deliverables:

1. **Aura API endpoint** for tool trace retrieval
2. **Anvil tool trace capture** in StoryRunner
3. **IndexEffectivenessAnalyzer** service
4. **Index usage expectation type** for scenarios
5. **Report section** for index effectiveness metrics
6. **Ground-truth dataset** for index-only testing (5-10 queries)
