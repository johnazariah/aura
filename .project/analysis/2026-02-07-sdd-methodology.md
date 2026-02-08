# SDD Methodology Recommendation

> **Date:** 2026-02-07
> **Purpose:** Propose a rigorous, practical SDD approach for Aura development
> **Audience:** Project maintainers

---

## 1. Current State Assessment

### What Exists Today

| Artifact | Quality | Freshness | Enforced? |
|----------|---------|-----------|-----------|
| Feature specs (~80 files in `features/`) | Variable — no template | Mixed | Header format only (via `Validate-Features.ps1`) |
| ADRs (24 files in `adr/`) | Good — consistent template | ⚠️ None written since Jan 2026 | No |
| `STATUS.md` | Good — actively maintained | ✅ Current (Feb 6, 2026) | No |
| Coding standards | Good | ⚠️ Dated Nov 2025 | No |
| Test strategy | Partial | 🔴 Level 2-3 still "TODO", test count says "205+" but reality is 849 | No |
| Roadmap | 🔴 Stale | References completed P0 items | No |
| Progress reports | Abandoned | Last entry: Dec 12, 2025 | No |
| Guardians (3 YAML files) | Aspirational | No implementation exists — they're design docs disguised as config | No |
| Processes | 1 file | — | No |

### What Works Well

- **ADR format** is the strongest artifact type — good template, consistently followed when created
- **Feature completion ceremony** is the only enforced gate — all 80+ completed features have standardised headers
- **`STATUS.md`** is genuinely useful as a living snapshot

### What Doesn't Work

1. **No feature spec template** — Every spec is freeform. Compare the excellent structure of `unified-wave-orchestration.md` (phased design, "What We Will NOT Build", file change inventory) with `remove-internal-agent-architecture.md` (a deletion manifest with no design section).
2. **No requirements traceability** — Requirements lack IDs. There's no way to trace requirement → design → code → test.
3. **No acceptance criteria** — Features don't define "done" beyond the completion ceremony header.
4. **No design review gate** — Nothing prevents a feature from going spec → code in one shot with no review.
5. **No test plan per feature** — The global test strategy is stale. Individual features don't specify test requirements.
6. **Stale artifacts create noise** — The roadmap, progress reports, and guardian YAMLs look like active process artifacts but are actually dead weight.

### The Meta-Observation

The project has strong **bookkeeping** ceremonies (moving files, updating indexes, conventional commits) but weak **quality** ceremonies (design review, requirements sign-off, test planning per feature). The gap isn't about adding more documents — it's about adding the **right sections** to the documents that already exist.

---

## 2. Proposed SDD Framework

### 2.1 Artifact Types

Replace the current flat `features/` directory with typed artifacts:

| Type | Template | When to Use | Required Sections |
|------|----------|-------------|-------------------|
| **Feature Spec** | Full SDD | New capability, significant change | All sections below |
| **Refactor Brief** | Light SDD | Code restructuring, cleanup, migration | Problem, blast radius, verification |
| **Spike Report** | Research output | Investigation, prototype, evaluation | Question, findings, recommendation |
| **ADR** | Existing template | Architecture decisions | Context, decision, consequences |

### 2.2 Feature Spec Template

```markdown
# Feature: {Title}

> **Status:** 📋 Proposed → 📐 Designing → 🔨 Building → 🧪 Testing → ✅ Complete
> **Author:** {name}
> **Created:** YYYY-MM-DD
> **Completed:** YYYY-MM-DD (added at completion)
> **Priority:** P0 Critical | P1 High | P2 Medium | P3 Low
> **Effort:** {T-shirt size: S/M/L/XL or days}

## Problem Statement

What problem are we solving? Why now? What happens if we don't do this?

## Requirements

| ID | Requirement | Priority | Acceptance Criteria |
|----|-------------|----------|---------------------|
| R1 | {what} | Must | {how we know it's done} |
| R2 | {what} | Should | {how we know it's done} |
| R3 | {what} | Could | {how we know it's done} |

## Non-Goals

What this feature explicitly will NOT do, to prevent scope creep.

## Design

### Architecture

How does this fit into the existing system? Diagrams welcome.

### API Changes

New or modified endpoints, request/response schemas.

### Data Model Changes

Schema changes, migrations.

### Key Decisions

Alternatives considered and why we chose this approach.
(For significant decisions, create a separate ADR.)

## Risks

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| {what could go wrong} | High/Med/Low | High/Med/Low | {how we handle it} |

## Test Plan

| Scenario | Type | Covers | Status |
|----------|------|--------|--------|
| {test description} | Unit | R1 | ☐ |
| {test description} | Integration | R2 | ☐ |

## File Change Inventory

| File | Change | Requirement |
|------|--------|-------------|
| `src/...` | New/Modified/Deleted | R1 |

## Rollback Plan

How do we undo this if it goes wrong?
```

### 2.3 Refactor Brief Template

```markdown
# Refactor: {Title}

> **Status:** 📋 Proposed → 🔨 In Progress → ✅ Complete
> **Author:** {name}
> **Created:** YYYY-MM-DD
> **Effort:** {estimate}

## Problem

What's wrong with the current code? Include metrics if possible
(line counts, duplication counts, dependency counts).

## Blast Radius

| File | Change | Risk |
|------|--------|------|
| ... | ... | ... |

## Approach

How will we do it? In what order? What's the verification step
after each change?

## Verification

- [ ] All tests pass
- [ ] No new warnings
- [ ] {specific checks}
```

### 2.4 Spike Report Template

```markdown
# Spike: {Question}

> **Author:** {name}
> **Date:** YYYY-MM-DD
> **Time-box:** {hours/days spent}

## Question

What are we trying to learn?

## Findings

What did we discover? Include code samples, benchmarks, or
prototypes as appropriate.

## Recommendation

What should we do based on these findings?

## Follow-up

- [ ] Create feature spec for {X}
- [ ] Create ADR for {Y}
```

---

## 3. Process Changes

### 3.1 Requirements Flow

**Current:**
```
Idea → Freeform spec → Code → Completion ceremony → Done
```

**Proposed:**
```
Idea
  ↓
Spike (if needed)          ← time-boxed research
  ↓
Feature Spec (templated)   ← required sections enforced
  ↓
Design Review              ← lightweight: self-review checklist or peer review
  ↓
Implementation             ← code against requirements
  ↓
Verification               ← test plan from spec
  ↓
Completion ceremony        ← existing process, enhanced
  ↓
Done
```

### 3.2 Design Review Gate

For a solo/small-team project, a formal review board is overkill. Instead, use a **self-review checklist** embedded in the template:

```markdown
## Design Review Checklist

- [ ] Problem statement is clear and justified
- [ ] Requirements have acceptance criteria
- [ ] Non-goals are defined
- [ ] At least one alternative was considered
- [ ] Test plan covers all "Must" requirements
- [ ] File change inventory is complete
- [ ] No API breaking changes (or migration plan exists)
```

A spec is ready for implementation when all checkboxes are ticked.

### 3.3 Status Lifecycle

Standardise the feature status values and their meanings:

| Status | Meaning | Gate to Next |
|--------|---------|-------------|
| 📋 Proposed | Idea captured, no design yet | — |
| 📐 Designing | Spec being written | Design review checklist complete |
| 🔨 Building | Implementation in progress | — |
| 🧪 Testing | Verifying against test plan | All test plan items pass |
| ✅ Complete | Done, completion ceremony performed | — |

### 3.4 Enhanced Completion Ceremony

Extend the existing ceremony with two additional checks:

1. **Requirements verification** — Every "Must" requirement has a passing test or documented verification
2. **Test plan completion** — All test plan items in the spec are checked off

The existing `Validate-Features.ps1` script should be extended to check for required sections (not just headers).

---

## 4. Artifact Retirement

### Retire Now

| Artifact | Action | Rationale |
|----------|--------|-----------|
| `.project/progress/` (4 files) | Archive to `.project/archive/progress/` | Last entry Dec 2025. STATUS.md fills this role. |
| `.project/features/roadmap.md` | Archive to `.project/archive/` | References completed P0 items as "Next Sprint." Misleading. |
| `guardians/*.yaml` | Move to `.project/features/unplanned/guardian-system.md` | These are a feature spec disguised as config. No implementation exists. |

### Refresh

| Artifact | Action |
|----------|--------|
| `.project/reference/test-strategy.md` | Update test count (849, not 205+). Fill in Level 2-3 strategy. |
| `.project/reference/coding-standards.md` | Review for currency. Add the code-review findings as input. |
| `.project/adr/README.md` | Ensure ADR index includes ADR-021 through ADR-023. |

---

## 5. Tooling Support

### 5.1 Extend `Validate-Features.ps1`

Currently checks only headers (`Status`, `Completed`). Extend to check:

- **Required sections present:** `## Problem Statement` (or `## Problem` or `## Overview`), `## Design` (or `## Architecture`), `## Non-Goals` (or `## Out of Scope`)
- **Requirements table exists** for specs with status beyond 📐
- **Test plan exists** for specs with status 🧪 or ✅
- Warn (not error) for missing optional sections

### 5.2 Aura Story → Feature Spec Bridge

Aura already has `StoryExporter` (SDD Artifact Export). Wire it so that completed stories automatically generate a skeleton feature spec from their research, plan, and step outputs. This creates the audit trail that the templated spec requires, without manual effort.

### 5.3 Template Files

Create actual template files that can be copied when starting new work:

```
.project/templates/
├── feature-spec.md
├── refactor-brief.md
└── spike-report.md
```

---

## 6. Directory Structure Change

**Current:**
```
.project/features/
├── completed/     (~80 files, all types mixed)
├── design/        (2 files)
├── upcoming/      (5 files)
├── unplanned/     (10 files)
└── README.md
```

**Proposed:**
```
.project/features/
├── completed/     (historical, no structural change needed)
├── in-progress/   (renamed from upcoming/ — active work with full template)
├── proposed/      (renamed from unplanned/ — ideas awaiting spec)
├── spikes/        (new — time-boxed research)
├── templates/     (new — the three templates above)
└── README.md
```

The rename from `upcoming/` to `in-progress/` and `unplanned/` to `proposed/` better reflects what the directories actually contain. `design/` can merge into `in-progress/` since the template now covers design within the spec.

---

## 7. What This Does NOT Recommend

- **Jira, Linear, or any project management tool** — The file-based approach works well for this project's size. Adding a tool would create synchronisation burden.
- **Formal review boards** — Self-review checklists are sufficient for the current team size.
- **Waterfall-style phase gates** — The checklist is a quality aid, not a bureaucratic gate. Spikes and small refactors don't need full SDDs.
- **Rewriting historical specs** — Completed feature specs are historical records. The template applies to new work only.
- **More documents** — The goal is better sections in fewer documents, not more documents.

---

## 8. Implementation Plan

| Step | Action | Effort |
|------|--------|--------|
| 1 | Create the three template files in `.project/templates/` | 30 min |
| 2 | Archive stale artifacts (progress reports, roadmap, guardians) | 15 min |
| 3 | Refresh test strategy document | 1 hr |
| 4 | Extend `Validate-Features.ps1` to check required sections | 2 hrs |
| 5 | Rename directories (`upcoming/` → `in-progress/`, `unplanned/` → `proposed/`) | 15 min |
| 6 | Write ADR-024 (Hybrid Architecture) | 1 hr |
| 7 | Update `STATUS.md` Principles section | 15 min |
| 8 | Apply template to the next 2-3 features as a trial run | Ongoing |
| 9 | Retrospective after 2 weeks — adjust template based on real usage | 30 min |
