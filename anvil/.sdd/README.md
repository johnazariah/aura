# SDD Framework

**Spec-Driven Development** - A structured workflow for building software with AI assistance.

## Bootstrap a New Project

### Quick Start (Script)

```powershell
# 1. Copy .sdd/ and .github/agents/ to your new project
cp -r /path/to/sdd-reference/.sdd ./
cp -r /path/to/sdd-reference/.github ./

# 2. Run the bootstrap script
.sdd/bootstrap.ps1 -ProjectName "MyProject"

# 3. Edit your vision and start building
code .project/VISION.md
```

Or on macOS/Linux:

```bash
.sdd/bootstrap.sh "MyProject"
```

### What Bootstrap Creates

```
your-project/
├── AGENTS.md                      # AI entry point (from template)
├── .project/
│   ├── VISION.md                  # Your project vision (fill this in!)
│   ├── STATUS.md                  # Project status tracker
│   ├── README.md                  # Project docs index
│   ├── adr/                       # Architecture Decision Records
│   ├── architecture/              # System architecture docs
│   ├── coding-guidelines/         # Language-specific standards
│   ├── backlog/                   # Work items
│   ├── research/                  # Research phase artifacts
│   ├── plans/                     # Implementation plans
│   ├── changes/                   # Change tracking
│   ├── reviews/                   # Review results
│   ├── handoffs/                  # Session handoffs
│   └── completed/                 # Archived work
└── .sdd/                          # The framework (you copied this)
```

### After Bootstrap

```
# Define your vision interactively
@backlog-builder
> "I want to build a CLI tool for..."

# Or manually edit .project/VISION.md, then:
@next-backlog-item          # What should I work on?
```

---

## Daily Workflow

```
@next-backlog-item          # What should I work on?
@backlog-builder            # Build vision and backlog
@research <backlog-item>    # Research an item
@plan <research-doc>        # Create implementation plan
@implement <plan-doc>       # Execute the plan
@verify <changes-doc>       # Review implementation
@docs                       # Update documentation
@release                    # Cut a release
```

## Philosophy

SDD separates **thinking** from **doing**:

1. **Vision** → What are we building and why?
2. **Backlog** → What capabilities do we need? (tech-agnostic)
3. **Research** → What technologies and approaches will we use?
4. **Plan** → What are the exact steps to implement?
5. **Implement** → Execute the plan mechanically
6. **Verify** → Did we meet the requirements?

Each phase produces artifacts that feed the next phase.

## Folder Structure

```
project/
├── VISION.md                      # Product vision and purpose
├── .project/
│   ├── backlog/                   # What to build (tech-agnostic)
│   │   └── {item-name}.md
│   ├── research/                  # How to build it
│   │   └── research-{item}-{date}.md
│   ├── plans/                     # Step-by-step instructions
│   │   └── plan-{item}-{date}.md
│   ├── changes/                   # What was built
│   │   └── changes-{item}-{date}.md
│   ├── reviews/                   # Verification results
│   │   └── review-{item}-{date}.md
│   └── completed/                 # Archived finished work
│       ├── backlog/
│       ├── research/
│       ├── plans/
│       ├── changes/
│       └── reviews/
└── .sdd/
    └── prompts/                   # One-shot operations
```

## Agents vs Prompts

| Type | Invocation | Nature |
|------|------------|--------|
| **Agent** | `@name` | Conversational, multi-turn, asks questions |
| **Prompt** | `/name` | One-shot, clear input → output |

### Agents (in `.github/agents/`)

| Agent | Purpose |
|-------|---------|
| `@backlog-builder` | Interactively build vision and backlog items |
| `@next-backlog-item` | Scan backlog, recommend next item |
| `@research` | Research a backlog item, answer open questions |
| `@plan` | Create step-by-step implementation plan |
| `@implement` | Execute plan, track progress |
| `@verify` | Review implementation against requirements |
| `@docs` | Audit and update documentation |
| `@release` | Guide through release ceremony |

### Prompts (in `.sdd/prompts/`)

| Prompt | Purpose |
|--------|---------|
| `/setup` | Bootstrap devcontainer and environment |
| `/commit` | Create logical commits from staged changes |
| `/complete` | Archive completed backlog item |
| `/merge` | Merge branch with quality gates |
| `/context` | Refresh understanding of project state |

## Workflow Patterns

### Starting a New Project

```
@backlog-builder
> "I want to build a CLI tool for..."
```

This creates:
- `VISION.md` - Product purpose and capabilities
- `.project/backlog/*.md` - Initial backlog items

### Working on an Item

```
@next-backlog-item              # Pick an item
@research .project/backlog/my-item.md
@plan .project/research/research-my-item-2026-01-31.md
@implement .project/plans/plan-my-item-2026-01-31.md
@verify .project/changes/changes-my-item-2026-01-31.md
/complete my-item               # Archive when approved
```

### Parallel Workflows

You can work on multiple items by keeping phases in separate chat windows:

- **Planning window**: `@backlog-builder`, `@next-backlog-item`
- **Research window**: `@research`
- **Development window**: `@plan`, `@implement`, `@verify`

### Releasing

```
@release
```

This guides you through:
1. Analyzing changes since last release
2. Recommending version bump
3. Updating CHANGELOG
4. Running quality gates
5. Creating and pushing tag

## Artifact Formats

### Backlog Item

```markdown
# Backlog: {Capability Name}

**Capability:** {Which vision capability this addresses}
**Priority:** High | Medium | Low
**Depends On:** {Other backlog items, if any}

## Functional Requirements

{What the system must do - tech agnostic}

## Open Questions (for Research)

{Questions that need answers before planning}
```

### Research Document

```markdown
# Research: {Item Name}

**Backlog Item:** {path}
**Researched:** {date}

## Open Questions Answered

### Q1: {question}
**Answer:** {what we learned}
**Sources:** {where we found it}

## Technical Approach

{Recommended approach with rationale}

## Risks

{What could go wrong and how to mitigate}
```

### Implementation Plan

```markdown
# Implementation Plan: {Item Name}

**Research:** {path}
**Planned:** {date}

## Steps

### Step 1: {Action}
**Files:** {what to create/modify}
**Verification:** {how to know it's done}

### Step 2: ...
```

### Changes Document

```markdown
# Changes: {Item Name}

**Plan:** {path}
**Status:** In Progress | Complete

## Progress

- [x] Step 1: ... ✅
- [ ] Step 2: ... ⏳

## Changes Made

{What was actually done}
```

### Review Document

```markdown
# Review: {Item Name}

**Changes:** {path}
**Verdict:** ✅ Approved | ⚠️ Needs Work | ❌ Rejected

## Requirements Verification

{Each requirement and whether it was met}

## Issues Found

{Any problems to address}
```

## Best Practices

1. **Don't skip phases** - Each phase catches different problems
2. **Keep backlog tech-agnostic** - Technology choices come in research
3. **Make plans atomic** - Each step should be completable in 15-60 min
4. **Verify before completing** - Don't archive items with "Needs Work"
5. **Archive completed work** - Keep the trail but clear the active folders

## Customization

The SDD framework is designed to be customized per project:

- Add project-specific prompts in `.sdd/prompts/`
- Extend agents in `.github/agents/`
- Adjust folder structure in `.project/`
- Update `copilot-instructions.md` with project-specific context

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

---

Brought to you by **anvil** 🔨
