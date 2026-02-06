# Backlog: Story Execution Core

**Status:** ✅ Complete  
**Completed:** 2026-01-31

**Capability:** 1 & 2 - Story Execution (Supervised & Autonomous)  
**Priority:** High - Foundation for everything else

## Functional Requirements

### Scenario Definition
- Define a story in plain English with expected outcomes
- Specify which executor to use (Copilot CLI, internal agents, or both)
- Specify sophistication level (greenfield simple → pattern-following complex)
- Include validation criteria (compiles, runs, functional checks)

### Execution Orchestration
- Submit story to Aura for analysis and planning
- Monitor progress through execution phases
- In supervised mode: act as gate approver at each step
- In autonomous mode: run to completion without intervention

### Result Validation
- Verify generated code compiles
- Verify generated code runs (where applicable)
- Verify functional intent is met (not structural comparison)
- Capture execution metadata (duration, steps, tool usage)

### Result Reporting
- Clear pass/fail status per scenario
- Details on what failed and where
- Comparison with previous runs (regression detection)

## Open Questions (for Research)

- How to express "functional intent" validation without golden files?
- How to handle flaky scenarios (LLM non-determinism)?
- What's the right granularity for a scenario?
