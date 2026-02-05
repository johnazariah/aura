# Backlog: Executor Calibration & Comparison

**Capability:** Cross-cutting - Validate all execution paths produce quality code
**Priority:** High - Multiple executors must all meet quality bar

## Context

Aura supports multiple execution paths for story implementation:

| Executor | Agent | Mode | Characteristics |
|----------|-------|------|-----------------|
| `internal` | RoslynCodingAgent | Deterministic | JSON extraction → Roslyn services, 100% semantic tools |
| `copilot` | GitHubCopilotDispatcher | One-shot | VS Code Copilot backend, `--yolo` mode, file.modify |
| (future) | LanguageSpecialistAgent | ReAct loop | Tool calling, multi-language, config-driven |

Each executor has different strengths and failure modes. Calibration ensures:
1. All executors can complete the same scenarios
2. Quality metrics are comparable across executors
3. Regressions in any executor are detected

## Functional Requirements

### Multi-Executor Scenarios

Each calibration scenario should be runnable with different executors:

```yaml
# Same scenario, different executors
name: add-method-to-service
executor: internal  # or: copilot, polyglot
```

Track metrics by executor to compare:
- Compilation success rate
- Test pass rate  
- Aura tool ratio (where applicable)
- Time to completion
- Token usage

### Executor-Specific Expectations

| Executor | Expected Aura Tool Ratio | Notes |
|----------|-------------------------|-------|
| `internal` | 100% | Roslyn agent uses only Aura tools |
| `copilot` | 0% (exempt) | Copilot uses file.modify, that's fine |
| `polyglot` | >60% | Should prefer semantic tools when available |

### Copilot `--yolo` Mode Validation

The Copilot executor runs in `--yolo` mode (no human confirmation). Validate:

- [ ] Task completion without intervention
- [ ] Code compiles after changes
- [ ] No destructive changes (deleting unrelated files)
- [ ] Reasonable scope (doesn't over-engineer)

### Multi-Language Calibration

Extend calibration beyond C# to validate language-specific agents:

| Language | Fixture | Agent | Semantic Tools |
|----------|---------|-------|----------------|
| C# | `csharp-webapi` | RoslynCodingAgent | Roslyn refactoring |
| Python | `python-fastapi` | LanguageSpecialistAgent | TreeSitter + file ops |
| TypeScript | `typescript-express` | LanguageSpecialistAgent | TreeSitter + file ops |

Create equivalent scenarios for each language:
- Level 1: Simple file creation
- Level 2: Add function/method to existing file
- Level 3: Multi-file feature (model + service)
- Level 4: Cross-cutting change (add type hints, logging)
- Level 5: Pattern-following (create service like existing)

### Fixture Requirements

Each language fixture needs:
- Standalone git repository
- Builds/runs cleanly
- Has existing patterns to follow (model, service, etc.)
- Added to git safe.directory for service account

**Python fixture (`python-fastapi`):**
```
python-fastapi/
├── pyproject.toml
├── src/
│   ├── models/
│   │   └── user.py
│   ├── services/
│   │   └── user_service.py
│   ├── routers/
│   │   └── users.py
│   └── main.py
└── tests/
```

**TypeScript fixture (`typescript-express`):**
```
typescript-express/
├── package.json
├── tsconfig.json
├── src/
│   ├── models/
│   │   └── User.ts
│   ├── services/
│   │   └── UserService.ts
│   ├── routes/
│   │   └── users.ts
│   └── app.ts
└── tests/
```

## Metrics & Reporting

### Per-Executor Metrics

```json
{
  "executor": "internal",
  "scenarios": {
    "passed": 8,
    "failed": 2,
    "passRate": 0.8
  },
  "auraToolRatio": 1.0,
  "avgDurationSeconds": 45.2,
  "avgTokens": 12500
}
```

### Comparison Report

Generate comparison across executors for same scenarios:

```
Scenario: add-method-to-service
┌──────────┬────────┬─────────────┬──────────┐
│ Executor │ Result │ Aura Ratio  │ Duration │
├──────────┼────────┼─────────────┼──────────┤
│ internal │ PASS   │ 100%        │ 42s      │
│ copilot  │ PASS   │ N/A         │ 38s      │
│ polyglot │ PASS   │ 72%         │ 55s      │
└──────────┴────────┴─────────────┴──────────┘
```

### Regression Detection

- Track results over time by executor
- Alert when pass rate drops for specific executor
- Identify scenarios that regressed

## Implementation Notes

### Phase 1: Internal Executor (Current)
- [x] Basic scenario runner
- [x] `index_usage` expectation with Aura tool ratio
- [ ] Level 3+ scenarios passing reliably

### Phase 2: Copilot Executor
- [ ] Implement `copilot` executor using Copilot CLI
- [ ] Capture tool call logs from Copilot execution
- [ ] Validate `--yolo` mode completion

### Phase 3: Polyglot Executor
- [ ] Implement `polyglot` executor using LanguageSpecialistAgent
- [ ] Create Python fixture and scenarios
- [ ] Create TypeScript fixture and scenarios

### Phase 4: Comparison Reporting
- [ ] Generate cross-executor comparison reports
- [ ] Track metrics over time
- [ ] CI integration for regression detection

## Open Questions

- How to capture tool calls from Copilot CLI execution?
- Should polyglot use same prompts as language configs in `agents/languages/*.yaml`?
- What's the right threshold for polyglot Aura tool ratio?
- How to handle language-specific validation (Python type checking, TS compilation)?
