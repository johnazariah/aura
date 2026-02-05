# Index Effectiveness Metrics

Anvil measures how effectively agents use Aura's semantic code index versus falling back to file-level exploration tools.

## Overview

The index effectiveness system tracks tool usage patterns during story execution to answer:

1. **Are agents using the semantic index?** - Ratio of Aura semantic tool calls to total discovery calls
2. **How efficiently do they find code?** - Steps taken before finding relevant code
3. **Are there problematic patterns?** - Detection of "fishing", "guessing", or "direct" behaviors

## Metrics Explained

### Aura Tool Ratio

The primary metric: what percentage of code discovery uses Aura's semantic tools?

| Ratio | Grade | Interpretation |
|-------|-------|----------------|
| ≥70% | Excellent | Agent fully leverages the index |
| 60-69% | Good | Acceptable, meeting target |
| 40-59% | Fair | Room for improvement |
| <40% | Poor | Agent is mostly guessing/fishing |

**Target: ≥60%**

### Tool Classification

**Semantic Tools** (good - use the index):
- `aura_search` - Full-text + embedding search
- `aura_navigate` - Code graph navigation (callers, implementations)
- `aura_inspect` - Type/member introspection

**File-Level Tools** (fallback - bypass the index):
- `read_file` - Direct file access
- `grep_search` - Text pattern matching
- `list_dir` - Directory listing

### Behavioral Patterns

| Pattern | Description | Indicates |
|---------|-------------|-----------|
| **direct** | Semantic tool → immediate file edit | Ideal behavior |
| **fishing** | 5+ consecutive file reads | Agent exploring blindly |
| **guessing** | grep without semantic alternative | Index not being used |

### Other Metrics

- **Steps to First Relevant Code**: How many tool calls before finding target. Lower is better.
- **Backtracking Events**: Files read then abandoned. High values indicate inefficient exploration.

## Using index_usage Expectations

Add to your scenario YAML:

```yaml
expectations:
  - type: index_usage
    description: "Agent should use semantic tools for discovery"
    min_aura_tool_ratio: 0.6    # Default: 60%
    max_steps_to_target: 5       # Optional: fail if too many steps
```

## Baseline Calibration Process

Before relying on these metrics, calibrate against known-good scenarios:

### Step 1: Run Calibration Scenarios

```bash
anvil run scenarios/calibration/*.yaml --output reports/baseline.json
```

### Step 2: Manual Review

1. Review the tool trace for each scenario
2. Determine if the behavior was appropriate
3. Record expected metrics for "good" execution

### Step 3: Set Thresholds

Based on calibration:

| Scenario Type | Expected Aura Ratio | Max Steps |
|---------------|---------------------|-----------|
| Simple lookup (find class) | 80%+ | 2 |
| Add method to class | 60%+ | 5 |
| Cross-file refactor | 50%+ | 10 |
| Bug fix (grep acceptable) | 40%+ | 8 |

### Step 4: Document Baseline

Create `scenarios/calibration/baseline.md` with:
- Run date
- Aura/LLM versions
- Recorded metrics per scenario

## Interpreting Results

### Console Report

```
───────────── Index Effectiveness ─────────────
Scenario        Aura Ratio  Steps  Patterns
add-service     72%         2      direct
fix-bug         45%         6      fishing

Aggregate: 58% Aura tool usage (target: 60%)
```

### JSON Report

```json
{
  "indexEffectiveness": {
    "averageAuraToolRatio": 0.585,
    "totalSemanticCalls": 12,
    "totalFileLevelCalls": 8,
    "scenariosAnalyzed": 2
  },
  "results": [
    {
      "scenarioName": "add-service",
      "indexMetrics": {
        "totalToolCalls": 15,
        "auraSemanticToolCalls": 8,
        "fileLevelToolCalls": 3,
        "auraToolRatio": 0.727,
        "stepsToFirstRelevantCode": 2,
        "backtrackingEvents": 1,
        "detectedPatterns": ["direct"]
      }
    }
  ]
}
```

## Improving Agent Behavior

If metrics are poor:

1. **Low Aura ratio**: Check agent prompts - are they instructed to use semantic tools?
2. **High backtracking**: Agent may not understand the codebase structure
3. **Fishing pattern**: Index might be missing relevant symbols, or agent instructions unclear
4. **Guessing pattern**: Agent falling back to grep when index should work

## Limitations

- **Copilot executor**: Tool tracing only works with internal executor (ReAct agents)
- **LLM non-determinism**: Run multiple times and average for reliable metrics
- **Context matters**: Some scenarios legitimately need more file exploration
