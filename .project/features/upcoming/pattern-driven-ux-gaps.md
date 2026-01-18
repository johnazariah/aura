# Pattern-Driven Story UX Gaps

**Status:** 🔄 In Progress
**Created:** 2025-01-18
**Priority:** High
**Source:** Dogfooding session - Story 9c43eea5-a229-4869-a60d-d8107fabb542

## Overview

During dogfooding of the pattern-driven story workflow (test coverage story with generate-tests pattern), we identified several UX gaps and MCP tool deficiencies that prevent smooth execution of pattern-driven stories in worktree-based workflows.

### Dogfooding Context
- **Task:** Generate unit tests for `WorkflowService` (25 public methods, 0 existing tests)
- **Pattern:** `generate-tests`
- **Outcome:** 32 tests created, 124 total in `Aura.Module.Developer.Tests`
- **Result:** Significant manual intervention required due to tool deficiencies

### Summary
- **21 gaps identified** (6 fixed, 15 remaining)
- **Key blockers:** Cache staleness, test file overwriting, missing test attributes
- **UX issues:** Story context injection, panel recovery, pattern binding

## Identified Gaps

### Gap 1: No Story Context Injection to Agent (HIGH)

**Problem:** When chatting with Copilot in a worktree, the agent has no knowledge that a story is in progress. The user must manually explain context.

**Expected:** The agent should automatically know:
- Story title and description
- Current step being worked on
- Pattern being followed
- Repository context

**Proposed Solution:** 
- Add story context to `.github/copilot-instructions.md` dynamically
- Or inject via MCP tool that agent can call to get current story context

---

### Gap 2: Workflow Panel Not Pinned (MEDIUM)

**Problem:** The workflow panel closes when switching between editor tabs, requiring manual reopening.

**Expected:** Workflow panel should stay open as a persistent side panel while working on a story.

**Proposed Solution:**
- Use VS Code's pinned editor or webview persistence APIs
- Consider moving to sidebar view instead of editor panel

---

### Gap 3: Pattern ↔ Enrich Not Connected (HIGH)

**Problem:** Loading a pattern via `aura_pattern` and enriching a story via `aura_workflow(operation: "enrich")` are disconnected. User must manually copy pattern steps.

**Expected:** One action to load a pattern and apply it to the current story.

**Proposed Solution:**
- Add `pattern` parameter to `enrich` operation
- `aura_workflow(operation: "enrich", id: "...", pattern: "generate-tests")` should:
  1. Load the pattern
  2. Parse steps from pattern markdown
  3. Add steps to the story

---

### Gap 4: No "Resume Story" Prompt (MEDIUM)

**Problem:** Opening a worktree shows generic welcome screen instead of contextual "Continue your story" experience.

**Expected:** When opening a worktree that has an associated story:
- Show story title prominently
- Show current step / next action
- Provide "Continue" button to resume work

**Proposed Solution:**
- `checkAndOpenWorkflowForWorktree()` already exists but may not be working correctly
- Add prominent notification or status bar item

---

### Gap 5: Steps Not Shown Until Enriched (LOW)

**Problem:** New stories created from issues appear empty in the UI even if a pattern is intended.

**Expected:** Pattern steps should be visible as "planned" steps from the start.

**Proposed Solution:**
- Store pattern name with story
- Show pattern steps as templates until explicitly enriched

---

### Gap 6: No Pattern Binding to Story (MEDIUM)

**Problem:** There's no way to associate a pattern with a story at creation time or later.

**Expected:** Stories should have an optional `pattern` field that:
- Gets set when creating story from a pattern
- Allows resuming pattern execution later
- Shows pattern context in UI

**Proposed Solution:**
- Add `pattern` column to workflow entity
- Update create story API to accept pattern name
- Display pattern badge in workflow panel

---

### Gap 7: No Easy Workflow Panel Recovery (HIGH)

**Problem:** If the workflow panel is closed, there's no obvious way to reopen it. The sidebar tree exists but isn't discoverable.

**Expected:** Multiple easy ways to reopen:
- Status bar click
- Command palette
- Sidebar icon
- Keyboard shortcut

**Proposed Solution:**
- Add status bar item showing current story (clickable to open panel)
- Ensure sidebar view is always visible when story is active
- Add keyboard shortcut for "Show Current Story"

---

### Gap 8: Worktree Shows Wrong Onboarding State (CRITICAL) ✅ IMPLEMENTED

**Status:** Implemented in extension.ts and healthCheckService.ts

**Problem:** When opening a worktree VS Code window, the extension shows:
- "RAG Index: Not indexed"
- "Enable Aura for this Workspace" welcome prompt
- Onboarding prompts despite parent repo being fully indexed

**Root Cause:** `checkOnboardingStatus()` in `extension.ts` queries workspace status using the worktree path. The API returns 404 because only the parent repo is registered as a workspace. Extension interprets 404 as "not onboarded."

**Backend Reality:** The backend correctly handles worktrees - it uses the parent repo's RAG index for code analysis. The worktree doesn't need separate indexing.

**Expected:** Worktree should show:
- "Using index from: c:\work\aura" (parent path)
- No onboarding prompts
- Full functionality

**Proposed Solution (Extension-Side):**

1. Create utility function `getGitRepositoryRoot(path: string)`:
   ```typescript
   async function getGitRepositoryRoot(workspacePath: string): Promise<{
       canonicalPath: string;
       isWorktree: boolean;
       parentPath?: string;
   }> {
       // Check if .git is a file (worktree) or directory (normal repo)
       // If file, parse it to find: "gitdir: /path/to/parent/.git/worktrees/name"
       // Extract parent repo path from that
   }
   ```

2. Modify `checkOnboardingStatus()`:
   - Call `getGitRepositoryRoot()` first
   - Query workspace status using canonical parent path
   - Set additional context: `aura.isWorktree`

3. Update UI:
   - Status bar shows "(worktree)" indicator
   - Welcome view hidden for worktrees with indexed parent
   - Info message: "Using index from parent repository"

---

## Implementation Priority

### Completed
- **Gap 8** - ✅ Worktree onboarding detection
- **Gap 9** - ✅ Test generation now creates more complete tests with realistic parameter values
- **Gap 10** - ✅ Test generation now places files in correct project (matches source project to test project by convention)
- **Gap 11** - ✅ Test generation now reads actual enum values from Roslyn instead of hallucinating
- **Gap 13** - ✅ aura_navigate definition now works for C# (uses Roslyn + code graph)
- **Gap 14** - ✅ aura_validate now supports solution-level validation (omit projectName)
- **Gap 18** - ✅ Roslyn workspace cache now auto-clears after file writes
- **Gap 12** - ✅ aura_inspect type_members now has Roslyn fallback (pass solutionPath)

### Critical - Blocking Pattern Execution
- **Gap 17** - Test generation overwrites instead of appending
- **Gap 19** - aura_generate method missing [Fact] attribute

### High - MCP Tool Reliability
- **Gap 15** - aura_search irrelevant results
- **Gap 16** - aura_search fails to find enums

### Medium - UX Improvements
- **Gap 20** - aura_validate tests truncated output
- **Gap 21** - aura_generate method formatting issues
- **Gap 3** - Pattern + Enrich connection
- **Gap 1** - Story context injection
- **Gap 7** - Panel recovery
- **Gap 4** - Resume story experience
- **Gap 6** - Pattern binding

### Low
- **Gap 2** - Panel pinning
- **Gap 5** - Template steps

---

### Gap 9: Test Generation Only Creates Skeletons (HIGH) ✅ IMPLEMENTED

**Status:** Fixed in RoslynTestGenerator.cs

**Problem:** The `aura_generate(operation: "tests")` tool only generates test file skeletons with empty test methods. It doesn't generate actual test logic with assertions.

**Expected:** Generated tests should include:
- Meaningful test method names based on scenarios
- Arrange/Act/Assert structure
- Actual assertions for the code being tested
- Mock setup where needed

**Proposed Solution:**
- Enhance the test generation prompt to include actual test logic
- Use code context (method signatures, dependencies) to generate meaningful assertions
- Consider generating multiple test cases per method (happy path, edge cases, error cases)

---

## MCP Tool Deficiencies (Customer Feedback)

The following issues were identified during dogfooding the generate-tests pattern:

### Gap 10: Test Generation Places Files in Wrong Project (HIGH) ✅ IMPLEMENTED

**Tool:** `aura_generate(operation: "tests")`

**Status:** Fixed in RoslynTestGenerator.cs - `DetermineTestFilePath` now matches source project to test project by convention.

**Problem:** When generating tests for `Aura.Module.Developer` classes, the tool placed tests in `Aura.Foundation.Tests` instead of `Aura.Module.Developer.Tests`.

**Impact:** Tests had to be manually recreated in the correct location.

**Root Cause:** Tool doesn't understand the project structure or naming conventions for test projects.

**Proposed Solution:**
- Infer test project from source project name (`X` → `X.Tests`)
- Use solution structure to find matching test project
- Allow explicit `testProjectPath` parameter

---

### Gap 11: Test Generation Uses Non-Existent Enum Values (HIGH) ✅ IMPLEMENTED

**Tool:** `aura_generate(operation: "tests")`

**Status:** Fixed in RoslynTestGenerator.cs - `GenerateComplexTypeValue` now reads actual enum members from Roslyn.

**Problem:** Generated tests used `StepStatus.Approved` which doesn't exist in the codebase.

**Impact:** Tests fail to compile.

**Root Cause:** Tool is hallucinating enum values instead of reading actual enum definition.

**Proposed Solution:**
- Before generating tests, inspect the target type and all its dependencies
- Read enum definitions from code graph
- Include enum values in the generation context

---

### Gap 12: aura_inspect Returns Empty for Valid Types (HIGH) ✅ IMPLEMENTED

**Tool:** `aura_inspect(operation: "type_members")`

**Status:** Fixed in McpHandler.cs - Added Roslyn fallback when code graph returns empty.

**Problem:** Returned empty results when inspecting `WorkflowService`.

**Impact:** Couldn't inspect class structure, had to manually read files.

**Root Cause:** Code graph (PostgreSQL) was not indexed or stale. The `type_members` operation only queried the code graph, with no Roslyn fallback.

**Solution Implemented:**
- Added `GetTypeMembersViaRoslynAsync()` method as fallback
- When code graph returns empty AND `solutionPath` is provided, uses Roslyn to find type and enumerate members
- Returns full member signatures including return types and parameters
- Updated schema description to document `solutionPath` enables Roslyn fallback

---

### Gap 13: aura_navigate Definition Fails with Key Error (HIGH) ✅ IMPLEMENTED

**Tool:** `aura_navigate(operation: "definition")`

**Status:** Fixed in McpHandler.cs - Added `FindCSharpDefinitionAsync` with Roslyn and code graph support.

**Problem:** Failed with "key not present" error when trying to find definition.

**Impact:** Had to use `usages` operation as a workaround.

**Root Cause:** Missing dictionary key handling in navigation code.

**Proposed Solution:**
- Add proper error handling for missing keys
- Return informative error message instead of exception
- Check if symbol exists before attempting navigation

---

### Gap 14: aura_validate Fails for Solution-Level Validation (MEDIUM) ✅ IMPLEMENTED

**Tool:** `aura_validate(operation: "compilation")`

**Status:** Fixed in McpHandler.cs - `projectName` is now optional. If omitted, validates all projects in solution.

**Problem:** Failed with "key not present" error when validating the entire solution.

**Impact:** Had to specify `projectName` to get compilation status.

**Workaround:** Provide explicit `projectName` parameter.

**Proposed Solution:**
- Fix solution-level compilation validation
- Handle case where no project specified (validate all)
- Better error messages

---

### Gap 15: aura_search Returns Irrelevant Results (HIGH)

**Tool:** `aura_search`

**Problem:** Searching for `IGitWorktreeService CreateAsync WorktreeResult` returned completely irrelevant results.

**Impact:** Had to use navigation tools as workaround.

**Root Cause:** Semantic search not finding exact symbol matches.

**Proposed Solution:**
- Boost exact symbol name matches
- Consider hybrid search (keyword + semantic)
- Pre-filter by type/interface name

---

### Gap 16: aura_search Fails to Find Enums (HIGH)

**Tool:** `aura_search`

**Problem:** Failed to find `StepStatus` enum when searched directly.

**Impact:** Manual file reading required.

**Root Cause:** Enums may not be indexed or search not matching enum types.

**Proposed Solution:**
- Ensure enums are indexed in RAG
- Add enum-specific search handling
- Index enum values as searchable content

---

### Gap 17: Test Generation Overwrites Instead of Appending (HIGH)

**Tool:** `aura_generate(operation: "tests")`

**Problem:** Each call to generate tests for a different method replaces the entire file content instead of appending to existing test class.

**Impact:** Previously generated tests are lost; must generate all at once or manually merge.

**Root Cause:** `GenerateTestCodeAsync` writes full file content without checking for existing tests.

**Proposed Solution:**
- Detect existing test file and parse its content
- Append new test methods to existing test class
- Avoid duplicating already-existing test methods

---

### Gap 18: Roslyn Workspace Cache Staleness (HIGH) ✅ IMPLEMENTED

**Status:** Implemented - cache is now cleared after all file-writing operations

**Tool:** Multiple (`aura_generate`, `aura_inspect`, `aura_navigate`)

**Problem:** After generating new files or modifying code:
- `aura_inspect list_types` doesn't show newly created files
- `aura_generate method` can't find classes until cache invalidated
- `roslynWorkspaceCached: false` in worktrees

**Impact:** Tools fail silently or produce wrong results; requires manual `aura_workspace invalidate_cache` calls.

**Root Cause:** Roslyn workspace not automatically refreshed when files change.

**Solution Implemented:**
- Added `_workspaceService.ClearCache()` after all file-writing operations in:
  - `RoslynRefactoringService`: ImplementInterfaceAsync, GenerateConstructorAsync, ExtractInterfaceAsync, SafeDeleteAsync, AddPropertyAsync, AddMethodAsync, ChangeSignatureAsync
  - `RoslynTestGenerator`: GenerateTestsAsync
- Existing methods that already cleared cache: RenameAsync, CreateTypeAsync, MoveTypeToFileAsync, file rename/move

---

### Gap 19: aura_generate method Missing Test Attributes (HIGH)

**Tool:** `aura_generate(operation: "method")`

**Problem:** Methods added to test classes lack `[Fact]` or `[Test]` attributes.

**Impact:** xUnit analyzer errors; tests don't run.

**Root Cause:** Method generator doesn't detect test class context.

**Proposed Solution:**
- Detect if target class is a test class (by convention or by existing test attributes)
- Add appropriate test framework attribute (`[Fact]`, `[Test]`, `[TestMethod]`)
- Use detected framework from existing test methods in the class

---

### Gap 20: aura_validate tests Returns Truncated/Zero Results (MEDIUM)

**Tool:** `aura_validate(operation: "tests")`

**Problem:** Returns `total: 0` with truncated build output, even when tests exist and pass.

**Impact:** Can't verify test results through the tool.

**Root Cause:** Output parsing or test discovery issue.

**Proposed Solution:**
- Fix test result parsing
- Increase output buffer size
- Return structured test results (passed/failed/skipped counts)

---

### Gap 21: aura_generate method Formatting Issues (MEDIUM)

**Tool:** `aura_generate(operation: "method")`

**Problem:** Generated method body has inconsistent indentation, closing brace on same line as code.

**Impact:** Code style violations, harder to read.

**Proposed Solution:**
- Use proper C# formatting after code generation
- Apply .editorconfig settings
- Consider running `dotnet format` on generated code

---

## Related Documents

- [pattern-driven-stories.md](../completed/pattern-driven-stories.md) - Original spec
- [generate-tests pattern](../../../patterns/generate-tests.md) - Pattern being tested
