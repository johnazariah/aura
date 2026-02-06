# Plan: SDD Templates Library

**Backlog Item:** Create comprehensive template library for SDD framework
**Created:** 2026-01-31
**Status:** Ready for Implementation
**Depends On:** SDD Architecture Cleanup (in progress)

## Problem Statement

The `.sdd/templates/` folder should contain all scaffolding needed to:
1. Bootstrap a new project using SDD
2. Create consistent artifacts during the workflow phases
3. Standardize git commits with agent attribution

Currently only `AGENTS.md` exists. We need a complete template library.

## Success Criteria

- [ ] All workflow phases have corresponding templates
- [ ] Templates include clear instructions and placeholders
- [ ] Git commit template includes agent co-contributor epilogue
- [ ] Templates are self-documenting (explain when/how to use)

---

## Implementation Steps

### Step 1: Create ADR Template

**File:** `.sdd/templates/adr-template.md`

```markdown
---
title: "ADR-NNN: [Title]"
status: "Proposed | Accepted | Deprecated | Superseded"
date: "YYYY-MM-DD"
authors: "[Names]"
tags: ["tag1", "tag2"]
supersedes: ""
superseded_by: ""
---

# ADR-NNN: [Title]

## Status

[Proposed | Accepted | Deprecated | Superseded]

## Context

[What is the issue that we're seeing that is motivating this decision or change?]

## Decision

[What is the change that we're proposing and/or doing?]

## Consequences

### Positive

- [Benefit 1]
- [Benefit 2]

### Negative

- [Drawback 1]
- [Drawback 2]

### Neutral

- [Side effect that is neither positive nor negative]

## Alternatives Considered

### [Alternative 1]

[Description and why it was rejected]

### [Alternative 2]

[Description and why it was rejected]

## References

- [Link to relevant documentation]
- [Link to related ADRs]
```

**Verification:** File exists and follows ADR standard format.

---

### Step 2: Create Git Commit Template

**File:** `.sdd/templates/commit-message.txt`

```
<type>(<scope>): <subject>

<body>

<footer>

---
🤖 Co-authored-by: AI Assistant
   Agent: <agent-name>
   Session: <session-id or date>
   Phase: <research|plan|implement|verify>
```

Include a companion documentation section at the top (as comments):

```
# Commit Message Template
#
# Types: feat, fix, docs, style, refactor, perf, test, build, ci, chore
# Scope: component or area affected (optional)
# Subject: imperative mood, no period, <50 chars
#
# Body: Explain WHAT and WHY (not HOW). Wrap at 72 chars.
#
# Footer: Reference issues, breaking changes
#   - Fixes #123
#   - BREAKING CHANGE: description
#
# Agent Attribution (when AI-assisted):
#   Keep the co-author block to track AI contributions.
#   Remove if commit is purely human-authored.
#
# Examples:
#   feat(api): add story execution endpoint
#   fix(cli): handle missing config file gracefully
#   docs(adr): add ADR-005 for database strategy

<type>(<scope>): <subject>

<body>

<footer>

---
🤖 Co-authored-by: AI Assistant
   Agent: <agent-name>
   Session: <session-id or date>
   Phase: <research|plan|implement|verify>
```

**Verification:** File exists, can be used with `git config commit.template`.

---

### Step 3: Create Backlog Item Template

**File:** `.sdd/templates/backlog-item.md`

```markdown
# [Capability Name]

> One-line description of the capability.

## Why

[Business value / user problem being solved]

## What

[Functional requirements - what it must do]

### Must Have

- [ ] Requirement 1
- [ ] Requirement 2

### Should Have

- [ ] Requirement 3

### Could Have

- [ ] Requirement 4

## Open Questions

- [ ] Question that needs answering during research
- [ ] Technical decision that needs investigation

## Acceptance Criteria

- [ ] Criterion 1: [How to verify]
- [ ] Criterion 2: [How to verify]

## Notes

[Any additional context, links, or references]
```

**Verification:** File exists with clear structure.

---

### Step 4: Create Research Template

**File:** `.sdd/templates/research.md`

```markdown
# Research: [Topic]

**Backlog Item:** [path to backlog item]
**Researched:** YYYY-MM-DD
**Status:** Complete | In Progress

## Summary

[2-3 sentence summary of findings]

## Open Questions Answered

### Q1: [Question from backlog]

**Answer:** [What we learned]

**Evidence:**
- [Source 1 with link]
- [Source 2 with link]

**Confidence:** High | Medium | Low

### Q2: [Question from backlog]

...

## Technical Approach

### Recommended Approach

[Describe the approach]

### Technology Choices

| Decision | Choice | Rationale | Alternatives Considered |
|----------|--------|-----------|------------------------|
| [Area] | [Choice] | [Why] | [What else was considered] |

### Architecture Impact

[How this fits into existing architecture]

## Risks and Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| [Risk] | High/Med/Low | High/Med/Low | [How to address] |

## Open Items

- [ ] Items that still need investigation
- [ ] Dependencies on other work

## References

- [Links to documentation, examples, prior art]
```

**Verification:** File exists with complete structure.

---

### Step 5: Create Plan Template

**File:** `.sdd/templates/plan.md`

```markdown
# Plan: [Feature Name]

**Research:** [path to research document]
**Created:** YYYY-MM-DD
**Status:** Ready | In Progress | Complete

## Problem Statement

[What we're solving, copied/refined from backlog]

## Solution Overview

[High-level approach, 2-3 paragraphs]

## Success Criteria

- [ ] Criterion 1
- [ ] Criterion 2
- [ ] All tests pass
- [ ] No new linting errors

---

## Implementation Steps

### Step 1: [Description]

**Files:**
- Create: `path/to/new/file.cs`
- Modify: `path/to/existing/file.cs`

**Details:**
[Specific instructions for this step]

**Verification:**
- [ ] How to verify this step is complete

---

### Step 2: [Description]

...

---

## Test Plan

| Test Type | What to Test | Expected Result |
|-----------|--------------|-----------------|
| Unit | [Component] | [Expectation] |
| Integration | [Flow] | [Expectation] |

## Rollback Plan

[How to undo these changes if needed]

## Notes

[Any additional context for the implementer]
```

**Verification:** File exists with step structure.

---

### Step 6: Create Changes Template

**File:** `.sdd/templates/changes.md`

```markdown
# Changes: [Feature Name]

**Plan:** [path to plan document]
**Started:** YYYY-MM-DD
**Completed:** YYYY-MM-DD (or "In Progress")
**Status:** 🟡 In Progress | ✅ Complete | 🔴 Blocked

## Progress

- [x] Step 1: [Description] ✅
- [x] Step 2: [Description] ✅
- [ ] Step 3: [Description] ⏳ In Progress
- [ ] Step 4: [Description]

## Changes Made

### Step 1: [Description]

**Files Changed:**
- Created: `path/to/file.cs` - [Brief description]
- Modified: `path/to/other.cs` - [What changed]

**Verification:** ✅ [How verified]

### Step 2: [Description]

...

## Deviations from Plan

[Any changes made that differ from the original plan, and why]

## Test Results

```
[Output from test run]
```

## Issues Encountered

| Issue | Resolution |
|-------|------------|
| [Problem] | [How it was solved] |

## Commits

- `abc1234` - feat(scope): description
- `def5678` - fix(scope): description
```

**Verification:** File exists with progress tracking structure.

---

### Step 7: Create Review Template

**File:** `.sdd/templates/review.md`

```markdown
# Review: [Feature Name]

**Changes:** [path to changes document]
**Reviewed:** YYYY-MM-DD
**Reviewer:** [Human | Agent name]
**Status:** ✅ Approved | 🟡 Changes Requested | 🔴 Rejected

## Summary

[2-3 sentence summary of the review outcome]

## Checklist

### Functional Requirements

- [x] Requirement 1 from backlog: [How verified]
- [x] Requirement 2 from backlog: [How verified]

### Code Quality

- [ ] Code follows project coding guidelines
- [ ] No new linting errors or warnings
- [ ] Appropriate error handling
- [ ] No hardcoded secrets or credentials

### Testing

- [ ] All existing tests pass
- [ ] New tests added for new functionality
- [ ] Edge cases covered
- [ ] Test coverage acceptable

### Documentation

- [ ] Code comments where needed
- [ ] README updated if applicable
- [ ] ADR created if architectural decision made

### Architecture

- [ ] Follows established patterns
- [ ] No unnecessary dependencies added
- [ ] Respects layer boundaries

## Findings

### 🔴 Must Fix

1. [Critical issue that blocks approval]

### 🟡 Should Fix

1. [Issue that should be addressed but doesn't block]

### 💡 Suggestions

1. [Optional improvements for consideration]

## Test Verification

```
[Test output or summary]
```

## Decision

[Approved / Changes Requested / Rejected]

[Rationale for decision]
```

**Verification:** File exists with checklist structure.

---

### Step 8: Create Handoff Template

**File:** `.sdd/templates/handoff.md`

```markdown
# Handoff: [Context]

**From:** [Previous agent/human]
**To:** [Next agent/human]
**Date:** YYYY-MM-DD
**Session:** [Session ID or description]

## Current State

[Where things are right now]

### What's Done

- [x] Completed item 1
- [x] Completed item 2

### What's In Progress

- [ ] Item being worked on
  - Current status: [details]
  - Blocker (if any): [details]

### What's Next

- [ ] Next item to tackle
- [ ] Following item

## Context Needed

### Key Files

| File | Relevance |
|------|-----------|
| `path/to/file` | [Why it matters] |

### Key Decisions Made

- [Decision 1]: [Rationale]
- [Decision 2]: [Rationale]

### Open Questions

- [ ] Question that needs answering
- [ ] Decision that needs making

## Environment State

```
Branch: feature/xyz
Last commit: abc1234 - description
Uncommitted changes: Yes/No
Tests passing: Yes/No
```

## Recommended Next Steps

1. [Specific action to take first]
2. [Follow-up action]

## Warnings

- ⚠️ [Anything the next person should be careful about]
```

**Verification:** File exists with handoff structure.

---

### Step 9: Create VISION Template

**File:** `.sdd/templates/VISION.md`

```markdown
# [Project Name] Vision

> [One-line description of the project]

## Purpose

[What problem does this solve? Who is it for?]

## Success Criteria

When this project succeeds:
- [Outcome 1]
- [Outcome 2]
- [Outcome 3]

## Core Capabilities

### 1. [Capability Name]

**Input:** [What it receives]
**Output:** [What it produces]

[Description of the capability]

### 2. [Capability Name]

...

## Non-Goals

What this project explicitly does NOT do:
- [Non-goal 1]
- [Non-goal 2]

## Constraints

- [Technical constraint]
- [Business constraint]
- [Timeline constraint]

## Open Questions

- [ ] Question that needs answering before/during development

## References

- [Link to related documentation]
- [Link to inspiration/prior art]
```

**Verification:** File exists with vision structure.

---

### Step 10: Create STATUS Template

**File:** `.sdd/templates/STATUS.md`

```markdown
# [Project Name] Status

> Last updated: YYYY-MM-DD

## Current Phase

**Phase:** [Planning | Active Development | Maintenance | Deprecated]
**Health:** 🟢 On Track | 🟡 At Risk | 🔴 Blocked

## Quick Stats

| Metric | Value |
|--------|-------|
| Backlog items | X |
| In progress | X |
| Completed | X |
| Open blockers | X |

## Recently Completed

| Date | Item | Summary |
|------|------|---------|
| YYYY-MM-DD | [Item] | [One-line summary] |

## In Progress

| Item | Phase | Owner | Next Step |
|------|-------|-------|-----------|
| [Item] | [research/plan/implement/verify] | [Who] | [Action] |

## Blockers

| Blocker | Impact | Owner | ETA |
|---------|--------|-------|-----|
| [Issue] | [What's blocked] | [Who's resolving] | [When] |

## Upcoming Priorities

1. [Next priority item]
2. [Following item]
3. [Third item]

## Notes

[Any context about current state]
```

**Verification:** File exists with status tracking structure.

---

### Step 11: Update AGENTS.md Template

**File:** `.sdd/templates/AGENTS.md`

Update the existing template to reference `.project/` paths and include guidance on using templates:

Add section:

```markdown
## Using Templates

Templates for all SDD artifacts are in `.sdd/templates/`:

| Template | Copy To | When |
|----------|---------|------|
| `VISION.md` | `.project/VISION.md` | Project bootstrap |
| `STATUS.md` | `.project/STATUS.md` | Project bootstrap |
| `adr-template.md` | `.project/adr/ADR-NNN-title.md` | New architecture decision |
| `backlog-item.md` | `.project/backlog/item-name.md` | New work item |
| `research.md` | `.project/research/research-{item}-{date}.md` | Research phase |
| `plan.md` | `.project/plans/plan-{item}-{date}.md` | Plan phase |
| `changes.md` | `.project/changes/changes-{item}-{date}.md` | Implement phase |
| `review.md` | `.project/reviews/review-{item}-{date}.md` | Verify phase |
| `handoff.md` | `.project/handoffs/handoff-{context}-{date}.md` | Session handoff |
```

---

### Step 12: Update `.sdd/README.md`

Add section about templates:

```markdown
## Templates

All scaffolding templates live in `.sdd/templates/`. Copy them to create new artifacts:

```bash
# Create a new ADR
cp .sdd/templates/adr-template.md .project/adr/ADR-018-my-decision.md

# Create a backlog item  
cp .sdd/templates/backlog-item.md .project/backlog/my-feature.md

# Set up git commit template
git config commit.template .sdd/templates/commit-message.txt
```

### Available Templates

| Template | Purpose | Destination |
|----------|---------|-------------|
| `VISION.md` | Project vision | `.project/VISION.md` |
| `STATUS.md` | Project status | `.project/STATUS.md` |
| `AGENTS.md` | AI entry point | `AGENTS.md` (root) |
| `adr-template.md` | Architecture decisions | `.project/adr/` |
| `backlog-item.md` | Work items | `.project/backlog/` |
| `research.md` | Research artifacts | `.project/research/` |
| `plan.md` | Implementation plans | `.project/plans/` |
| `changes.md` | Change tracking | `.project/changes/` |
| `review.md` | Review results | `.project/reviews/` |
| `handoff.md` | Session handoffs | `.project/handoffs/` |
| `commit-message.txt` | Git commits | Git config |
```

---

## Files Created

| File | Purpose |
|------|---------|
| `.sdd/templates/adr-template.md` | ADR scaffolding |
| `.sdd/templates/commit-message.txt` | Git commit with agent attribution |
| `.sdd/templates/backlog-item.md` | Backlog item scaffolding |
| `.sdd/templates/research.md` | Research phase template |
| `.sdd/templates/plan.md` | Plan phase template |
| `.sdd/templates/changes.md` | Changes tracking template |
| `.sdd/templates/review.md` | Review phase template |
| `.sdd/templates/handoff.md` | Session handoff template |
| `.sdd/templates/VISION.md` | Project vision template |
| `.sdd/templates/STATUS.md` | Project status template |

## Files Modified

| File | Changes |
|------|---------|
| `.sdd/templates/AGENTS.md` | Add templates usage section |
| `.sdd/README.md` | Add templates documentation |

---

## Notes for Implementation

1. **Preserve AGENTS.md** - It already exists, just add to it
2. **Keep templates minimal** - Include only essential structure, not project-specific content
3. **Use placeholders consistently** - `[brackets]` for required, `YYYY-MM-DD` for dates
4. **Include usage comments** - Help users understand when/how to use each template
