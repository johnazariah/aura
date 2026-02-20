# Feature: {Title}

> **Status:** 📋 Proposed
> **Author:** {name}
> **Created:** YYYY-MM-DD
> **Priority:** P0 Critical | P1 High | P2 Medium | P3 Low
> **Effort:** S / M / L / XL

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

- ...

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
| {what could go wrong} | H / M / L | H / M / L | {how we handle it} |

## Test Plan

| Scenario | Type | Covers | Status |
|----------|------|--------|--------|
| {test description} | Unit | R1 | ☐ |
| {test description} | Integration | R2 | ☐ |

## File Change Inventory

| File | Change | Requirement |
|------|--------|-------------|
| `src/...` | New / Modified / Deleted | R1 |

## Rollback Plan

How do we undo this if it goes wrong?

---

## Design Review Checklist

- [ ] Problem statement is clear and justified
- [ ] Requirements have acceptance criteria
- [ ] Non-goals are defined
- [ ] At least one alternative was considered
- [ ] Test plan covers all "Must" requirements
- [ ] File change inventory is complete
- [ ] No API breaking changes (or migration plan exists)

---

## Completion

> _Added by completion ceremony — do not fill in advance._
>
> **Status:** ✅ Complete
> **Completed:** YYYY-MM-DD
