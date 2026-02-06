---
title: Spec-Driven Development Philosophy
description: The methodology that transforms AI-assisted coding from prompt to spec to code
maturity: stable
---

# Spec-Driven Development (SDD) Philosophy

> The methodology that transforms AI-assisted coding from "prompt → code" into "prompt → spec → code."

## Definition

**Spec-Driven Development** is a disciplined workflow where LLMs generate and execute detailed specifications—built from architecture, history, and current code context—to produce consistent, high-quality software changes through a **Research → Plan → Implement → Validate** pipeline.

Or even shorter:

> **SDD = Context-rich spec → deterministic-ish code generation.**

---

## Core Principle

**The spec is the key artifact.** It is not an English request like "add a new API endpoint." It is an executable contextual document that enables deterministic, repeatable LLM behavior.

## The Four-Phase Workflow

```
┌─────────────┐    ┌─────────────┐    ┌─────────────┐    ┌─────────────┐
│  RESEARCH   │───►│    PLAN     │───►│  IMPLEMENT  │───►│  VALIDATE   │
│             │    │             │    │             │    │             │
│ Gather      │    │ Generate    │    │ Execute     │    │ Check       │
│ context     │    │ spec        │    │ spec        │    │ alignment   │
└─────────────┘    └─────────────┘    └─────────────┘    └─────────────┘
```

### Phase 1: Research

**Purpose:** Transform uncertainty into verified knowledge.

The agent gathers context to build an accurate mental model:

- Architecture documents and ADRs
- Cross-cutting concerns (logging, error handling, security)
- Code history and existing patterns
- Current state of the repository

**Output:** `.project/research/{{YYYY-MM-DD}}-topic-research.md`

**Key Constraint:** Research-only. Never plans or implements. Documents findings with evidence and citations.

**Goal:** Prevent the LLM from:
- Inventing new services that already exist
- Duplicating patterns
- Misunderstanding domain logic
- Re-implementing infrastructure (loggers, repositories, etc.)

### Phase 2: Plan

**Purpose:** Transform knowledge into actionable strategy.

Generate a structured, human-readable specification describing:

| Element | Purpose |
|---------|---------|
| **Problem** | What we're solving and why |
| **Approach** | How we'll solve it |
| **Tasks** | Step-by-step implementation plan |
| **Constraints** | Architectural rules, patterns to follow |
| **Success Criteria** | What "done" looks like |
| **Validation Steps** | How to verify correctness |

**Output:** `.project/plans/{{YYYY-MM-DD}}-topic-plan.md`

**Key Constraint:** Planning-only. Requires research first. Never implements actual code.

The spec is clear enough for both humans and LLMs to execute.

### Phase 3: Implement

**Purpose:** Transform strategy into working code.

The LLM executes the spec:
- Generating code following specified patterns
- Writing tests (TDD where appropriate)
- Refactoring as needed
- Following architectural constraints

**Output:** Working code + `.project/changes/{{YYYY-MM-DD}}-topic-changes.md`

**Key Constraint:** Requires completed plan files. Follows plan exactly, no improvisation.

### Phase 4: Validate

**Purpose:** Transform working code into validated code.

Check the result for:
- **Correctness** — Does it work? Run tests, lint, build.
- **Completeness** — Does it cover all requirements from the spec?
- **Alignment** — Does it follow architecture and standards?
- **Compliance** — Does it match coding guidelines and ADRs?

**Output:** `.project/reviews/{{YYYY-MM-DD}}-topic-review.md`

**Key Constraint:** Review-only. Never modifies code. Identifies gaps that require iteration back to earlier phases.

---

## Critical Rule: Clear Context Between Phases

🔴 **Always start a new chat or clear context between phases.**

Each phase has different instructions and goals. Accumulated context causes confusion and contamination:

```
Research → [clear] → Plan → [clear] → Implement → [clear] → Validate
```

Research findings are preserved in **files**, not chat history. Clean context lets each phase work optimally.

---

## When to Use the Full Workflow

| Use Full SDD When... | Use Quick Edits When... |
|----------------------|-------------------------|
| Changes span multiple files | Fixing a typo |
| Learning new patterns/APIs | Adding a log statement |
| External dependencies involved | Refactoring < 50 lines |
| Requirements are unclear | Change is obvious |

**Rule of Thumb:** If you need to understand something before implementing, use SDD.

---

## The Spec as Executable Context

A proper spec is not just instructions—it's a **rich environment**:

```
Spec: Feature X
├── Architecture References
│   ├── Which layers are involved
│   ├── Which services to use/extend
│   └── Dependency direction rules
├── Historical Patterns
│   ├── How similar features were built
│   ├── Existing code to reference
│   └── Anti-patterns to avoid
├── Constraints
│   ├── ADR decisions that apply
│   ├── Cross-cutting concerns
│   └── Performance/security requirements
├── Expected Changes
│   ├── Files to create/modify
│   ├── Tests to write
│   └── Documentation to update
├── Tasks
│   ├── Ordered implementation steps
│   ├── Dependencies between tasks
│   └── Verification checkpoints
└── Rationale
    ├── Why this approach
    ├── Alternatives considered
    └── Trade-offs accepted
```

---

## Context Is Everything

SDD isn't about clever prompts. It's about assembling the right **context window**:

| Context Type | What It Provides |
|--------------|------------------|
| **ADRs** | Architectural decisions and their rationale |
| **Coding Guidelines** | Language-specific conventions |
| **Folder Structure** | Where things belong |
| **Cross-Cutting Concerns** | Logging, error handling, security patterns |
| **Existing Patterns** | How similar things were built |
| **Domain Knowledge** | Business rules and terminology |

The research phase assembles this context so the LLM behaves like a **seasoned engineer familiar with your codebase**.

---

## Agents, Commands, and Instructions

"Agents" are higher-order versions of the same pattern. What matters is:

| Component | Description |
|-----------|-------------|
| **Instructions** | The algorithm/program the agent follows |
| **Knowledge** | Standards and guardrails |
| **Triggers** | Commands or events that invoke capability |

SDD builds **reusable, modular units of capability** that agents execute reliably.

---

## Why This Works

### The Old Way
```
Human: "Add a login endpoint"
LLM: *invents patterns, duplicates code, ignores ADRs*
Human: "No, use the existing auth service..."
LLM: *partially fixes, introduces new issues*
[Repeat 5x]
```

### The SDD Way
```
Research: Understand auth patterns, existing services, ADRs
Plan: Spec says "extend AuthService, use existing middleware, follow ADR-003"
Implement: LLM follows spec exactly
Validate: Matches spec, follows patterns, tests pass
[Done in 1 iteration]
```

---

## Principles for Effective SDD

1. **Never skip phases.** Each phase produces artifacts that feed the next.

2. **Clear context between phases.** Accumulated context contaminates focus.

3. **Specs are living documents.** Update them when requirements change.

4. **Context quality determines output quality.** Invest in good ADRs and guidelines.

5. **Validation is not optional.** Always check alignment with architecture.

6. **Iterate on the spec, not the code.** If implementation is wrong, the spec was incomplete.

7. **Constraints change goals.** When AI knows it cannot implement during research, it optimizes for verified truth instead of plausible code.

---

## Artifact Maturity Lifecycle

All SDD artifacts follow a four-stage maturity model:

| Stage | Description |
|-------|-------------|
| `experimental` | Early exploration, may change significantly |
| `preview` | Usable but not yet stable, feedback welcome |
| `stable` | Production-ready, breaking changes rare |
| `deprecated` | Being phased out, migrate to replacement |

---

## References

- `.sdd/prompts/research.md` — How to run the research phase
- `.sdd/prompts/plan.md` — How to generate specifications
- `.sdd/prompts/implement.md` — How to execute specifications
- `.project/adr/` — Architecture Decision Records
- `.project/coding-guidelines/` — Language-specific conventions
- `.project/` — Workflow artifacts (research, plans, changes, reviews)
