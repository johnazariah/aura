# Self-Bootstrapping: Aura Develops Aura

## The Vision

The ultimate test of an AI coding assistant: **Can it develop itself?**

If Aura can successfully contribute to its own codebase, we have proof that it's genuinely useful for real development work.

## Why This Matters

1. **Dogfooding at the deepest level** - We use what we build
2. **Rapid iteration** - Aura helps implement Aura features faster
3. **Quality validation** - If it can handle *this* codebase, it can handle others
4. **Trust building** - Watching it work on familiar code builds confidence

## Prerequisites (What We Need First)

### Must Have
- [x] Working agent execution (Phase 2 ✅)
- [ ] Persistent conversation history (Phase 3 - Data Layer)
- [ ] File read/write tools for agents
- [ ] Git integration (commit, branch, diff)
- [ ] Test execution feedback

### Nice to Have
- [ ] RAG over the codebase (understand context)
- [ ] Multi-file editing
- [ ] Build/test feedback loop

## The Bootstrap Ladder

### Level 1: Documentation Agent
**Task**: Write XML documentation for undocumented methods

- Low risk (doesn't change behavior)
- Easy to verify (build still passes)
- Immediate value (better IntelliSense)

**Test**: Point at `OllamaProvider.cs`, ask for XML docs, verify they're accurate.

### Level 2: Test Author Agent  
**Task**: Write unit tests for existing code

- Medium risk (tests don't affect production)
- Verifiable (tests should pass)
- High value (increases coverage)

**Test**: Give it `AgentRegistry.cs`, ask for edge case tests, run them.

### Level 3: Bug Fix Agent
**Task**: Fix a simple bug with failing test

- Provide: failing test, relevant source file
- Agent: proposes fix
- Human: reviews and applies

**Test**: Create a deliberate bug, write a failing test, let Aura fix it.

### Level 4: Feature Agent
**Task**: Implement a small feature from a spec

- Provide: feature spec, relevant files
- Agent: generates implementation + tests
- Human: reviews, requests changes, merges

**Test**: Write a spec for "add agent tags filtering", let Aura implement.

### Level 5: Full Loop
**Task**: Pick up a GitHub issue and implement it

- Agent reads issue
- Breaks down into tasks
- Implements each task
- Creates PR
- Human reviews and merges

## The First Bootstrap Task

Once Phase 3 (Data Layer) is done, the first self-bootstrap task should be:

### "Add Conversation History Display to Extension"

**Why this task?**
1. Touches API, Foundation, and Extension (full stack)
2. Uses the new database we just added
3. Result is visible in the UI (satisfying to see)
4. Moderate complexity - not trivial, not overwhelming

**Steps Aura would need to do:**
1. Add endpoint: `GET /api/conversations` 
2. Add endpoint: `GET /api/conversations/{id}`
3. Add tree view section in extension
4. Display conversation history when clicked

## Tools Aura Needs

For self-development, Aura needs these tools attached to agents:

```markdown
## Tools
- file_read: Read file contents
- file_write: Write/update file contents  
- terminal: Run commands (build, test)
- git_status: See changed files
- git_diff: See what changed
- git_commit: Commit changes
- test_run: Run specific tests
- search_code: Find relevant code
```

## Safety Rails

### Human-in-the-Loop
- All changes require human approval before commit
- PR-based workflow (Aura creates branch, human merges)
- Review diff before applying

### Sandboxing
- Read-only access to `main` branch
- Writes only to feature branches
- Cannot push directly to main

### Observability
- Log all agent actions
- Show reasoning/planning steps
- Audit trail in database

## Metrics to Track

1. **Task completion rate** - Did it finish the task?
2. **Iteration count** - How many attempts to get it right?
3. **Human intervention rate** - How often did we step in?
4. **Test pass rate** - Do generated tests pass? Does existing suite still pass?
5. **Code quality** - Does it follow CODING-STANDARDS.md?

## Timeline Proposal

| Week | Milestone |
|------|-----------|
| 1 | Phase 3 complete (Data Layer) |
| 2 | Add file_read/file_write tools |
| 3 | Level 1: Documentation agent working |
| 4 | Level 2: Test author agent working |
| 5 | Level 3: First bug fix by Aura |
| 6 | Level 4: First feature by Aura |

## The Dream

One day, we open a GitHub issue:

> "Add support for Anthropic Claude as an LLM provider"

Aura picks it up, reads the existing OllamaProvider, understands the pattern, creates `ClaudeProvider`, writes tests, creates a PR with a clear description.

We review, approve, and merge.

**Aura just extended itself.**

---

## Immediate Next Steps (Tomorrow)

1. **Finish Phase 3** - We need conversation persistence
2. **Add file tools** - Agents need to read/write files
3. **Create bootstrap-agent.md** - A meta-agent for development tasks
4. **First test** - Documentation task on a small file

The goal isn't perfection. It's proof that the system can improve itself, even in small ways.

*Start small. Iterate. Let Aura earn our trust.*
