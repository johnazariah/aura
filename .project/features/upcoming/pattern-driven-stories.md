# Feature: Pattern-Driven Stories

**Status:** 📋 Spec
**Priority:** High

## Summary

Evolve patterns from documentation-only playbooks into executable plans that can generate fully-analyzed stories with discrete, user-modifiable items. Large-scale operations execute in isolated worktrees for atomicity and reversibility.

## Problem

Current patterns are passive documentation:
- LLM reads the pattern and executes steps
- User has no visibility into what will change until it happens
- No way to add domain-specific steps (e.g., database migrations)
- If execution fails mid-way, codebase may be in inconsistent state
- No record of what was planned vs. executed

## Solution

### Tiered Ceremony Based on Scope

Different patterns have different risk profiles. The threshold for ceremony scales accordingly:

| Pattern Type | Small | Medium | Large |
|--------------|-------|--------|-------|
| **Refactoring** | < 5 files | 5-15 files | 15+ files |
| **Test Generation** | 1-2 test files | 3-5 test files | 5+ OR integration tests |
| **Code Generation** | Single file | Multiple files | Cross-module |

| Scope | Ceremony |
|-------|----------|
| **Small** | Execute directly, single commit |
| **Medium** | Full analysis → user confirms → execute → logical commits |
| **Large** | Full analysis → create story in worktree → user-modifiable plan → step-by-step execution → squash merge |

### The Analysis IS The Spec

When a pattern is invoked:
1. Agent performs full blast radius analysis
2. Analysis generates discrete, typed operations
3. User reviews the complete list of changes
4. User can add, remove, disable, or reorder items
5. Only then does execution begin

### Full-Resolution Visibility

Show every discrete item, not summaries. User sees exactly what will happen, can spot omissions, remove false positives.

**Example: Comprehensive Rename**

```
Phase 1: Core Types                               [+ Add Item]

☑ Workflow → Story                    (14 refs, 3 files)
☑ IWorkflow → IStory                  (8 refs, 2 files)
☑ WorkflowStatus → StoryStatus        (6 refs, 2 files)
☐ WorkflowState → StoryState          (user-added)

Phase 2: Services                                 [+ Add Item]

☑ WorkflowService → StoryService      (22 refs, 4 files)
☑ IWorkflowService → IStoryService    (12 refs, 3 files)
```

**Example: Test Generation**

```
Phase 1: Unit Tests                               [+ Add Item]

☑ OrderService                        (12 public methods)
☑ OrderRepository                     (6 public methods)
☑ OrderValidator                      (4 public methods)
☐ OrderMapper                         (trivial - user disabled)

Phase 2: Integration Tests                        [+ Add Item]

☑ OrderRepository                     (DB access)
☐ External API client tests           (user-added)
```

### User-Added Steps

Patterns provide a scaffold, but the user knows things the agent doesn't:
- Database tables that need migration
- Downstream services that call the API
- Wiki pages documenting the concept
- Terraform modules using the name

User can add manual steps that pause execution for confirmation.

### Worktree Isolation for Large Changes

Story Mode executes in an isolated git worktree:
- Main branch untouched until success
- Fine-grained commits per step (preserved in branch history)
- Squash merge for clean main history
- Rollback = `git worktree remove` — gone without trace

## Data Model

### StoryItem

Each item is a discrete, typed operation:

```typescript
interface StoryItem {
  id: string;
  phase: string;
  type: StoryItemType;
  source: 'analysis' | 'user';
  enabled: boolean;
  
  // Type-specific properties (see below)
  properties: Record<string, unknown>;
  
  // Execution state
  status: 'pending' | 'in-progress' | 'completed' | 'failed' | 'skipped';
  error?: string;
  commitHash?: string;  // If committed separately
}

// Extensible type system - each pattern can define its own types
type StoryItemType = 
  // Refactoring types
  | 'symbol-rename' 
  | 'file-rename' 
  | 'route-change'
  // Test generation types  
  | 'generate-unit-test'
  | 'generate-integration-test'
  // Common types
  | 'manual' 
  | 'verification';

// Example properties by type:
// symbol-rename: { symbolName, newName, referenceCount, fileCount, files }
// file-rename: { oldPath, newPath }
// generate-unit-test: { targetClass, publicMethodCount, testFramework, outputPath }
// generate-integration-test: { targetClass, dependencies, outputPath }
// manual: { description }
```

### PatternExecution

Captures the full execution context:

```typescript
interface PatternExecution {
  id: string;
  patternName: string;
  storyId?: string;  // If escalated to story mode
  worktreePath?: string;
  
  parameters: Record<string, string>;  // e.g., { oldName: "Workflow", newName: "Story" }
  
  phases: Phase[];
  
  status: 'analyzing' | 'pending-approval' | 'executing' | 'completed' | 'failed' | 'cancelled';
  startedAt?: Date;
  completedAt?: Date;
}

interface Phase {
  name: string;
  items: StoryItem[];
}
```

## UX Flow

### Example 1: Comprehensive Rename

```
User: "Rename Workflow to Story across the codebase"
                    ↓
Agent: Invokes comprehensive-rename pattern
                    ↓
Agent: Performs blast radius analysis
                    ↓
Agent: Presents full item list with ref counts
       "I found 64 references across 12 files. 
        Here's the complete plan:"
       [Shows phases with all items]
       "Would you like to add any steps or proceed?"
                    ↓
User: "Add a step for database migration"
                    ↓
Agent: Adds manual step to Phase 5
       "Added. This is a large change (12+ files).
        Recommend executing in Story Mode for atomicity.
        [Execute Inline] [Create Story]"
                    ↓
User: Clicks [Create Story]
                    ↓
Agent: Creates worktree, story with all items
       Opens story view for step-by-step execution
                    ↓
User: Reviews, modifies, then starts execution
                    ↓
Agent: Executes each step, commits after each
       Pauses at manual steps for user action
                    ↓
On success: Squash merge to main, delete worktree
On failure: Worktree preserved for diagnosis
```

### Example 2: Test Generation

```
User: "Generate tests for the Order module"
                    ↓
Agent: Invokes generate-tests pattern
                    ↓
Agent: Analyzes module, discovers testable classes
                    ↓
Agent: Presents plan:
       "I found 4 classes with 28 public methods.
        Here's the test generation plan:"
       
       Phase 1: Unit Tests
       ☑ OrderService          (12 methods → 12 tests)
       ☑ OrderRepository       (6 methods → 6 tests)
       ☑ OrderValidator        (4 methods → 8 tests)
       ☐ OrderMapper           (trivial getters)
       
       Phase 2: Integration Tests
       ☑ OrderRepository       (DB layer)
       
       "Would you like to modify the plan?"
                    ↓
User: "Skip OrderMapper, add integration test for external API"
                    ↓
Agent: Updates plan
       "This will create 5 test files.
        [Execute Inline] [Create Story]"
                    ↓
User: Chooses [Execute Inline] (lower risk, fewer files)
                    ↓
Agent: Generates each test file
       Shows preview, user approves each
       Commits with logical grouping
                    ↓
Done: 5 test files created, build passes
```

## Trust Contract

| Black Box AI | Aura with Pattern-Driven Stories |
|--------------|----------------------------------|
| "I'll do it for you" | "Here's everything I found — did I miss any?" |
| "Trust me, I've got this" | "Here's my plan — does this look right?" |
| "Done!" | "Step 3 of 8 complete — ready for next?" |
| "Oops, something broke" | "Step 4 failed — worktree intact, main untouched" |

Principles:
1. **Transparency** — Show the full analysis, hide nothing
2. **Control** — User can add, remove, reorder, disable items
3. **Consent** — Nothing executes without explicit approval
4. **Reversibility** — Worktree isolation means "undo" is always available
5. **Accountability** — Complete record of what was planned vs. executed

## The PR as Proof of Work

A key benefit of the Story model (vs. just following a recipe) is the PR artifact at completion.

### Recipe vs. Story

| Aspect | Recipe (just steps) | Story (first-class artifact) |
|--------|---------------------|------------------------------|
| **Execution** | Follow steps, done | Follow steps, create PR |
| **Review** | None — reviewer sees one big diff | Reviewer sees itemized plan + commits |
| **Approval** | Trust the executor | PR review process |
| **Audit trail** | Git log only | Story record + PR + commits |
| **Collaboration** | Single operator | Multiple reviewers can comment |
| **Atomicity** | Hope nothing broke | Merge or reject entire PR |

### What the PR Contains

1. **Description** — The full plan with all items (phases, what changed, ref counts)
2. **Commits** — One per logical step (preserves fine-grained history)
3. **Diff** — The total change, reviewable as a unit
4. **Status** — Build passed, tests passed

### Review Benefits

A reviewer can:
- See exactly what was planned before any code changed
- Verify each step's commit in isolation
- Approve/reject the whole thing atomically
- Request changes ("you missed the database migration")

For large refactors requiring approval (tech lead review, team awareness, compliance), the PR is the natural artifact. Without the Story model, you'd have a big commit with no structure, no visibility into the plan, and no way to discuss before merge.

## Implementation Phases

### Phase 1: Analysis-to-Items

- Define common `StoryItem` structure usable by all patterns
- Extend `aura_refactor` to return structured items for renames
- Extend `aura_inspect` to return testable classes/methods for test generation
- Each item has type, target, metrics (refs, methods, etc.), affected files
- No execution changes yet — just richer analysis output

### Phase 2: Pattern Execution Context

- Track pattern execution state
- Present items for user review/modification
- Execute with user consent
- Generate logical commits

### Phase 3: Story Mode Integration

- Escalate large executions to Story
- Create worktree automatically
- Populate story with items from analysis
- UI for reviewing/modifying items

### Phase 4: Step-by-Step Execution

- Execute one item at a time
- Commit after each step
- Pause at manual steps
- Handle failures gracefully

### Phase 5: Completion & Merge

- Squash merge on success
- Preserve branch history for audit
- Clean up worktree

## Open Questions

1. **Threshold for story mode**: Always offer? Or auto-escalate at some threshold?

2. **Phase grouping**: Should patterns define phase categories, or derive from symbol types?

3. **Step library**: Common additions like "Database migration" — should these be templated?

4. **Execution resumption**: If user closes VS Code mid-execution, how to resume?

5. **Partial rollback**: Can user undo specific steps, or only full rollback?

## Related

- [Operational Patterns](../completed/operational-patterns.md) — original patterns implementation
- [ADR-007](../../adr/adr-007-operational-patterns.md) — patterns architecture decision
- `patterns/comprehensive-rename.md` — refactoring pattern, will be updated
- `patterns/generate-tests.md` — test generation pattern, will be updated
