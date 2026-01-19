# Pattern-Driven Story UX Gaps

**Status:** ✅ Complete
**Created:** 2025-01-18
**Completed:** 2025-06-25
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
- **Gap 17** - ✅ Test generation now skips duplicate methods when appending
- **Gap 19** - ✅ aura_generate method now adds test attributes (auto-detect or specify testAttribute)
- **Gap 15** - ✅ aura_search now splits multi-word queries into symbol candidates
- **Gap 16** - ✅ aura_search uses case-insensitive matching for enums

### Test Generation Quality (NEW)
- ✅ Deduplicate test names (include parameter name for null tests)
- ✅ Generate NSubstitute mocks for constructor dependencies
- ✅ Use NSubstitute syntax instead of Moq
- ✅ Handle IReadOnlyDictionary/IReadOnlyList with proper generics
- ✅ Detect mocking library (NSubstitute, Moq, FakeItEasy) instead of hardcoding

### Critical - Blocking Pattern Execution
(All critical gaps resolved)

### High - MCP Tool Reliability
- **Gap 22** - ✅ File locking on parallel writes (per-file SemaphoreSlim locking)

### Medium - UX Improvements
- **Gap 20** - ✅ aura_validate tests improved output parsing (multiple formats, larger buffer)
- **Gap 21** - ✅ aura_generate method multi-statement body parsing
- **Gap 3** - ✅ Pattern + Enrich connection (pattern parameter loads and returns content)
- **Gap 1** - ✅ Story context injection (get_by_path operation)
- **Gap 7** - ✅ Panel recovery (showCurrentStory command, status bar click, Ctrl+Shift+S)
- **Gap 4** - ✅ Resume story experience (progress indicator, action buttons)
- **Gap 6** - ✅ Pattern binding (PatternName field on Workflow entity)

### Low
- **Gap 2** - ✅ Panel pinning (retainContextWhenHidden already enabled)
- **Gap 5** - ✅ Template steps visibility (patternName shown in panel UI)

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

### Gap 15: aura_search Returns Irrelevant Results (HIGH) ✅ IMPLEMENTED

**Tool:** `aura_search`

**Status:** Fixed in McpHandler.cs and CodeGraphService.cs

**Problem:** Searching for `IGitWorktreeService CreateAsync WorktreeResult` returned completely irrelevant results.

**Impact:** Had to use navigation tools as workaround.

**Root Cause:** Multi-word queries passed directly to exact name match. Semantic search not finding exact symbol matches.

**Solution Implemented:**
- Extract potential symbol names from query (split on whitespace)
- Search code graph for each symbol candidate individually
- Case-insensitive matching using ILIKE in PostgreSQL
- Partial FullName matching (e.g., `.ClassName` pattern)
- Priority ordering: interfaces, classes, enums first
- Combined results: exact matches first, then semantic results

---

### Gap 16: aura_search Fails to Find Enums (HIGH) ✅ IMPLEMENTED

**Tool:** `aura_search`

**Status:** Fixed alongside Gap 15

**Problem:** Failed to find `StepStatus` enum when searched directly.

**Impact:** Manual file reading required.

**Root Cause:** Case-sensitive exact matching in code graph query.

**Solution Implemented:**
- Case-insensitive matching using EF.Functions.ILike
- Enums already indexed as CodeNodeType.Enum
- Enums now prioritized in result ordering (score 85)

---

### Gap 17: Test Generation Overwrites Instead of Appending (HIGH) ✅ IMPLEMENTED

**Tool:** `aura_generate(operation: "tests")`

**Status:** Fixed in RoslynTestGenerator.cs - now checks for existing method names before appending.

**Problem:** Each call to generate tests for a different method replaces the entire file content instead of appending to existing test class.

**Impact:** Previously generated tests are lost; must generate all at once or manually merge.

**Root Cause:** `AppendToExistingTestFileAsync` didn't check for duplicate method names.

**Solution Implemented:**
- Added check for existing method names in test class
- Filters out tests that would duplicate existing methods
- Logs count of skipped duplicates

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

### Gap 19: aura_generate method Missing Test Attributes (HIGH) ✅ IMPLEMENTED

**Tool:** `aura_generate(operation: "method")`

**Status:** Fixed in RoslynRefactoringService.cs - now auto-detects test class and adds appropriate attribute.

**Problem:** Methods added to test classes lack `[Fact]` or `[Test]` attributes.

**Impact:** xUnit analyzer errors; tests don't run.

**Root Cause:** Method generator didn't detect test class context.

**Solution Implemented:**
- Added `DetectTestFramework()` to identify test classes by naming convention and existing attributes
- Added `CreateTestAttribute()` to generate appropriate attribute (Fact, Test, TestMethod)
- Added optional `testAttribute` parameter to allow caller to specify (e.g., `testAttribute: "Fact"`)
- Auto-detects framework from existing test methods in the class
- Falls back to xUnit if class looks like a test class but framework can't be detected

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

### Gap 22: File Locking on Parallel Writes (HIGH) ✅ IMPLEMENTED

**Tool:** `aura_generate(operation: "method")`, `aura_refactor`

**Problem:** When calling aura_generate method 3x in parallel to the same file, 2 fail with "file being used by another process". Sequential calls work fine.

**Impact:** Agents calling tools in parallel get failures; must serialize operations.

**Root Cause:** Roslyn workspace operations and file I/O are not thread-safe for concurrent access to the same file.

**Solution Implemented:**
- Added per-file locking using `SemaphoreSlim` keyed by normalized file path in `RoslynRefactoringService.cs`
- `ConcurrentDictionary<string, SemaphoreSlim>` manages locks by file path
- All 13 `File.WriteAllTextAsync` calls wrapped with `using (await AcquireFileLockAsync(...))`
- Operations on the same file wait for lock; different files proceed in parallel

---

### Gap 23: Duplicate Test Method Names for Overloads ✅ FIXED

**Tool:** `aura_generate(operation: "tests")`

**Problem:** When a class has overloaded methods (e.g., two `ExecuteAsync` overloads), Aura generates tests with identical names like `ExecuteAsync_WhenAwaited_CompletesSuccessfully` for both. This causes compile errors.

**Impact:** Generated tests don't compile; user must manually rename.

**Expected:** Differentiate test names by parameter signature:
- `ExecuteAsync_WithContext_WhenAwaited_CompletesSuccessfully`
- `ExecuteAsync_Simple_WhenAwaited_CompletesSuccessfully`

**Root Cause:** `RoslynTestGenerator.GenerateTestMethodName()` doesn't consider parameter types for uniqueness.

**Solution Implemented:**
1. Added `ParameterSignature` property to `TestGap` record
2. `IdentifyTestGaps()` now detects overloaded methods and computes a simplified parameter signature (e.g., "String_Int")
3. `GenerateTestName()` appends `_With{ParameterSignature}` when gap has overload disambiguation
4. Helper methods `ComputeParameterSignature()` and `SimplifyTypeName()` handle type normalization

**Result:** For overloaded methods, test names now include parameter types:
- `ExecuteAsync_WithString_WhenCalled_ReturnsExpectedResult`
- `ExecuteAsync_WithContext_CancellationToken_WhenCalled_ReturnsExpectedResult`

---

### Gap 24: Generated Tests Use Wrong Type/Property Names ✅ FIXED

**Tool:** `aura_generate(operation: "tests")`

**Problem:** Generated tests reference types and properties that don't exist:
- `GuardianCheck` (doesn't exist - should be `GuardianDefinition`)
- `Check` property (doesn't exist - should be `Detection`)
- Missing `using` statements for parameter types

**Impact:** Generated tests don't compile; user must fix all type references.

**Root Cause:** Generator wasn't collecting namespaces from all referenced types.

**Solution Implemented:**
1. Added `CollectRequiredNamespaces()` method that traverses:
   - Method return types
   - Method parameter types
   - Generic type arguments (recursively)
   - Constructor dependency types
2. Added `CollectNamespacesFromType()` recursive helper for deep type traversal
3. `GenerateNewTestFile()` now includes all required using statements
4. Added common System namespaces (System, System.Collections.Generic, System.Threading, etc.)

**Result:** Generated tests now include proper `using` statements for all referenced types.

---

### Gap 25: Skeleton TODOs Instead of Real Assertions ✅ FIXED

**Tool:** `aura_generate(operation: "tests")`

**Problem:** Many generated tests have `// TODO: Assert` placeholders instead of real assertions based on return types.

**Impact:** User must write all assertions manually; defeats purpose of generation.

**Solution Implemented:**
1. Added `GenerateAssertions()` method with return type analysis
2. Unwraps `Task<T>` and `ValueTask<T>` to get inner return type
3. Type-specific assertion generation:
   - `bool` → `Assert.True(result)` / `Assert.That(result, Is.True)`
   - `string` → `Assert.NotNull + Assert.NotEmpty`
   - Numeric types → `Assert.True(result >= 0)` with adjust comment
   - Collections → `Assert.NotNull + Assert.NotEmpty`
   - Reference types → `Assert.NotNull`
   - `void`/`Task` → Comment about no-throw success
4. Framework-aware generation (xUnit, NUnit, MSTest)
5. Added helper methods: `IsNumericType()`, `IsCollectionType()`, and assertion generators for each type category

**Result:** Generated tests now have meaningful assertions based on actual return types, with no TODO placeholders except for truly unhandled scenarios.

---

### Gap 26: No Compile Validation Before Output ✅ FIXED

**Tool:** `aura_generate(operation: "tests")`

**Problem:** Generated test code is returned even if it won't compile. User discovers errors only after file is written.

**Impact:** Wastes time fixing avoidable errors.

**Solution Implemented:**
1. Added `ValidateCompilation` option to `TestGenerationRequest` (opt-in to avoid latency for simple cases)
2. Added `CompilationDiagnostics` and `CompilesSuccessfully` properties to `GeneratedTests`
3. Implemented `ValidateGeneratedCodeAsync()` method that:
   - Parses generated code into a syntax tree
   - Adds it to the test project's compilation
   - Runs Roslyn diagnostics
   - Filters to errors/warnings from the generated file
   - Reports line numbers for each diagnostic
4. MCP schema updated with `validateCompilation: true` option
5. Response includes `compilesSuccessfully` boolean and `compilationDiagnostics` array

**Usage:**
```
aura_generate(operation: "tests", target: "MyClass", validateCompilation: true)
```

**Result:** Users can opt-in to compilation validation. Response will include:
- `compilesSuccessfully: true/false`
- `compilationDiagnostics: ["Error: CS0246 - The type or namespace 'Foo' could not be found (line 15)"]`

---

### Gap 27: Required Properties Not Initialized ✅ FIXED

**Tool:** `aura_generate(operation: "tests")`

**Problem:** C# 11+ `required` properties on dependency types are not set during test object creation, causing compile errors like "Required member 'WorkflowStep.StepId' must be set".

**Impact:** Generated tests don't compile; user must manually add object initializers.

**Root Cause:** `GenerateComplexTypeValue()` didn't detect or handle `required` modifier on properties.

**Solution Implemented:**
1. Added `GetRequiredProperties()` method using Roslyn to find properties with `required` modifier
2. Modified `GenerateComplexTypeValue()` to use object initializer syntax when required properties exist
3. Generates `new TypeName { RequiredProp1 = value, RequiredProp2 = value }` instead of `new TypeName()`
4. Uses `GenerateDefaultValue()` for each required property type

**Result:** Types with `required` properties now get proper object initializers with all required properties set.

---

### Gap 28: Constructor Dependency Namespaces Missing ✅ FIXED

**Tool:** `aura_generate(operation: "tests")`

**Problem:** Generic constructor dependencies like `ILogger<SomeClass>` don't get their namespace collected, causing "The type or namespace 'ILogger<>' could not be found" errors.

**Impact:** Generated tests don't compile due to missing `using Microsoft.Extensions.Logging;`.

**Root Cause:** Constructor dependencies were parsed as strings using `dep.TypeName.LastIndexOf('.')`, which fails for generic types like `ILogger<GuardianExecutor>` (no dot in minimally qualified format).

**Solution Implemented:**
1. Added `CollectConstructorDependencyNamespaces()` method
2. Uses actual `IParameterSymbol` from constructor to get real `ITypeSymbol`
3. Calls `CollectNamespacesFromType()` on each parameter type, which properly handles:
   - Generic types (collects namespace from generic definition and all type arguments)
   - Nested types
   - Array element types
4. Integrated into `CollectRequiredNamespaces()` pipeline

**Result:** `ILogger<T>`, `IOptions<T>`, and other generic dependencies now get proper namespace imports.

---

### Gap 29: Append Mode Missing Using Statements ✅ FIXED

**Tool:** `aura_generate(operation: "tests")`

**Problem:** When appending tests to an existing file, new `using` statements aren't added for types that weren't previously imported.

**Impact:** Tests that use types not already imported in the file fail to compile.

**Root Cause:** `AppendToExistingTestFileAsync` only inserted test method bodies before the class closing brace. It didn't check if new tests required namespaces not already in the file.

**Solution Implemented:**
1. Added namespace collection for the tests being appended (using existing `CollectRequiredNamespaces()`)
2. Extract existing usings from the file via Roslyn `UsingDirectiveSyntax`
3. Compute missing usings (required - existing)
4. Insert missing usings after the last existing `using` statement
5. Re-parse the file to get correct insertion position for test methods

**Result:** When appending tests to existing files, any missing `using` statements are automatically added.

---

### Gap 30: Test Files Created in Wrong Folder ✅ FIXED

**Tool:** `aura_generate(operation: "tests")`

**Problem:** New test files were placed in an arbitrary folder instead of mirroring the source file's folder structure. For example, `CodeGraphIndexer` in `Services/` was getting tests in `Agents/`.

**Impact:** Test files scattered incorrectly, requiring manual move to correct location.

**Root Cause:** `DetermineTestFilePath` was just using the first document in the test project to determine the target directory, rather than computing the relative path from the source.

**Solution Implemented:**
1. Get source file's relative path from its project root
2. Mirror that relative path in the test project
3. Example: `src/Aura.Module.Developer/Services/Testing/Foo.cs` → `tests/Aura.Module.Developer.Tests/Services/Testing/FooTests.cs`

**Result:** Test files are now created in the correct folder, matching the source file structure.

---

### Gap 31: No Way to Specify Output Directory ✅ FIXED

**Tool:** `aura_generate(operation: "tests")`

**Problem:** Users couldn't specify where test files should be placed. Even with the Gap 30 fix (mirroring source structure), if the source structure didn't match the desired test structure, users had to manually move files.

**Impact:** Extra manual step to move test files to preferred location.

**Solution Implemented:**
Added `outputDirectory` parameter to test generation:
- If relative path (e.g., `Services/Testing`): relative to the test project root
- If absolute path: uses it directly
- Overrides the automatic source-structure mirroring

**Usage:**
```json
{
  "operation": "tests",
  "target": "CodeGraphIndexer",
  "solutionPath": "c:\\work\\aura\\Aura.sln",
  "outputDirectory": "Services"
}
```

**Result:** Users can now control exactly where test files are created.

---

## Net Assessment: Test Generation

**Works Well:**
- `analyzeOnly: true` - Excellent for coverage gap discovery
- Simple classes (DTOs, modules with properties) - Time saver
- Quick scaffolding of test structure
- **NEW**: Overloads now have unique test names (Gap 23)
- **NEW**: All type namespaces included in usings (Gap 24)
- **NEW**: Return-type aware assertions instead of TODOs (Gap 25)
- **NEW**: Optional compilation validation (Gap 26)
- **NEW**: Required properties properly initialized (Gap 27)
- **NEW**: Generic dependency namespaces collected (Gap 28)
- **NEW**: Missing usings added when appending to existing files (Gap 29)
- **NEW**: Test files placed in correct folder mirroring source structure (Gap 30)
- **NEW**: `outputDirectory` parameter to control test file placement (Gap 31)
- **NEW**: Static classes handled correctly - no SUT instantiation (Gap 32)
- **NEW**: Mocking library namespace always included (Gap 33)
- **NEW**: `IOptions<T>` uses `Options.Create()` instead of mock (Gap 34)
- **NEW**: File-system heavy classes flagged as integration test candidates (Gap 35)

**Needs Work:**
- Validation adds latency (~1-2s) so is opt-in

---

### Gap 32: Static Class Detection ✅ FIXED

**Tool:** `aura_generate(operation: "tests")`

**Problem:** Aura generates instance-based tests for static classes (e.g., `var sut = new GuardianRegistryInitializer();`) when static classes can't be instantiated.

**Impact:** Generated tests don't compile for static classes.

**Root Cause:** `RoslynTestGenerator` doesn't check `INamedTypeSymbol.IsStatic` before generating constructor calls.

**Solution Implemented:** Check `typeSymbol.IsStatic` and generate static method calls (e.g., `TypeName.Method()`) instead of `sut.Method()`.

---

### Gap 33: Missing NSubstitute Using ✅ FIXED

**Tool:** `aura_generate(operation: "tests")`

**Problem:** Generated tests sometimes lack `using NSubstitute;` even when mocks are generated.

**Impact:** Tests don't compile - missing namespace.

**Root Cause:** NSubstitute namespace not added to required namespaces when mocking library is detected and appending to existing files.

**Solution Implemented:** Add mocking library namespace to `requiredNamespaces` when dependencies exist, for both new files and appends.

---

### Gap 34: IOptions<T> Mocking Pattern ✅ FIXED

**Tool:** `aura_generate(operation: "tests")`

**Problem:** Aura generates `Substitute.For<IOptions<T>>()` but NSubstitute can't mock `IOptions<T>.Value` properly - it returns null.

**Impact:** Tests fail at runtime with null reference.

**Root Cause:** `IOptions<T>.Value` is a property getter that NSubstitute doesn't handle well.

**Solution Implemented:** Detect `IOptions<T>` parameters and generate `Options.Create(new T())` instead of `Substitute.For<>()`. Also adds `using Microsoft.Extensions.Options;`.

---

### Gap 35: File-System Heavy Classes ✅ IMPLEMENTED

**Tool:** `aura_generate(operation: "tests")`

**Problem:** Classes like `RoslynWorkspaceService`, `GitWorktreeService` are too file-system dependent for unit tests.

**Impact:** Generated unit tests are not meaningful - need integration tests instead.

**Solution:** Detect classes with heavy file-system dependencies (constructor takes `IFileSystem`, multiple file operations) and either:
1. Flag in analysis as "integration test candidate"
2. Skip unit test generation with explanation
3. Generate integration test scaffolding instead

**Implementation:** Added `IsIntegrationTestCandidate` and `IntegrationTestReason` properties to `TestAnalysis` record. The `DetectIntegrationTestCandidate()` method in `RoslynTestGenerator` checks for:
- File-system dependencies: `IFileSystem`, `IFile`, `IDirectory`, `IPath`, etc.
- I/O dependencies: `IFileProvider`, `Stream`, `FileStream`, etc.
- Class name patterns suggesting file operations (only as supporting evidence)

---

## Related Documents

- [pattern-driven-stories.md](../completed/pattern-driven-stories.md) - Original spec
- [generate-tests pattern](../../../patterns/generate-tests.md) - Pattern being tested
