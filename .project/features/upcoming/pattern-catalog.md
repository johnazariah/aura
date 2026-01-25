# Feature: Pattern Catalog

**Status:** 📋 Spec
**Priority:** High
**Depends On:** [Pattern-Driven Stories](pattern-driven-stories.md)

## Summary

Define and implement a comprehensive catalog of operational patterns for common software development tasks. Each pattern follows the Pattern-Driven Stories execution model: analysis → user-modifiable plan → step-by-step execution → PR.

## Prerequisite: Pattern-Driven Stories

This spec assumes the execution model from [Pattern-Driven Stories](pattern-driven-stories.md) is implemented. Key concepts:

- **Analysis → Items**: Each pattern produces discrete, typed items (not prose)
- **User Control**: Users can add, remove, disable, reorder items before execution
- **Tiered Ceremony**: Small patterns execute inline; large patterns create stories in worktrees
- **PR as Artifact**: Large patterns produce a reviewable PR with fine-grained commits

The patterns in this catalog define **what** gets analyzed and **what items** are produced. Pattern-Driven Stories defines **how** those items are presented, modified, and executed.

---

## Pattern Categories

### 1. Refactoring Patterns

Patterns that modify existing code structure.

| Pattern | Trigger | Complexity |
|---------|---------|------------|
| [Comprehensive Rename](#comprehensive-rename) | "Rename X to Y across codebase" | High |
| [Extract Service](#extract-service) | "This class does too much" | Medium |
| [Extract Interface](#extract-interface) | "Need to mock this for testing" | Low |
| [Change Signature](#change-signature) | "Add parameter to interface method" | High |
| [Move to Module](#move-to-module) | "This belongs in a different project" | High |
| [Safe Delete](#safe-delete) | "Remove unused code" | Medium |

### 2. Code Generation Patterns

Patterns that create new code.

| Pattern | Trigger | Complexity |
|---------|---------|------------|
| [Generate Tests](#generate-tests) | "Add tests for this class/module" | Medium |
| [Add Endpoint](#add-endpoint) | "Expose X via API" | Medium |
| [Add Entity](#add-entity) | "Need a new database table" | Medium |
| [Implement Interface](#implement-interface) | "Create implementation of IX" | Low |

### 3. Quality Patterns

Patterns that improve code quality.

| Pattern | Trigger | Complexity |
|---------|---------|------------|
| [Documentation Sweep](#documentation-sweep) | "Update XML docs for public API" | Medium |
| [Test Coverage Boost](#test-coverage-boost) | "Get coverage above X%" | High |
| [Null Safety Sweep](#null-safety-sweep) | "Enable nullable reference types" | High |
| [Validation Sweep](#validation-sweep) | "Add input validation" | Medium |

### 4. Architectural Patterns

Patterns that enforce or improve architecture.

| Pattern | Trigger | Complexity |
|---------|---------|------------|
| [Layer Violation Fix](#layer-violation-fix) | "Enforce layered architecture" | High |
| [Dependency Untangle](#dependency-untangle) | "Break circular dependencies" | High |
| [Pattern Compliance](#pattern-compliance) | "All repos should follow X pattern" | Medium |
| [API Surface Audit](#api-surface-audit) | "What's our public API?" | Low |

### 5. Migration Patterns

Patterns for evolving systems.

| Pattern | Trigger | Complexity |
|---------|---------|------------|
| [Deprecation Wave](#deprecation-wave) | "Phase out V1 API" | Medium |
| [Extract Microservice](#extract-microservice) | "Split this into separate service" | Very High |
| [Framework Upgrade](#framework-upgrade) | "Upgrade from .NET 8 to 9" | High |

---

## Pattern Specifications

### Comprehensive Rename

**Status:** ✅ Exists (`patterns/comprehensive-rename.md`)

**Trigger:** User wants to rename a domain concept across the entire codebase.

**Analysis Output:**
- All symbols matching the name pattern
- Reference counts per symbol
- Related symbols discovered by convention
- Files affected

**Phases:**
1. Core types (classes, interfaces, enums)
2. Members (properties, methods, fields)
3. Services and repositories
4. Files and folders
5. API routes and DTOs
6. Manual steps (database, external systems)
7. Verification (build, test, sweep)

---

### Extract Service

**Status:** 📋 To implement

**Trigger:** "This class is doing too much" / "Extract the X functionality into a service"

**Analysis Output:**
- Source class members
- Proposed members to extract
- Dependencies of extracted members
- Callers of extracted members

**Phases:**
1. Create new service interface
2. Create service implementation with extracted methods
3. Add DI registration
4. Inject service into original class
5. Replace direct calls with service calls
6. Verification

**Example:**
```
User: "Extract the email sending logic from OrderService into EmailService"

Phase 1: Create Interface
☑ Create IEmailService with:
  - SendOrderConfirmation(Order) 
  - SendShippingNotification(Order)

Phase 2: Create Implementation  
☑ Create EmailService implementing IEmailService
  - Move SendOrderConfirmation from OrderService
  - Move SendShippingNotification from OrderService
  - Move _smtpClient field

Phase 3: Configure DI
☑ Add services.AddScoped<IEmailService, EmailService>()

Phase 4: Update OrderService
☑ Add IEmailService constructor parameter
☑ Replace this.SendOrderConfirmation → _emailService.SendOrderConfirmation
☑ Replace this.SendShippingNotification → _emailService.SendShippingNotification

Phase 5: Verification
☑ Build solution
☑ Run tests
```

---

### Extract Interface

**Status:** 📋 To implement

**Trigger:** "Need to mock this for testing" / "Extract interface from X"

**Analysis Output:**
- Class public members
- Which members to include (all, queries only, commands only)
- Existing consumers

**Phases:**
1. Create interface file with selected members
2. Add interface implementation to class
3. Update DI registration
4. (Optional) Update consumers to use interface

**Example:**
```
Phase 1: Create Interface
☑ IWorkflowService with 12 methods:
  - GetByIdAsync(Guid)
  - GetAllAsync()
  - CreateAsync(CreateRequest)
  - ...

Phase 2: Implement Interface
☑ WorkflowService : IWorkflowService
```

---

### Change Signature

**Status:** 📋 To implement (extends existing `aura_refactor`)

**Trigger:** "Add parameter X to method Y" / "Change return type of Z"

**Analysis Output:**
- Method/interface affected
- All implementations
- All call sites (including expression trees, delegates)
- Breaking change assessment

**Phases:**
1. Update interface signature
2. Update each implementation
3. Update call sites (with default value if applicable)
4. Verification

---

### Move to Module

**Status:** 📋 To implement

**Trigger:** "This class belongs in project X" / "Move Foo to Core"

**Analysis Output:**
- Type to move
- Current namespace
- Target project and namespace
- Dependencies that would create circular references
- Dependents that would need project reference

**Phases:**
1. Analyze dependency impact
2. Move file to target project
3. Update namespace
4. Update using statements in all referencing files
5. Add/verify project references
6. Verification

---

### Safe Delete

**Status:** ✅ Exists in `aura_refactor`

**Trigger:** "Remove unused code" / "Delete the legacy X"

**Analysis Output:**
- Symbol to delete
- Any remaining references
- Impact of deletion

---

### Generate Tests

**Status:** ✅ Exists (`patterns/generate-tests.md`)

**Trigger:** "Add tests for this class/module"

**Analysis Output:**
- Testable classes
- Public method count per class
- Existing test coverage
- Dependencies requiring mocks

**Phases:**
1. Unit tests per class
2. Integration tests (if DB/external)
3. Verification (run tests)

---

### Add Endpoint

**Status:** 📋 To implement

**Trigger:** "Expose X via API" / "Add endpoint for Y"

**Analysis Output:**
- Service method to expose
- HTTP method recommendation
- Route recommendation
- DTO requirements

**Phases:**
1. Create request DTO (if needed)
2. Create response DTO (if needed)
3. Add endpoint to controller/Program.cs
4. Add OpenAPI annotations
5. Add authorization attribute
6. Verification

---

### Add Entity

**Status:** 📋 To implement

**Trigger:** "Need a new database table" / "Add Order entity"

**Analysis Output:**
- Entity name and properties
- Relationships to existing entities
- Required indexes

**Phases:**
1. Create entity class
2. Add DbSet to context
3. Configure entity (keys, indexes, relationships)
4. Create migration
5. Create repository interface
6. Create repository implementation
7. Register in DI
8. Create DTO
9. Verification

---

### Implement Interface

**Status:** ✅ Exists in `aura_generate`

**Trigger:** "Create implementation of IX"

---

### Documentation Sweep

**Status:** 📋 To implement

**Trigger:** "Update XML docs for public API" / "Document the X module"

**Analysis Output:**
- Public types and members
- Current documentation status (missing, partial, complete)
- Methods missing parameter docs
- Methods missing return docs

**Phases:**
1. Add missing type summaries
2. Add missing method summaries
3. Add missing parameter descriptions
4. Add missing return descriptions
5. Add missing exception documentation
6. Verification (build with warnings-as-errors for docs)

**Why Pattern-Driven:**
- Large surface area (could be 100+ undocumented members)
- User may want to prioritize (public API first, internals later)
- Quality matters (not just templated "Gets the X")

---

### Test Coverage Boost

**Status:** 📋 To implement

**Trigger:** "Get coverage above 80%" / "Add tests for uncovered code"

**Analysis Output:**
- Current coverage per class/method
- Uncovered lines
- Complexity of uncovered code
- Priority ranking (public API > internal, high complexity > low)

**Phases:**
1. Run coverage analysis
2. Identify gaps prioritized by:
   - Public API surface
   - Cyclomatic complexity
   - Lines of code
3. Generate tests for each gap
4. Re-run coverage
5. Iterate until threshold met

**Why Pattern-Driven:**
- Iterative process, not one-shot
- User may exclude certain code
- Coverage threshold is user-defined

---

### Null Safety Sweep

**Status:** 📋 To implement

**Trigger:** "Enable nullable reference types" / "Fix null warnings"

**Analysis Output:**
- Current nullable warnings
- Parameters that should be nullable
- Return types that can be null
- Properties that need null guards

**Phases:**
1. Enable nullable in project file
2. Add nullable annotations to parameters
3. Add nullable annotations to return types
4. Add null guards where appropriate
5. Fix remaining warnings
6. Verification

---

### Validation Sweep

**Status:** 📋 To implement

**Trigger:** "Add input validation to API"

**Analysis Output:**
- Endpoints without validation
- DTO properties needing validation
- Recommended validation rules

**Phases:**
1. Add validation attributes/FluentValidation rules
2. Add validation filter/middleware
3. Generate tests for validation
4. Verification

---

### Layer Violation Fix

**Status:** 📋 To implement

**Trigger:** "Enforce layered architecture" / "Fix layer violations"

**Analysis Output:**
- Defined layers (Controller → Service → Repository → Data)
- Current violations
- Suggested fixes per violation

**Phases:**
1. Define layer rules
2. Scan for violations
3. Present violations with fix options:
   - Move type to correct layer
   - Inject through interface
   - Extract shared type to contracts
4. Execute fixes per user selection
5. Add compile-time layer enforcement (optional)
6. Verification

**Example:**
```
Violation: WorkflowController.cs:145 directly calls _workflowRepository.GetById()
Fix Options:
  [A] Route through IWorkflowService (add method if needed)
  [B] Move this to a service method
  [C] Ignore (add exception annotation)
```

---

### Dependency Untangle

**Status:** 📋 To implement

**Trigger:** "Break circular dependencies" / "Why can't I add this reference?"

**Analysis Output:**
- Complete dependency graph
- Circular dependency chains
- Types causing cycles
- Suggested moves to break cycles

**Phases:**
1. Build full dependency graph
2. Identify cycles
3. Propose type moves to break cycles (prioritized by impact)
4. Execute moves per user selection
5. Verification

---

### Pattern Compliance

**Status:** 📋 To implement

**Trigger:** "All repositories should follow X pattern" / "Audit against pattern"

**Analysis Output:**
- Pattern definition (interface shape, required members, naming)
- Implementations found
- Compliance per implementation
- Violations per implementation

**Phases:**
1. Define pattern (or load from template)
2. Find implementations
3. Audit each implementation
4. Present violations
5. Fix violations per user selection
6. Verification

**Example:**
```
Pattern: Repository
Required:
  - Constructor(IDbContext)
  - Implements IDisposable
  - Async methods return Task<T>

Found 20 implementations, 4 violations:
  ❌ OrderRepository: Missing IDisposable
  ❌ CustomerRepository: GetById returns Customer, not Task<Customer>
```

---

### API Surface Audit

**Status:** 📋 To implement

**Trigger:** "What's our public API?" / "What would break externally?"

**Analysis Output:**
- Public types
- Public members
- External usage (if detectable)
- Stability classification (stable, at-risk, internal-only)

---

### Deprecation Wave

**Status:** 📋 To implement

**Trigger:** "Phase out V1 API" / "Deprecate the legacy X"

**Analysis Output:**
- Items to deprecate
- Current consumers
- Replacement recommendations

**Phases:**
1. Add `[Obsolete]` attributes with messages
2. Add sunset headers (for APIs)
3. Update documentation
4. Log deprecation warnings
5. (Later phase) Remove deprecated items

---

### Extract Microservice

**Status:** 📋 To implement

**Trigger:** "Split X into separate service"

**Analysis Output:**
- Types in extraction scope
- Incoming dependencies (other code depends on this)
- Outgoing dependencies (this depends on other code)
- Data access patterns
- Estimated effort

**Phases:**
1. Create contracts project (interfaces, DTOs, events)
2. Create service project
3. Move types to appropriate projects
4. Create client for consuming code
5. Replace direct calls with client calls
6. Update data access (if splitting database)
7. Add API gateway routing (if applicable)
8. Verification

**Why Pattern-Driven:**
- Very high impact, needs extensive user review
- Many optional steps (split DB or not?)
- Might be executed over multiple PRs

---

### Framework Upgrade

**Status:** 📋 To implement

**Trigger:** "Upgrade to .NET 9" / "Migrate from Newtonsoft to System.Text.Json"

**Analysis Output:**
- Breaking changes in target version
- Affected code in codebase
- Required code changes
- Deprecated API usage

**Phases:**
1. Update project files (TFM, package versions)
2. Fix breaking changes
3. Replace deprecated APIs
4. Update build scripts
5. Update CI configuration
6. Verification

---

## Implementation Roadmap

### Wave 1: Core Refactoring (Q1)

Foundation patterns used frequently:

1. **Extract Interface** — simple, high utility
2. **Change Signature** — extends existing tool
3. **Extract Service** — common refactoring need

### Wave 2: Code Quality (Q1-Q2)

Patterns that improve quality:

4. **Documentation Sweep** — high visibility
5. **Test Coverage Boost** — ties into generate-tests
6. **Null Safety Sweep** — .NET-specific but valuable

### Wave 3: Architecture (Q2)

Patterns for structural improvement:

7. **Layer Violation Fix** — demo-worthy
8. **Pattern Compliance** — enterprise value
9. **Dependency Untangle** — solves real pain

### Wave 4: Advanced (Q2-Q3)

High-value but complex:

10. **Add Entity** — full vertical slice
11. **Add Endpoint** — full vertical slice
12. **Move to Module** — cross-project refactoring

### Wave 5: Migration (Q3+)

Large-scale operations:

13. **Deprecation Wave**
14. **Extract Microservice**
15. **Framework Upgrade**

---

## Open Questions

1. **Pattern templates:** Should patterns be defined in YAML/JSON for extensibility, or is markdown sufficient?

2. **Cross-language:** Which patterns apply to non-C# codebases? (generate-tests, documentation-sweep are language-agnostic)

3. **Pattern chaining:** Can patterns call sub-patterns? (e.g., Extract Microservice calls Move to Module)

4. **Community patterns:** Should users be able to share custom patterns?

---

## See Also

- [Pattern-Driven Stories](pattern-driven-stories.md) — execution model
- [Operational Patterns](../completed/operational-patterns.md) — initial implementation
- [Demo Playbook](../../docs/demo-playbook.md) — demo scenarios that informed this catalog
