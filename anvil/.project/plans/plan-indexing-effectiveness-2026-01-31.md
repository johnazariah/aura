# Indexing System Effectiveness

**Status:** Plan  
**Created:** 2026-01-31  
**Research:** [research-indexing-effectiveness-2026-01-31](../research/research-indexing-effectiveness-2026-01-31.md)  
**Backlog:** [indexing-effectiveness](../backlog/indexing-effectiveness.md)

## Summary

Implement a two-layer evaluation system to measure and report how effectively agents use Aura's semantic index vs. falling back to grep/file scanning patterns.

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Tool trace capture | Extend Aura API with `/trace` endpoint | Tool data exists in `AgentOutput.ToolCalls`, needs exposure |
| Pattern detection | Heuristic rules | Start simple; LLM-as-judge adds cost/latency |
| Metric storage | Embed in `StoryResult` and JSON report | No new infrastructure needed |
| Baseline creation | Calibration runs + manual review | Industry best practice (LangSmith, Langfuse) |

## Implementation Steps

### Phase 1: Aura API Enhancement

1. [ ] Add `GET /api/developer/stories/{id}/steps/{stepId}/trace` endpoint
   - Returns list of tool calls with timestamps from step execution
   - Source data: `StoryStep.Output` contains serialized `AgentOutput`
   - Response: `{ toolCalls: [{ tool, arguments, result, timestamp }] }`

2. [ ] Parse `AgentOutput.ToolCalls` from stored step output
   - `StoryStep.Output` stores JSON; deserialize and extract tool calls
   - Add helper method to `StoryService` or new `StepTraceService`

### Phase 2: Anvil Tool Trace Capture

3. [ ] Add `ToolCallRecord` model to Anvil
   ```csharp
   public record ToolCallRecord(
       string ToolName,
       string Arguments,
       string? Result,
       DateTimeOffset? Timestamp);
   ```

4. [ ] Extend `IAuraClient` with `GetStepTraceAsync(Guid storyId, Guid stepId)`
   - Calls new Aura API endpoint
   - Returns `IReadOnlyList<ToolCallRecord>`

5. [ ] Extend `StoryResult` with `IReadOnlyList<ToolCallRecord> ToolTrace`
   - Populated after story completes by fetching trace for each step

6. [ ] Update `StoryRunner` to fetch trace after completion
   - After `WaitForCompletionAsync`, call `GetStepTraceAsync` for each step
   - Aggregate into `StoryResult.ToolTrace`

### Phase 3: Index Effectiveness Analyzer

7. [ ] Create `IIndexEffectivenessAnalyzer` interface
   ```csharp
   public interface IIndexEffectivenessAnalyzer
   {
       IndexEffectivenessMetrics Analyze(IReadOnlyList<ToolCallRecord> toolTrace);
   }
   ```

8. [ ] Create `IndexEffectivenessMetrics` record
   ```csharp
   public record IndexEffectivenessMetrics(
       int TotalToolCalls,
       int AuraSemanticToolCalls,     // aura_search, aura_navigate, aura_inspect
       int FileLevelToolCalls,         // file.read, grep_search, list_dir
       double AuraToolRatio,           // Semantic / Total (target: ≥60%)
       int StepsToFirstRelevantCode,   // Tool calls before finding target
       int BacktrackingEvents,         // File read then abandoned
       IReadOnlyList<string> DetectedPatterns); // "guessing", "direct", "fishing"
   ```

9. [ ] Implement `IndexEffectivenessAnalyzer`
   - Classify tools into categories:
     - **Semantic tools**: `aura_search`, `aura_navigate`, `aura_inspect`
     - **File-level tools**: `file.read`, `file.list`, `grep_search`, `list_dir`
   - Detect patterns:
     - **Fishing**: 5+ consecutive file reads without semantic tool
     - **Guessing**: grep for terms the index should know
     - **Direct**: Semantic tool → immediate file edit

### Phase 4: Scenario Expectations

10. [ ] Add `index_usage` expectation type to `Expectation` model
    ```yaml
    expectations:
      - type: index_usage
        description: "Agent should use semantic tools for discovery"
        min_aura_tool_ratio: 0.6
        max_steps_to_target: 3
    ```

11. [ ] Implement `IndexUsageExpectationValidator`
    - Validates metrics against thresholds from expectation
    - Returns pass/fail with detailed message

12. [ ] Register validator in `ExpectationValidator`

### Phase 5: Report Generation

13. [ ] Add index effectiveness section to `ReportGenerator`
    - Per-scenario: tool breakdown, patterns detected, metrics
    - Aggregate: average Aura tool ratio across scenarios
    - Trend: compare to previous runs (if available)

14. [ ] Create markdown report format:
    ```markdown
    ## Index Effectiveness

    | Scenario | Aura Ratio | Steps to Target | Patterns |
    |----------|------------|-----------------|----------|
    | add-service | 72% | 2 | direct |
    | fix-bug | 45% | 6 | fishing |

    **Aggregate:** 58.5% Aura tool usage (target: 60%)
    ```

### Phase 6: Baseline Calibration

15. [ ] Document baseline calibration process in `docs/`
    - Run N scenarios with known expected behavior
    - Manual review to confirm metrics accuracy
    - Record baseline thresholds

16. [ ] Add sample scenarios for calibration
    - One "easy" scenario (should use direct semantic lookup)
    - One "hard" scenario (acceptable to use more file reads)

## Tool Classification Reference

| Tool Name | Category | Notes |
|-----------|----------|-------|
| `aura_search` | Semantic | Full-text + embedding search |
| `aura_navigate` | Semantic | Code graph navigation |
| `aura_inspect` | Semantic | Type/member introspection |
| `file.read` | File-level | Direct file access |
| `file.list` | File-level | Directory listing |
| `grep_search` | File-level | Text pattern matching |
| `list_dir` | File-level | Copilot file browser |
| `read_file` | File-level | Copilot file read |

## Success Criteria

- [ ] API endpoint returns tool trace for completed steps
- [ ] Anvil captures and stores tool trace in results
- [ ] Metrics calculated correctly (manual verification)
- [ ] Report shows meaningful patterns
- [ ] At least one scenario uses `index_usage` expectation

## Risks & Mitigations

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Step output doesn't include tool calls | Medium | Verify `AgentOutput` serialization in `StoryStep.Output` |
| LLM non-determinism affects metrics | High | Use multiple runs, calculate confidence intervals |
| Pattern detection too simplistic | Medium | Start simple, iterate based on real data |
| Copilot executor doesn't trace tools | High | Initially support internal executor only; add Copilot later |

## Dependencies

- Story execution must complete successfully to capture trace
- Internal executor (ReAct agents) stores tool calls in `AgentOutput`
- Copilot executor may need separate tracing mechanism (future work)

## Out of Scope

- LLM-as-judge evaluation (future enhancement)
- Real-time index effectiveness dashboard
- Automatic index tuning based on metrics
