# Aura Workflow ↔ SDD Phase Mapping

> Understanding how Aura's workflow engine implements Spec-Driven Development principles.

## Overview

Aura's workflow system and SDD (Spec-Driven Development) share the same fundamental insight:

> **Good AI-assisted development requires structured phases, not freestyle prompting.**

This document maps the concepts between the two systems.

---

## Phase Correspondence

| SDD Phase | Aura Status | Key Artifact | Purpose |
|-----------|-------------|--------------|---------|
| **Research** | `Analyzing` → `Analyzed` | `AnalyzedContext` (JSON) | Transform uncertainty into verified knowledge |
| **Plan** | `Planning` → `Planned` | `ExecutionPlan` + `Steps[]` | Transform knowledge into actionable strategy |
| **Implement** | `Executing` | Step outputs | Transform strategy into working code |
| **Validate** | `GatePending` / `Verifying` | `VerificationResult` | Transform working code into validated code |

```
SDD:    Research  ───►  Plan  ───►  Implement  ───►  Validate
           │              │              │              │
Aura:  Analyzing     Planning       Executing      GatePending
           ▼              ▼              ▼              ▼
       Analyzed        Planned      (per step)     Completed
```

---

## Detailed Mapping

### Research ↔ Analyzing

| Aspect | SDD | Aura |
|--------|-----|------|
| **Trigger** | `@research .project/backlog/item.md` | `POST /api/stories/{id}/analyze` |
| **Agent** | Research agent | Analysis agent (business-analyst) |
| **Output** | `.project/research/research-{item}-{date}.md` | `Story.AnalyzedContext` (JSON) |
| **Contains** | Open questions answered, technology choices, risks | Core requirements, technical constraints, affected files, suggested approach |

**SDD extras not in Aura:**
- Web search for best practices
- Explicit risk/mitigation table
- Sources with citations

### Plan ↔ Planning

| Aspect | SDD | Aura |
|--------|-----|------|
| **Trigger** | `@plan .project/research/...` | `POST /api/stories/{id}/plan` |
| **Agent** | Plan agent | Business-analyst agent |
| **Output** | `.project/plans/plan-{item}-{date}.md` | `Story.ExecutionPlan` + `StoryStep[]` entities |
| **Contains** | Ordered steps with files, verification, rollback | Ordered steps with capability, language, description |

**SDD extras not in Aura:**
- Explicit rollback plan
- Prerequisites section
- Test strategy table

### Implement ↔ Executing

| Aspect | SDD | Aura |
|--------|-----|------|
| **Trigger** | `@implement .project/plans/...` | `POST /api/stories/{id}/steps/{stepId}/execute` |
| **Agent** | Implement agent | Capability-matched agent (coding, testing, review) |
| **Output** | Code + `.project/changes/changes-{item}-{date}.md` | Code + `StoryStep.Output` (JSON) |
| **Tracking** | Markdown document with progress checkboxes | Database entity with status enum |

**SDD extras not in Aura:**
- Explicit deviation-from-plan tracking
- Commit log in changes document

### Validate ↔ Verifying

| Aspect | SDD | Aura |
|--------|-----|------|
| **Trigger** | `@verify .project/changes/...` | `POST /api/stories/{id}/verify` (auto after waves) |
| **Agent** | Verify agent | Verification service (build, test, lint) |
| **Output** | `.project/reviews/review-{item}-{date}.md` | `Story.VerificationResult` (JSON) |
| **Contains** | Checklist (code quality, testing, architecture) | Pass/fail + error details |

**SDD extras not in Aura:**
- Structured checklist (functional, code quality, testing, docs, architecture)
- Must fix / should fix / suggestions tiers
- Explicit approval decision

---

## Key Differences

### 1. Storage Model

| Aspect | SDD | Aura |
|--------|-----|------|
| **Primary storage** | Markdown files in `.project/` | SQLite/PostgreSQL database |
| **Human-readable** | ✅ Always | ⚠️ JSON blobs require parsing |
| **Version controlled** | ✅ Git-native | ⚠️ Separate from code history |
| **Queryable** | Via grep/search | Via SQL/API |

### 2. Context Isolation

| Aspect | SDD | Aura |
|--------|-----|------|
| **Between phases** | "Start new chat" - strict isolation | Same workflow entity, context may carry |
| **Rationale** | Prevent contamination | Convenience, but risks phase blurring |

### 3. Research Depth

| Aspect | SDD Research | Aura Analyze |
|--------|--------------|--------------|
| **Web search** | ✅ Best practices, docs, examples | ❌ Codebase only |
| **Open questions** | Explicit Q&A format | Implicit in analysis |
| **Risk analysis** | Explicit table | ❌ Not structured |
| **Technology decisions** | Documented with rationale | ❌ Assumed from codebase |

### 4. Backlog Concept

| Aspect | SDD | Aura |
|--------|-----|------|
| **Backlog** | Persistent `.project/backlog/` | ❌ No persistent backlog |
| **Work items** | Survive across sessions | Ephemeral stories |
| **Prioritization** | Explicit in backlog | Per-story priority field |

---

## Using Aura with SDD Mindset

Even without feature changes, you can apply SDD principles to Aura workflows:

### Before Creating a Story

1. **Write a backlog item** (even if just for yourself) in `.project/backlog/`
2. **Identify open questions** that need answering
3. **Create the Aura story** with a thorough description including the questions

### During Analysis

1. **Review AnalyzedContext** before proceeding to Plan
2. **Ask clarifying questions** via workflow chat if analysis is thin
3. **Manually research** anything the agent missed

### During Planning  

1. **Check step granularity** - reject plans with 10+ steps
2. **Verify each step has clear verification criteria**
3. **Ask for rollback plan** if not included

### During Execution

1. **Approve steps thoughtfully** - don't rubber-stamp
2. **Note deviations** from the plan
3. **Request rework** if output doesn't match expectations

### After Completion

1. **Export story artifacts** for your records
2. **Write a brief retrospective** on what worked/didn't
3. **Update your backlog** with learnings

---

## See Also

- [SDD Philosophy](../../anvil/.sdd/philosophy.md) - The methodology in depth
- [Aura Workflow Lifecycle](workflows.md) - Aura-specific workflow documentation
- [Feature: SDD Artifact Export](#) - Upcoming feature to bridge the gap
