# Anvil Vision

> **Anvil is the first non-human user of Aura.**

## Purpose

Anvil exercises Aura end-to-end the way a real user would—through all surfaces (API, Extension, MCP)—to detect when new development breaks existing functionality.

Anvil assumes Aura compiles, passes unit tests, and is deployed. **It validates the experience, not the hygiene.**

## Who It's For

The Aura developer who:
- Wants confidence that Aura works correctly today and will keep working as it evolves
- Needs a guardrail that grows alongside an organically evolving system
- Wants to stop worrying whether the next shiny feature broke something that used to work

## Success Criteria

When Anvil works:
- Developer runs it overnight or after building a big feature
- It tells them whether existing functionality still works
- If something broke, it's clear what and where

---

## Core Capabilities

### 1. Story Execution (Supervised) ✅ Complete (2026-01-31)

**Input:** Plain English story definition  
**Mode:** Human-in-the-loop (Anvil is the human)  
**Output:** Verified code via step-by-step agent execution

Anvil acts as the approver at each gate, validating that the step-by-step process works as designed.

### 2. Story Execution (Autonomous) ✅ Complete (2026-01-31)

**Input:** Plain English story definition  
**Mode:** Unattended (no interaction)  
**Output:** PR-ready code

Exercises both execution paths:
- Copilot CLI + Aura MCP tools
- Aura's internal agent fleet

### 3. Issue-to-PR Pipeline

**Input:** GitHub Issue (from test account with curated issue suite)  
**Mode:** Unattended  
**Output:** PR linked to issue

Validates the full GitHub integration path. Requires:
- Dedicated test GitHub account
- Curated suite of test issues at various complexity levels

### 4. MCP Tool Effectiveness

**Mode:** Integration (not unit)  
**Validation:** Tools ARE called when they should be

We don't codify when the LLM should call tools—we verify that it actually uses Aura's MCP tools instead of falling back to basic file operations. Success = tools invoked; failure = tools available but ignored.

### 5. Indexing System Effectiveness

**Mode:** Integration  
**Validation:** Agents use the index effectively

Not just "does search return results" but "did the agent find the right context instead of grepping and guessing." Measured by:
- Agent found relevant code without exhaustive file scanning
- Agent cited indexed context in its reasoning

### 6. Extension UI Validation

**Mode:** UI automation  
**Validation:** Sensible user experience

Validates:
- Renders without errors
- Buttons do what labels say
- State is displayed correctly
- Ceremony detection: too many clicks to do common tasks = flagged

---

## Story Sophistication Ladder

Stories range in complexity. Anvil should test across this spectrum:

| Level | Description | Example |
|-------|-------------|---------|
| 1 | Simple greenfield | "Create Hello World console app" |
| 2 | Single file feature | "Add a method that does X" |
| 3 | Multi-file feature | "Add an API endpoint with tests" |
| 4 | Cross-cutting change | "Add logging to all services" |
| 5 | Pattern-following | "Create a new microservice following existing patterns" |

**Validation is functional, not structural:**
- Does the code compile? ✓
- Does it run? ✓
- Does it do what was intended? ✓
- Does it match a golden file? ✗ (not this)

---

## What Anvil Is Not

- **Not a unit test framework** — Aura has those already
- **Not a code style checker** — Generated code should work, not match templates
- **Not a build system** — Assumes Aura is already built and deployed
- **Not continuous** — Run on-demand or scheduled, not on every commit

---

## Usage Model

1. Developer makes changes to Aura
2. Deploys locally (Update-LocalInstall.ps1)
3. Runs Anvil suite overnight or on-demand
4. Reviews results in the morning
5. Confidence to continue or clear signal something broke
