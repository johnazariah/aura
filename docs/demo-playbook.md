# Aura Roslyn Tools — Demo Playbook

> **Purpose:** Comprehensive guide for demonstrating Aura's Roslyn-powered code editing and refactoring capabilities.

---

## Positioning: ReSharper + LLM

### The Elevator Pitch

> "ReSharper makes a skilled developer faster. Aura makes every developer as capable as your best architect — because the AI knows when and how to apply the right refactoring."

### The Core Insight

| ReSharper | LLM Alone | Aura |
|-----------|-----------|------|
| Precise refactoring | Understands intent | **Intent + Precision** |
| Requires human expertise | Makes fragile text edits | LLM picks the right Roslyn tool |
| One refactoring at a time | Can chain but breaks things | Chains multiple refactorings safely |
| Manual, interactive | Automated but risky | Automated and verified |
| Single developer productivity | Scales but unreliably | AI agent at scale, reliably |

### How It Works

```
User: "This method is too long, extract the validation logic"

ReSharper: Developer must select code → Ctrl+Alt+M → name method → confirm

LLM alone: *attempts regex-based extraction, probably breaks something*

Aura: LLM analyzes method → identifies validation code → 
      calls aura_extract_method with correct line range → 
      Roslyn does the extraction safely → compilation verified
```

### What Makes This Different

| Capability | ReSharper | Aura |
|------------|-----------|------|
| **Natural language interface** | ❌ Menus and shortcuts | ✅ Describe what you want |
| **Intent-based chaining** | ❌ One at a time | ✅ "Make this class immutable" = coordinated refactorings |
| **Cross-cutting changes** | ❌ Manual repetition | ✅ "Add logging to all public methods" |
| **Architectural enforcement** | ❌ Manual review | ✅ "Ensure all repositories follow this pattern" |
| **AI-assisted decisions** | ❌ Human must choose | ✅ LLM suggests which refactoring applies |

### Competitive Positioning

| Tool | Strength | Weakness | Aura Advantage |
|------|----------|----------|----------------|
| **ReSharper/Rider** | Precise refactoring | Requires human expertise | AI provides the expertise |
| **Copilot** | Understands intent | Text-based, fragile | Roslyn provides precision |
| **Cursor** | Good code generation | Still text-based edits | Semantic, not textual |
| **CodeWhisperer** | AWS integration | Limited refactoring | Full refactoring suite |

### The Demo Message

When presenting, emphasize this narrative:

1. **"LLMs are great at understanding what you want"** — Show a natural language request
2. **"But they're terrible at making precise code changes"** — Show a text-based edit failing
3. **"Roslyn is great at precise changes"** — Show ReSharper doing a refactoring
4. **"But requires expertise to use correctly"** — Note the menu navigation, decisions
5. **"Aura combines both"** — LLM understands, Roslyn executes, verification confirms

---

## Table of Contents

- [Audience Segments](#audience-segments)
- [Demo Tier 1: The "Wow" Moments](#demo-tier-1-the-wow-moments)
- [Demo Tier 2: Productivity Multipliers](#demo-tier-2-productivity-multipliers)
- [Demo Tier 3: Enterprise Scenarios](#demo-tier-3-enterprise-scenarios)
- [Demo Tier 4: "Only Roslyn Can Do This"](#demo-tier-4-only-roslyn-can-do-this)
- [Presentation Formats](#presentation-formats)
- [Demo Environment Setup](#demo-environment-setup)
- [Objection Handling](#objection-handling)
- [Success Metrics](#success-metrics)

---

## Audience Segments

| Audience | What They Care About | Demo Focus |
|----------|---------------------|------------|
| **Individual Developers** | Productivity, less tedium | Speed, accuracy, "magic" moments |
| **Tech Leads / Architects** | Code quality, consistency | Refactoring safety, pattern enforcement |
| **Engineering Managers** | Risk reduction, velocity | Error prevention, time savings |
| **Enterprise / Security** | Compliance, auditability | Traceability, guaranteed coverage |

---

## Demo Tier 1: The "Wow" Moments

### Demo 1.1: The Cascade Rename

**Setup:** Method used in 50+ places across 15 files

**Script:**

```
You: "Rename 'GetWorkflow' to 'GetWorkflowAsync' — it's an async method 
      but the previous developer forgot the naming convention."

Aura: [Single tool call]
      "Renamed 'GetWorkflow' to 'GetWorkflowAsync'
       ├─ 1 method definition
       ├─ 2 interface declarations  
       ├─ 3 implementations
       ├─ 47 call sites
       ├─ 5 XML doc references
       └─ Compilation verified ✅
       
       Time: 1.2 seconds"

You: [Show git diff] — Exactly 58 surgical changes, no collateral damage
```

**Why it impresses:**

- Everyone has done this manually — they know the pain
- The number (58 changes) is concrete and verifiable
- "Compilation verified" shows it's not just find-replace

**Talking point:** *"An LLM would grep for the string and miss the interface implementations. Roslyn knows these are all the same symbol."*

---

### Demo 1.2: The Impossible Refactor

**Setup:** Add required parameter to interface method

**Script:**

```
You: "I need to add a CancellationToken to IWorkflowEngine.ExecuteAsync(). 
      This is called everywhere."

Aura: "Analyzing impact...
       
       IWorkflowEngine.ExecuteAsync is:
       ├─ Defined in: IWorkflowEngine.cs:24
       ├─ Implemented by: 3 classes
       │   ├─ StandardWorkflowEngine
       │   ├─ DebugWorkflowEngine  
       │   └─ MockWorkflowEngine (in tests)
       └─ Called from: 34 locations
           ├─ 28 direct calls
           ├─ 4 via delegate
           └─ 2 via expression tree (!)

       Proceed with adding 'CancellationToken ct = default'? [Y/n]"

You: "Yes"

Aura: "Applied changes:
       ├─ Modified interface signature
       ├─ Updated 3 implementations
       ├─ Updated 34 call sites (added 'default' argument)
       └─ Compilation verified ✅"
```

**Why it impresses:**

- "Expression tree" callout shows deep analysis humans miss
- Shows the full blast radius BEFORE making changes
- Interactive confirmation for safety

**Talking point:** *"Notice it found calls through expression trees — those would be runtime failures if we missed them. Roslyn sees what grep can't."*

---

### Demo 1.3: The Pattern Police

**Setup:** Codebase with 20 repository classes

**Script:**

```
You: "Audit all IRepository implementations. They should all:
      1. Have a constructor taking IDbContext
      2. Implement IDisposable
      3. Have async methods return Task<T>, not T"

Aura: [Uses aura_find_implementations, aura_get_type_members on each]

      "Repository Pattern Audit:
       
       20 implementations found
       
       ❌ Violations:
       ├─ OrderRepository: Missing IDisposable
       ├─ CustomerRepository: GetById returns Customer, not Task<Customer>
       ├─ LegacyProductRepo: Constructor takes DbContext, not IDbContext
       └─ ReportRepository: Missing IDisposable
       
       ✅ Compliant: 16 repositories
       
       Would you like me to fix the violations?"

You: "Yes"

Aura: [Uses aura_implement_interface, aura_change_method_signature]
      
      "Fixed 4 violations:
       ├─ Added IDisposable to OrderRepository, ReportRepository
       ├─ Changed CustomerRepository.GetById return type to Task<Customer>
       │   └─ Updated 12 call sites to await the result
       └─ Changed LegacyProductRepo constructor parameter type
       
       Compilation verified ✅"
```

**Why it impresses:**

- Systematic enforcement across entire codebase
- Finds violations humans would miss in code review
- Fixes them correctly, including cascading changes

**Talking point:** *"This is architectural governance at scale. Every new hire, every PR — consistent patterns enforced automatically."*

---

## Demo Tier 2: Productivity Multipliers

### Demo 2.1: The Interface Extraction

**Setup:** Fat service class with 15 public methods

**Script:**

```
You: "Extract an interface from WorkflowService — I need to mock it for testing"

Aura: "Analyzing WorkflowService...
       
       15 public members found:
       ├─ 12 methods
       ├─ 2 properties
       └─ 1 event
       
       Generating IWorkflowService..."

      [Creates IWorkflowService.cs with all signatures]
      [Adds ': IWorkflowService' to class declaration]
      [Optionally updates DI registration]

      "Created IWorkflowService with 15 members
       Updated WorkflowService to implement IWorkflowService
       Compilation verified ✅"
```

**Variation — Selective extraction:**

```
You: "Extract interface with only the public query methods, not the mutations"

Aura: [Analyzes which methods are queries vs commands]
      "Extracting 5 query methods to IWorkflowQueryService..."
```

---

### Demo 2.2: The Constructor Generator

**Setup:** Class with 8 injected dependencies as fields

**Script:**

```
You: "Generate a constructor for AgentOrchestrator — it has a lot of dependencies"

Aura: "Found 8 private readonly fields:
       ├─ _agentRegistry: IAgentRegistry
       ├─ _llmProvider: ILlmProvider
       ├─ _workflowStore: IWorkflowStore
       ├─ _logger: ILogger<AgentOrchestrator>
       ├─ _metrics: IMetricsService
       ├─ _cache: IDistributedCache
       ├─ _eventBus: IEventBus
       └─ _options: IOptions<AgentOptions>
       
       Generating constructor..."

      public AgentOrchestrator(
          IAgentRegistry agentRegistry,
          ILlmProvider llmProvider,
          IWorkflowStore workflowStore,
          ILogger<AgentOrchestrator> logger,
          IMetricsService metrics,
          IDistributedCache cache,
          IEventBus eventBus,
          IOptions<AgentOptions> options)
      {
          _agentRegistry = agentRegistry ?? throw new ArgumentNullException(nameof(agentRegistry));
          _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
          // ... etc
      }
```

**Talking point:** *"This is boilerplate nobody should write by hand. But notice the null checks — Roslyn knows these are reference types."*

---

### Demo 2.3: The Dead Code Finder

**Setup:** Legacy codebase with accumulated cruft

**Script:**

```
You: "Find all unused public methods in the Aura.Foundation project"

Aura: [Builds complete call graph]

      "Analyzed 342 public methods in Aura.Foundation
       
       Found 14 unreferenced methods:
       
       Definitely unused (0 callers in solution):
       ├─ RagService.LegacySearch() — RagService.cs:156
       ├─ AgentContext.FromJson() — AgentContext.cs:78
       └─ EmbeddingCache.Invalidate() — EmbeddingCache.cs:203
       
       Possibly unused (only test callers):
       ├─ ChunkingService.ChunkByTokens() — only called from ChunkingTests
       └─ [8 more...]
       
       Likely API surface (keep):
       ├─ Extension methods (may be called by consumers)
       └─ [3 marked with [PublicAPI] attribute]
       
       Would you like to remove the definitely unused methods?"
```

**Why it impresses:**

- Categorizes results intelligently
- Distinguishes "unused" from "public API"
- Offers safe deletion with verification

---

## Demo Tier 3: Enterprise Scenarios

### Demo 3.1: The Breaking Change Assessment

**Setup:** Planning a major version release

**Script:**

```
You: "I want to change WorkflowStatus from an enum to a class for extensibility. 
      What's the impact?"

Aura: "Analyzing WorkflowStatus enum...
       
       Current usage:
       ├─ 156 direct references
       ├─ 23 switch statements
       ├─ 8 comparisons (== WorkflowStatus.Completed)
       ├─ 4 serialization points (JSON)
       └─ 2 database mappings (EF Core)
       
       Breaking change analysis:
       
       🔴 High impact (requires code changes):
       ├─ Switch statements won't compile with class
       ├─ Enum comparisons need .Equals() or pattern matching
       └─ EF Core HasConversion needs update
       
       🟡 Medium impact (behavior change):
       └─ JSON serialization will change shape
       
       🟢 Low impact (compatible):
       └─ Property assignments will work
       
       Estimated migration: 45 code changes across 18 files
       
       Would you like a detailed migration plan?"
```

**Talking point:** *"Before you commit to a breaking change, you know exactly what you're signing up for. This is release planning with data."*

---

### Demo 3.2: The Compliance Audit

**Setup:** Security review requirement

**Script:**

```
You: "Find all methods that access the database but don't require authorization"

Aura: [Traces from IDbContext usage up to controller endpoints]

      "Database access path analysis:
       
       Found 8 code paths from HTTP endpoint to database without [Authorize]:
       
       1. GET /api/reports/public/{id}
          └─ ReportController.GetPublic() [AllowAnonymous]
              └─ ReportService.GetById()
                  └─ _context.Reports.FindAsync() ⚠️
       
       2. GET /api/health
          └─ HealthController.Check() [AllowAnonymous]  
              └─ _context.Database.CanConnectAsync() ⚠️
       
       [6 more paths...]
       
       Recommendation:
       ├─ #1: Intentional? Reports are public. Verify business logic.
       └─ #2: Acceptable for health checks.
       
       Generate security audit report? [Y/n]"
```

**Talking point:** *"For SOC 2, HIPAA, or any compliance audit — you need to prove every data access is authorized. This is that proof."*

---

### Demo 3.3: The Dependency Untangling

**Setup:** Circular dependency between projects

**Script:**

```
You: "Why can't I add a reference from Aura.Api to Aura.Module.Developer?"

Aura: "Analyzing project dependencies...
       
       ⚠️ Circular dependency detected:
       
       Aura.Api 
         → Aura.Foundation
           → Aura.Module.Developer (proposed)
             → Aura.Foundation ✓ (OK)
             → Aura.Api ❌ (CYCLE!)
       
       The cycle is caused by:
       ├─ Aura.Module.Developer.WorkflowController references Aura.Api.Filters.ValidateModelAttribute
       └─ This type should live in Aura.Foundation or a shared package
       
       Suggested fix:
       1. Move ValidateModelAttribute to Aura.Foundation
       2. Update 3 files that reference it
       
       Execute fix? [Y/n]"
```

---

## Demo Tier 4: "Only Roslyn Can Do This"

### Demo 4.1: The Expression Tree Refactor

**Setup:** LINQ expressions compiled to expression trees

**Script:**

```
You: "Rename the 'CustomerId' property on Order — but I use it in EF Core queries"

Aura: "Found 'CustomerId' in Order class
       
       References found:
       ├─ 12 direct property accesses
       ├─ 3 LINQ queries (IQueryable<T>)
       │   └─ These compile to Expression<Func<T>> — property name is embedded!
       └─ 2 EF Core configurations (.HasIndex(o => o.CustomerId))
       
       All references will be updated including expression trees.
       
       Note: If you have raw SQL or string-based queries, search for 'CustomerId' 
       in .sql files and string literals.
       
       Proceed? [Y/n]"
```

**Why only Roslyn:** Expression trees embed property names as strings at compile time. Grep finds the source, but doesn't understand that `o => o.CustomerId` becomes `"CustomerId"` in the expression tree.

---

### Demo 4.2: The Overload Disambiguation

**Setup:** Multiple method overloads

**Script:**

```
You: "Add a 'priority' parameter to ProcessOrder, but only the overload that takes an OrderRequest"

Aura: "Found 3 overloads of ProcessOrder:
       
       1. ProcessOrder(int orderId) — 23 callers
       2. ProcessOrder(OrderRequest request) — 45 callers ← THIS ONE
       3. ProcessOrder(int orderId, bool expedite) — 8 callers
       
       Adding 'int priority = 0' to overload #2 only...
       
       Updated:
       ├─ Method signature in OrderService.cs:89
       ├─ Interface signature in IOrderService.cs:34
       └─ 0 call sites (new parameter has default value)
       
       Compilation verified ✅"
```

**Why only Roslyn:** Grep would find all three overloads. LLM might confuse them. Roslyn knows exactly which overload each call site binds to.

---

## Demo Tier 5: Architectural Intelligence

These demos go beyond single refactorings into whole-codebase analysis that no human would do manually and no LLM could do alone.

### Demo 5.1: The Dependency Minimizer

**Setup:** Solution with 10+ projects that have grown organically

**Script:**

```
You: "Analyze which classes should move between projects to minimize dependencies"

Aura: "Building complete dependency graph...
       
       Current state:
       ├─ 10 projects
       ├─ 847 types
       ├─ 2,341 cross-project references
       └─ 23 circular dependency chains
       
       Analysis complete. Found optimization opportunities:
       
       🔴 High Impact Moves (reduces dependencies significantly):
       
       1. Move 'ValidationResult' from Aura.Api → Aura.Foundation
          ├─ Currently: Api references Foundation, Foundation references Api (cycle!)
          ├─ After move: Eliminates cycle, reduces references by 34
          └─ Affected files: 12
       
       2. Move 'JsonSerializerOptions' extensions from Aura.Module.Developer → Aura.Foundation
          ├─ Currently: 4 projects duplicate this code
          ├─ After move: Single source of truth
          └─ Affected files: 8
       
       3. Extract 'Aura.Contracts' project for shared DTOs
          ├─ Currently: DTOs scattered across projects cause reference chains
          ├─ After move: Clean separation of contracts
          └─ Types to extract: OrderDto, WorkflowDto, AgentResultDto (15 types)
       
       🟡 Medium Impact:
       └─ [5 more recommendations...]
       
       🟢 Already Optimal:
       └─ Aura.ServiceDefaults has minimal dependencies ✓
       
       Estimated improvement: 
       ├─ 23 → 0 circular dependencies
       ├─ 2,341 → 1,892 cross-project references (-19%)
       └─ Build time improvement: ~15% (less recompilation)
       
       Would you like to:
       [1] See detailed migration plan
       [2] Execute high-impact moves
       [3] Export as architecture diagram"
```

**Why this is impossible without Roslyn:**
- Requires complete type dependency graph
- Must trace through generics, interfaces, inheritance
- LLM can't hold entire codebase in context
- Grep can't understand what "depends on" means semantically

**Why LLM adds value:**
- Explains *why* each move helps
- Prioritizes by business impact, not just technical metrics
- Suggests architectural patterns (extract contracts project)

---

### Demo 5.2: The Layer Violation Detector

**Setup:** Layered architecture (Controller → Service → Repository)

**Script:**

```
You: "Verify our layered architecture. Controllers should only call services, 
      services should only call repositories, repositories should only access DbContext."

Aura: "Analyzing architectural layers...
       
       Defined layers:
       ├─ Layer 1: Controllers (*.Controller.cs)
       ├─ Layer 2: Services (*Service.cs, I*Service.cs)
       ├─ Layer 3: Repositories (*Repository.cs, I*Repository.cs)
       └─ Layer 4: Data (DbContext, Entities)
       
       Scanning 847 types for violations...
       
       ❌ Violations Found (12):
       
       Layer Skip (Controller → Repository):
       ├─ WorkflowController.cs:145 — directly calls _workflowRepository.GetById()
       │   └─ Should go through IWorkflowService
       └─ ReportController.cs:89 — directly queries _context.Reports
           └─ Should go through IReportRepository
       
       Reverse Dependency (Repository → Service):
       └─ OrderRepository.cs:234 — calls _pricingService.Calculate()
           └─ Business logic should not be in repository
       
       Cross-Cutting Concern in Wrong Layer:
       └─ CustomerService.cs:67 — directly uses HttpContext
           └─ Controller concern leaked into service
       
       ✅ Compliant: 835 types (98.6%)
       
       Would you like me to:
       [1] Generate refactoring plan for violations
       [2] Add [LayerBoundary] attributes to enforce at compile time
       [3] Create architecture diagram"
```

---

### Demo 5.3: The API Surface Analyzer

**Setup:** Library project consumed by external teams

**Script:**

```
You: "What's our public API surface? What would break if we made internal changes?"

Aura: "Analyzing public API surface of Aura.Foundation...
       
       Public API Summary:
       ├─ 45 public types
       ├─ 234 public methods
       ├─ 89 public properties
       └─ 12 public events
       
       Stability Analysis:
       
       🔴 High Risk (many external dependencies):
       ├─ IRagService — 8 methods, used by 5 external projects
       ├─ AgentContext — 12 properties, serialized to JSON
       └─ IAgent — 3 methods, 7 implementations outside this solution
       
       🟡 Medium Risk (some external usage):
       └─ [12 types...]
       
       🟢 Low Risk (internal use only, safe to change):
       └─ [28 types marked 'public' but only used internally]
       
       Recommendations:
       ├─ Consider making 28 low-risk types 'internal'
       ├─ Add [PublicAPI] attribute to intentionally public types
       └─ AgentContext: Create AgentContextV2 for breaking changes
       
       Would you like to:
       [1] Generate API documentation
       [2] Mark internal-only types as internal
       [3] Create API compatibility report for versioning"
```

---

### Demo 5.4: The Microservice Extractor

**Setup:** Monolith ready to split

**Script:**

```
You: "If I wanted to extract the 'Workflow' functionality into a separate microservice, 
      what would need to move and what would break?"

Aura: "Analyzing 'Workflow' boundary...
       
       Identified Workflow-related types:
       ├─ Entities: Workflow, WorkflowStep, WorkflowStatus (3)
       ├─ Services: WorkflowService, WorkflowOrchestrator (2)
       ├─ Repositories: IWorkflowRepository, WorkflowRepository (2)
       ├─ Controllers: WorkflowController, WorkflowStepsController (2)
       └─ DTOs: WorkflowDto, CreateWorkflowRequest, etc. (8)
       
       Total: 17 types, 4,234 lines of code
       
       Dependency Analysis:
       
       📥 Incoming (other code depends on Workflow):
       ├─ AgentOrchestrator → WorkflowService.GetCurrentStep() — 12 calls
       ├─ DashboardController → WorkflowService.GetStats() — 3 calls
       └─ ReportService → Workflow entities — 8 references
       
       📤 Outgoing (Workflow depends on other code):
       ├─ Workflow → IAgentRegistry — for agent lookup
       ├─ WorkflowService → IRagService — for context retrieval
       └─ WorkflowRepository → AuraDbContext — for persistence
       
       Extraction Plan:
       
       1. New Aura.Workflow.Contracts project:
          └─ IWorkflowService interface, DTOs, events
       
       2. New Aura.Workflow.Service project:
          └─ WorkflowService, WorkflowOrchestrator, Repository
       
       3. Integration changes:
          ├─ Replace direct calls with HTTP/gRPC client
          ├─ Publish WorkflowCompleted events instead of direct calls
          └─ Inject IWorkflowClient instead of IWorkflowService
       
       Estimated effort:
       ├─ Code moves: 17 types, ~2 hours
       ├─ Integration refactoring: 23 call sites, ~4 hours
       └─ Testing: Update 45 tests, ~3 hours
       
       Would you like to:
       [1] Create the new project structure
       [2] See detailed migration steps
       [3] Identify additional boundaries (Agent, Rag, etc.)"
```

**Why this is transformative:**
- Microservice extraction is a multi-week project manually
- Aura provides complete impact analysis in seconds
- Surfaces hidden dependencies humans miss
- Generates actual migration plan, not just analysis

---

## Presentation Formats

### Format A: Live Coding (Best for Developers)

```
Duration: 20-30 minutes
Setup: VS Code with Aura extension, real codebase
Flow:
  1. Quick context on the codebase (2 min)
  2. Demo 1.2: Impossible Refactor (5 min) — the hook
  3. Demo 2.1: Interface Extraction (5 min) — practical value
  4. Demo 1.3: Pattern Police (8 min) — architectural value
  5. Q&A with live requests from audience (10 min)
```

**Tips:**

- Have the codebase pre-loaded
- Use a visible terminal or Copilot Chat for commands
- Show git diff after each operation
- Have "undo" ready if something goes wrong

---

### Format B: Recorded Demo (Best for Marketing)

```
Duration: 3-5 minutes (trailer), 15 minutes (full)
Style: Screen recording with voiceover
Flow:
  1. "The Problem" — show manual refactoring pain (30 sec)
  2. "The Solution" — single Aura command (30 sec)
  3. "The Proof" — git diff, compilation, tests (30 sec)
  4. Repeat for 2-3 scenarios
  5. Call to action
```

**Tips:**

- Edit out any delays
- Add visual callouts for key moments
- Show the numbers: "58 changes in 1.2 seconds"

---

### Format C: Comparison Demo (Best for Skeptics)

```
Duration: 15 minutes
Flow:
  1. Split screen: Manual vs Aura
  2. Same task: "Add parameter to interface method"
  3. Left side: Developer doing it manually (fast-forward)
  4. Right side: Aura doing it in real-time
  5. Compare:
     - Time: 15 minutes vs 3 seconds
     - Errors: 2 missed call sites vs 0
     - Verification: Manual testing vs compile check
```

---

### Format D: Progressive Complexity (Best for Training)

```
Duration: 45-60 minutes (workshop)
Flow:
  Level 1: Simple rename (trust building)
  Level 2: Interface extraction (productivity)
  Level 3: Signature change with caller updates (power)
  Level 4: Cross-cutting pattern enforcement (architecture)
  Level 5: Custom audit scenario from audience
```

---

## Demo Environment Setup

### Recommended Codebase

Use **Aura itself** as the demo codebase:

- Familiar to you
- Real complexity (not toy example)
- Multiple projects, interfaces, patterns
- Good size (not too big, not too small)

### Pre-Demo Checklist

```
□ Solution builds and all tests pass
□ Git working tree is clean (for clear diffs)
□ API server running (if needed for MCP)
□ Terminal visible for tool output
□ VS Code Aura extension active
□ Backup branch in case demo goes sideways
□ Know your "escape hatches" (git reset --hard)
```

### Suggested Demo Targets in Aura

Based on analysis of the actual codebase:

#### Best Targets for Rename Demo

| Target | Location | Callers | Why It's Good |
|--------|----------|---------|---------------|
| `IRagService.QueryAsync` | [IRagService.cs](../src/Aura.Foundation/Rag/IRagService.cs) | 15+ | Core method, used across projects |
| `IWorkflowService.GetByIdAsync` | [IWorkflowService.cs](../src/Aura.Module.Developer/Services/IWorkflowService.cs) | 10+ | Interface + implementation + API endpoints |
| `IAgent.ExecuteAsync` | [IAgent.cs](../src/Aura.Foundation/Agents/IAgent.cs) | 20+ | 7 implementations, many callers |

#### Best Targets for Add Parameter Demo

| Target | Suggested Change | Impact |
|--------|------------------|--------|
| `IRagService.QueryAsync` | Add `bool includeMetadata = false` | Cascades to RagService, all API endpoints |
| `IAgent.ExecuteAsync` | Add `IProgress<AgentProgress>? progress = null` | All 7 agent implementations must update |
| `IWorkflowService.CreateAsync` | Add `string? issueUrl = null` | Service + API + tests |

#### Best Targets for Pattern Audit Demo

| Interface | Implementations | Audit Focus |
|-----------|-----------------|-------------|
| `IAgent` | 7 classes | All should have `AgentId`, `Metadata`, proper `ExecuteAsync` signature |
| - CSharpIngesterAgent | [CSharpIngesterAgent.cs](../src/Aura.Module.Developer/Agents/CSharpIngesterAgent.cs) | |
| - TreeSitterIngesterAgent | [TreeSitterIngesterAgent.cs](../src/Aura.Module.Developer/Agents/TreeSitterIngesterAgent.cs) | |
| - RoslynCodingAgent | [RoslynCodingAgent.cs](../src/Aura.Module.Developer/Agents/RoslynCodingAgent.cs) | |
| - ConfigurableAgent | [ConfigurableAgent.cs](../src/Aura.Foundation/Agents/ConfigurableAgent.cs) | |
| - FallbackIngesterAgent | [FallbackIngesterAgent.cs](../src/Aura.Foundation/Agents/FallbackIngesterAgent.cs) | |
| - TextIngesterAgent | [TextIngesterAgent.cs](../src/Aura.Foundation/Agents/TextIngesterAgent.cs) | |
| - LanguageSpecialistAgent | [LanguageSpecialistAgent.cs](../src/Aura.Module.Developer/Agents/LanguageSpecialistAgent.cs) | |

#### Best Targets for Interface Extraction Demo

| Class | Public Members | Why Extract |
|-------|----------------|-------------|
| `RagService` | 10+ methods | Already has interface, but good for showing the process |
| `WorkflowService` | 15+ methods | Already has interface, realistic DI scenario |
| `CodebaseContextService` | 5+ methods | Good candidate if no interface exists |

#### Best Targets for Expression Tree Demo

| Property | Used In | Expression Tree Risk |
|----------|---------|---------------------|
| `Workflow.Status` | EF Core queries, LINQ | `.Where(w => w.Status == ...)` |
| `RagChunk.SourcePath` | EF Core queries | `.Where(c => c.SourcePath.Contains(...))` |
| `CodeNode.FullName` | EF Core queries | Indexed property, used in search |

---

## Objection Handling

| Objection | Response |
|-----------|----------|
| "IDE already does this" | "Yes, for a single developer. This is IDE-level refactoring exposed to AI agents — enabling automation at scale." |
| "What about other languages?" | "Starting with C#/Roslyn because it's the most precise. TypeScript via TS Compiler API is feasible. The architecture is extensible." |
| "What if it makes a mistake?" | "Every change validates compilation. If it breaks, it rolls back. And preview mode lets you see changes before applying." |
| "How is this different from Copilot?" | "Copilot suggests code. This *modifies code* — safely, across your entire codebase, with guarantees." |

---

## Success Metrics

| Metric | Target |
|--------|--------|
| "Wow" reaction | At least 1 audible reaction per demo |
| Questions asked | 3+ questions = engagement |
| Follow-up requests | "Can it also do X?" = they're sold |
| Immediate ask | "When can I try this?" = success |

---

## Appendix: Tool Requirements

For the full technical specification of the MCP tools needed to enable these demos, see the feature spec in `.project/features/upcoming/roslyn-editing-tools.md`.

---

## Appendix: Recommended Demo Script

### The "Signature Change" Demo (10 minutes)

This is the single most impressive demo. Use this for first impressions.

#### Setup (before demo)

```powershell
# Ensure clean state
git checkout main
git pull
dotnet build

# Verify the target exists
grep -n "ExecuteAsync" src/Aura.Foundation/Agents/IAgent.cs
```

#### Script

**[0:00] The Setup**

> "I have an interface `IAgent` with an `ExecuteAsync` method. It's implemented by 7 different agent classes. I want to add a progress reporting parameter so the UI can show what the agent is doing."

*Show [IAgent.cs](../src/Aura.Foundation/Agents/IAgent.cs) — the interface with `ExecuteAsync`*

**[1:00] The Problem**

> "Normally, adding a parameter to an interface method is painful. You have to update the interface, find every implementation, update each one, then find every caller and update those too. Miss one and you get a runtime error."

*Quick scroll through implementations to show the scope*

**[2:00] The Solution**

```
"Add an optional progress parameter to IAgent.ExecuteAsync: 
 IProgress<string>? progress = null"
```

**[2:30] Aura Analyzes**

```
Aura: "Analyzing IAgent.ExecuteAsync...

       Found:
       ├─ 1 interface definition (IAgent.cs:28)
       ├─ 7 implementations:
       │   ├─ CSharpIngesterAgent.ExecuteAsync
       │   ├─ TreeSitterIngesterAgent.ExecuteAsync
       │   ├─ RoslynCodingAgent.ExecuteAsync
       │   ├─ ConfigurableAgent.ExecuteAsync
       │   ├─ FallbackIngesterAgent.ExecuteAsync
       │   ├─ TextIngesterAgent.ExecuteAsync
       │   └─ LanguageSpecialistAgent.ExecuteAsync
       └─ 23 call sites across 8 files
       
       Proceed with adding 'IProgress<string>? progress = null'? [Y/n]"
```

> "Notice it found ALL 7 implementations automatically. Let's do it."

**[3:30] Aura Applies**

```
Aura: "Applied changes:
       ├─ Updated interface signature
       ├─ Updated 7 implementation signatures
       ├─ 0 caller changes needed (parameter has default value)
       └─ Compilation verified ✅
       
       Time: 1.4 seconds"
```

**[4:00] The Proof**

```powershell
# Show the git diff
git diff --stat
# Output: 8 files changed, 16 insertions(+), 8 deletions(-)

# Show actual changes
git diff src/Aura.Foundation/Agents/IAgent.cs
```

> "Every implementation updated with the exact same signature. Compilation passes. Tests pass."

**[5:00] The Comparison**

> "How long would this take manually? Let's count:
> - Find the interface (1 minute)
> - Update it (30 seconds)  
> - Find each implementation (grep, 2 minutes)
> - Update each of 7 implementations (7 × 30 seconds = 3.5 minutes)
> - Find callers (2 minutes)
> - Build to check (30 seconds)
> - Fix the ones you missed (? minutes)
> 
> Total: 10-15 minutes, with risk of missing something.
> 
> Aura: 1.4 seconds, zero risk."

**[6:00] Go Further**

> "Now let's say we actually want to USE this progress parameter. The agents should report their progress."

```
"In each IAgent implementation, add a call to progress?.Report() 
 at the start of ExecuteAsync with the agent's name"
```

```
Aura: "Modified 7 implementations:
       ├─ CSharpIngesterAgent: Added progress?.Report("CSharpIngesterAgent starting...")
       ├─ TreeSitterIngesterAgent: Added progress?.Report("TreeSitterIngesterAgent starting...")
       └─ [5 more...]
       
       Compilation verified ✅"
```

> "That's a cross-cutting change applied to every implementation with consistent formatting."

**[7:00] Wrap Up**

> "This is what Roslyn-powered refactoring gives you:
> 1. Complete coverage — no implementations missed
> 2. Guaranteed correctness — compilation verified
> 3. Speed — seconds instead of minutes
> 4. Safety — atomic changes, can roll back
> 
> And this is exposed to AI agents, so you can express intent in natural language and get precise code changes."

---

### Minimum Tools for Demo

| Tool | Required For |
|------|--------------|
| `aura_rename_symbol` | Demo 1.1, 4.1, 4.2 |
| `aura_change_method_signature` | Demo 1.2, 1.3, 2.3 |
| `aura_implement_interface` | Demo 1.3, 2.1 |
| `aura_generate_constructor` | Demo 2.2 |
| `aura_find_implementations` | Demo 1.3, 3.2 |
| `aura_find_callers` | Demo 1.2, 2.3, 3.2 |
| `aura_validate_compilation` | All demos (verification) |

### Nice-to-Have Tools

| Tool | Enables |
|------|---------|
| `aura_extract_interface` | Demo 2.1 enhanced |
| `aura_safe_delete` | Demo 2.3 enhanced |
| `aura_move_type` | Demo 3.3, 5.1, 5.4 |
| `aura_find_unused_code` | Demo 2.3 dedicated |

### Architectural Intelligence Tools (Tier 5)

| Tool | Enables | Complexity |
|------|---------|------------|
| `aura_analyze_dependencies` | Demo 5.1, 5.4 | High — requires full project graph |
| `aura_find_circular_dependencies` | Demo 5.1 | Medium — transitive reference analysis |
| `aura_verify_layer_architecture` | Demo 5.2 | Medium — configurable layer rules |
| `aura_analyze_public_api` | Demo 5.3 | Medium — public surface enumeration |
| `aura_suggest_module_boundaries` | Demo 5.4 | High — cohesion/coupling analysis |
| `aura_extract_to_project` | Demo 5.1, 5.4 | High — multi-file coordinated move |

---

## Appendix: Quick One-Liners

For rapid-fire demos or social media clips, use these single-command showcases:

### 30-Second Demos

| Demo | Command | Visual Result |
|------|---------|---------------|
| **Rename** | "Rename `GetStats` to `GetStatisticsAsync`" | "Updated 24 references in 1.1s ✅" |
| **Add Property** | "Add `CreatedAt` DateTime property to `Workflow`" | Property added with XML doc |
| **Implement Interface** | "Make `MockRagService` implement `IRagService`" | 10 method stubs generated |
| **Find Unused** | "What public methods in `RagService` are never called?" | List of dead code |
| **Audit Pattern** | "Do all `IAgent` implementations have XML docs?" | Compliance report |

### Tweetable Results

```
Before: "Add CancellationToken to ExecuteAsync"
After:  "Updated 1 interface, 7 implementations, verified ✅"
Time:   1.4 seconds

Before: "Rename property across solution"
After:  "58 references updated, 0 errors"
Time:   0.9 seconds

Before: "Find all callers of DeleteWorkflow"
After:  "Found 12 callers across 6 files with full call paths"
Time:   0.3 seconds
```

### Demo GIF Scripts

**GIF 1: The Cascade (5 seconds)**
1. Show interface with method
2. Type rename command
3. Show "47 references updated ✅"
4. Flash git diff showing multiple files

**GIF 2: The Impossible (8 seconds)**
1. Show "7 implementations found"
2. "Adding parameter..."
3. Show all 7 files updating simultaneously
4. "Compilation verified ✅"

**GIF 3: The Audit (6 seconds)**
1. "Audit IAgent implementations"
2. Show checklist appearing
3. ❌ marks on violations
4. "Fix all?" → "Fixed ✅"
