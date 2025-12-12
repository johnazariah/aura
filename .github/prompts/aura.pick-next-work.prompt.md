```prompt
---
description: Analyze upcoming features and open items to suggest the next piece of work
---
# Pick Next Work

You are helping the user decide what to work on next. Analyze available work items and make a prioritized recommendation.

**IMPORTANT**: This prompt is for DISCUSSION and PLANNING only. Do NOT start implementing anything until the user explicitly says to proceed (e.g., "let's do #1", "start on X", "go ahead").

## Step 1: Gather Context

Read the following to understand current state:

1. **STATUS.md** - Check:
   - "Open Items" section for technical debt and not-yet-implemented items
   - "Pending Actions" section for outstanding tasks
   - "Recent Changes" to understand current momentum

2. **Upcoming Features** - List all files in `.project/features/upcoming/`
   - Read a brief summary of each (first 30 lines)
   - Note which have detailed specs vs. just ideas

3. **Check for tagged TODOs** in source code:
   ```powershell
   git grep -n "TODO\|FIXME\|HACK\|XXX" -- "*.cs" "*.ts" | Select-Object -First 20
   ```

## Step 2: Categorize Work Items

Group findings into:

| Category | Priority | Examples |
|----------|----------|----------|
| **Blocking** | 🔴 High | Broken tests, build failures, user-reported bugs |
| **Momentum** | 🟡 Medium | Continue partially-done features, quick wins |
| **Strategic** | 🟢 Normal | New features aligned with roadmap |
| **Debt** | ⚪ Low | Refactoring, code cleanup, documentation |

## Step 3: Evaluate Upcoming Features

For each feature in `features/upcoming/`, assess:

1. **Readiness** - Does it have a clear spec or just an idea?
2. **Dependencies** - Does it require other work first?
3. **Effort** - Small (hours), Medium (day), Large (days)?
4. **Impact** - How much does it improve Aura?

## Step 4: Make Recommendation

Suggest the **top 3** work items with reasoning:

```markdown
## Recommended Next Work

### 1. [Feature/Item Name]
- **Why**: Brief justification
- **Effort**: Small/Medium/Large
- **First Step**: Concrete action to start

### 2. [Alternative Option]
- **Why**: ...

### 3. [Another Option]
- **Why**: ...

## Also Consider
- Quick wins that can be done in parallel
- Debt items worth addressing opportunistically
```

## Step 5: Check Prerequisites

Before starting the recommended work:

1. Is there a spec file? If not, create one first in `features/upcoming/`
2. Are there dependencies to address?
3. Is the scope well-defined?

## Output Format

Present findings as:

1. **Current State Summary** - One paragraph on project status
2. **Work Items Found** - Categorized list
3. **Recommendation** - Top 3 prioritized items with reasoning
4. **Ready to Start?** - Confirmation or prerequisites needed

## STOP HERE

After presenting your recommendation, **STOP and wait for user input**. 

Do NOT:
- Create files
- Write code
- Start implementation
- Run commands (except for gathering context in Steps 1-3)

DO:
- Present your analysis clearly
- Answer questions about the options
- Discuss trade-offs if asked
- Refine recommendations based on feedback

Only proceed with implementation when the user explicitly instructs you to start (e.g., "let's do #1", "start working on X", "go ahead with the recommendation").
```
