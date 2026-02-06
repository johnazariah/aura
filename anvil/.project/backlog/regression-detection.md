# Backlog: Regression Detection & Reporting

**Capability:** Cross-cutting - Know when things get worse  
**Priority:** High - Core value proposition

## Functional Requirements

### Result Persistence
- Store results from each Anvil run
- Track pass/fail status per scenario over time
- Capture execution metadata (duration, tool usage, errors)

### Regression Detection
- Compare current run against previous runs
- Flag scenarios that used to pass but now fail
- Flag scenarios that got significantly slower
- Flag scenarios where tool usage patterns degraded

### Trend Analysis
- Success rate over time per scenario
- Success rate by complexity level
- Tool usage trends (are Aura tools being used more or less?)

### Reporting Formats
- Console output for interactive use
- JSON/structured output for programmatic use
- HTML report for human review
- Diff view against previous run

### Alerting (Future)
- Threshold-based alerts (success rate drops below X%)
- Notification channels (email, Teams, GitHub Issue)

## Open Questions (for Research)

- What storage backend for result history?
- How to handle LLM non-determinism in trend analysis?
- What's the right comparison granularity (run vs. scenario vs. step)?
