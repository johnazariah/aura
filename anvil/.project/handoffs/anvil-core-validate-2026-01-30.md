# Handoff: Anvil Core → Validation

> **Story**: anvil-core  
> **From Stage**: Implement  
> **To Stage**: Validate  
> **Date**: 2026-01-30

## Context

The Anvil CLI test harness has been implemented following the 7-phase plan. All 49 unit tests pass and the CLI commands work. Now we need to validate against a running Aura instance.

## Implementation Summary

| Metric | Value |
|--------|-------|
| Files created | ~25 |
| Tests written | 49 |
| Test pass rate | 100% |
| Phases completed | 7/7 |

## Commits Made

| Commit | Description |
|--------|-------------|
| `4ccbcf0` | Phase 1 - Foundation |
| `eb3d68f` | Phase 2 - Scenario Loading |
| `019b31a` | Phase 3 - Aura Client |
| `33983d0` | Phase 4 - Story Runner |
| `3a7cd41` | Phase 5 - Reporting |
| `24f456c` | Phase 6 - CLI Commands |
| `1111293` | Phase 7 - Sample Scenario |

## What Was Built

### CLI Commands
```
anvil health      # Check Aura API health
anvil validate    # Validate scenario files  
anvil run         # Run scenarios against Aura
```

### Components
- `AuraClient` - HTTP client for Aura Story API
- `ScenarioLoader` - YAML scenario parser
- `StoryRunner` - Orchestrates story execution through Aura
- `ExpectationValidator` - Validates results against expectations
- `ReportGenerator` - Console + JSON reporting

### Sample Scenario
```
scenarios/csharp/hello-world.yaml
```

## Validation Required

### Prerequisites
- [ ] Aura service running at `http://localhost:5300`
- [ ] Test repository exists (for the scenario)

### Validation Steps

1. **Health check**
   ```powershell
   cd c:\work\aura-anvil\anvil
   dotnet run --project src/Anvil.Cli -- health
   ```
   Expected: Shows Aura is healthy

2. **Validate scenarios**
   ```powershell
   dotnet run --project src/Anvil.Cli -- validate scenarios/
   ```
   Expected: Shows 1 valid scenario (already verified ✅)

3. **Run against Aura**
   ```powershell
   dotnet run --project src/Anvil.Cli -- run scenarios/csharp/hello-world.yaml
   ```
   Expected: 
   - Creates story in Aura
   - Executes through analyze → plan → run
   - Validates expectations
   - Produces JSON report

### Integration Test Checklist

- [ ] `anvil health` returns healthy
- [ ] `anvil run` creates a story in Aura
- [ ] Story progresses through lifecycle (Created → Analyzed → Planned → Executing → Completed)
- [ ] Generated code compiles (expectation: `compiles`)
- [ ] Tests pass (expectation: `tests_pass`)
- [ ] Expected file exists (expectation: `file_exists`)
- [ ] JSON report written to `reports/`
- [ ] Console output shows progress (verbose mode)
- [ ] Cleanup: story deleted after run

### Known Dependencies

The hello-world scenario requires:
- A C# console project repository
- Repository path configured in the scenario

**Current scenario config**:
```yaml
repository: fixtures/repos/csharp-console
```

This path needs a real test repository (git submodule or local folder).

## Test Repository Setup

Option 1: Create minimal local repo
```powershell
mkdir -p anvil/fixtures/repos/csharp-console
cd anvil/fixtures/repos/csharp-console
dotnet new console -n HelloWorld
git init
git add -A
git commit -m "Initial commit"
```

Option 2: Use git submodule (preferred for CI)
```powershell
git submodule add <url> anvil/fixtures/repos/csharp-console
```

## Known Issues

- None from unit tests

## Deviations from Plan

- None reported by implementation agent

## Start Validation

```powershell
# 1. Ensure Aura is running
curl http://localhost:5300/health

# 2. Run health check
cd c:\work\aura-anvil\anvil
dotnet run --project src/Anvil.Cli -- health

# 3. If healthy, run the scenario
dotnet run --project src/Anvil.Cli -- run scenarios/csharp/hello-world.yaml
```
