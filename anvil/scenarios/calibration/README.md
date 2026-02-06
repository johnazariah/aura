# Calibration Scenarios

These scenarios establish baselines for index effectiveness metrics.

## Purpose

Run these scenarios to:
1. Verify the metrics system works correctly
2. Establish baseline thresholds for your environment
3. Compare agent performance across LLM models/versions

## Usage

```bash
# Run all calibration scenarios
anvil run scenarios/calibration/*.yaml --output reports/calibration.json

# Review results
cat reports/calibration.json | jq '.indexEffectiveness'
```

## Expected Results

| Scenario | Expected Aura Ratio | Expected Steps | Acceptable Patterns |
|----------|---------------------|----------------|---------------------|
| find-class-simple | ≥80% | ≤2 | direct |
| add-method-existing | ≥60% | ≤5 | direct |

## Updating Baselines

After calibration runs:
1. Review tool traces manually
2. Confirm behavior was appropriate
3. Update expected values in this file
4. Use values to set `index_usage` expectations in other scenarios
