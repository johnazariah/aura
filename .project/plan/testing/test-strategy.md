# Aura Testing Strategy

## Overview

This document defines the three-level testing strategy for Aura, ensuring quality from unit tests through full end-to-end scenarios with real LLM integration.

## Test Levels

### Level 1: API Integration Tests (✅ COMPLETE)

**Purpose**: Verify API endpoints work correctly with isolated, fast tests.

**Characteristics**:
- Uses `WebApplicationFactory` for in-process API testing
- In-memory database (no PostgreSQL required)
- `StubLlmProvider` instead of real Ollama
- Test agents created in temp directories
- Fast execution (~2 seconds for 17 tests)
- Runs on every commit (pre-push hook)

**Location**: `tests/Aura.Api.Tests/`

**Test Coverage**:
- Agent discovery endpoints (`/api/agents`, `/api/agents/best`)
- Capability and language filtering
- Agent execution flow (with stub responses)
- Health check endpoints
- Error handling (404 for missing agents)

**Status**: ✅ 17 tests passing

---

### Level 2: LLM Integration Tests (🔲 TODO)

**Purpose**: Verify agents produce quality outputs with real LLM inference.

**Characteristics**:
- Requires running Ollama instance
- Uses real models (qwen2.5-coder:7b, llama3.2:3b, etc.)
- Tests actual prompt→response quality
- Slower execution (10-60 seconds per test)
- Runs nightly or on-demand, NOT on every commit
- May use PostgreSQL or in-memory database

**Location**: `tests/Aura.Integration.Tests/` (to be created)

**Proposed Test Scenarios**:

1. **Chat Agent Basic Response**
   - Prompt: "What is 2+2?"
   - Assert: Response contains "4"
   - Model: llama3.2:3b

2. **Coding Agent Code Generation**
   - Prompt: "Write a C# function that reverses a string"
   - Assert: Response contains valid C# code with `Reverse` or character manipulation
   - Model: qwen2.5-coder:7b

3. **Issue Enricher Summarization**
   - Input: Multi-paragraph GitHub issue text
   - Assert: Response is shorter than input, contains key terms
   - Capability: Enrichment

4. **Build Fixer Diagnostic Analysis**
   - Input: Compiler error messages (CS0246, CS1061, etc.)
   - Assert: Response identifies missing using statements or typos
   - Capability: fixing

5. **Code Review Feedback**
   - Input: Code snippet with obvious issues (unused variables, magic numbers)
   - Assert: Response mentions specific improvements
   - Capability: review

6. **Documentation Generator**
   - Input: C# class without XML docs
   - Assert: Response contains `<summary>` and `<param>` tags
   - Capability: documentation

**Configuration**:
```json
{
  "Aura:IntegrationTests": {
    "OllamaBaseUrl": "http://localhost:11434",
    "Timeout": 120,
    "Models": {
      "Chat": "llama3.2:3b",
      "Coding": "qwen2.5-coder:7b"
    }
  }
}
```

**Skip Conditions**:
- `[Trait("Category", "Integration")]` - Filtered out by default runsettings
- Skip if Ollama not running (graceful skip, not failure)

---

### Level 3: End-to-End Silver Thread Tests (🔲 TODO)

**Purpose**: Verify complete user scenarios work from VS Code extension through API to LLM and back.

**Characteristics**:
- Full stack: Extension → API → Agents → Ollama → Response
- Requires all infrastructure running (PostgreSQL, Ollama, API)
- Tests real user workflows
- May involve file system operations
- Slowest execution (minutes per scenario)
- Runs nightly or before releases

**Location**: `tests/Aura.E2E.Tests/` (to be created)

**Proposed Silver Thread Scenarios**:

1. **"Hello World" Chat**
   - User opens extension
   - Selects chat agent from tree
   - Sends "Hello, who are you?"
   - Receives coherent response about being Aura
   - Execution is logged to database

2. **Issue Breakdown Workflow**
   - Given: GitHub issue URL in workspace
   - When: User requests issue Enrichment
   - Then: Best Enrichment agent is selected
   - And: Response summarizes the issue
   - And: Execution appears in history

3. **Code Generation with Context**
   - Given: User has a C# project open
   - When: User asks "Add a logging service"
   - Then: Agent receives workspace path context
   - And: Response includes C# code
   - And: Code references project conventions

4. **Agent Failover**
   - Given: Primary model is unavailable
   - When: User sends request
   - Then: Falls back to chat agent
   - And: Response is still generated
   - And: Warning is logged

5. **Multi-Step Workflow** (future)
   - Given: Build error in workspace
   - When: User requests fix
   - Then: Build-fixer agent analyzes error
   - And: Suggests code changes
   - And: User can apply changes
   - And: Workflow state persists

**Infrastructure Requirements**:
```yaml
# docker-compose.e2e.yml
services:
  postgres:
    image: postgres:16
    ports: ["5432:5432"]
  
  ollama:
    image: ollama/ollama
    ports: ["11434:11434"]
    volumes:
      - ollama-models:/root/.ollama
```

**Test Runner**:
- xUnit with custom fixtures for infrastructure setup
- Or: Playwright for extension UI testing (future)
- Health checks before test execution

---

## Test Categories and Filtering

### Category Traits

```csharp
[Trait("Category", "Unit")]        // Fast, isolated, no external deps
[Trait("Category", "Integration")] // Requires Ollama
[Trait("Category", "E2E")]         // Requires full infrastructure
```

### Runsettings Configuration

**Default (pre-push)**: `tests/.runsettings`
```xml
<RunSettings>
  <RunConfiguration>
    <TestCaseFilter>Category!=Integration&amp;Category!=E2E</TestCaseFilter>
  </RunConfiguration>
</RunSettings>
```

**Integration**: `tests/integration.runsettings`
```xml
<RunSettings>
  <RunConfiguration>
    <TestCaseFilter>Category=Integration</TestCaseFilter>
  </RunConfiguration>
</RunSettings>
```

**All**: `tests/all.runsettings`
```xml
<RunSettings>
  <RunConfiguration>
    <!-- No filter - runs everything -->
  </RunConfiguration>
</RunSettings>
```

---

## CI/CD Integration

### Pre-Push Hook (Fast)
```bash
# Runs Level 1 only
dotnet test --settings tests/.runsettings
```

### Pull Request (Medium)
```yaml
# GitHub Actions
- name: Run Unit + API Tests
  run: dotnet test --settings tests/.runsettings

- name: Run Integration Tests (if Ollama available)
  run: |
    if curl -s http://localhost:11434/api/tags; then
      dotnet test --settings tests/integration.runsettings
    fi
```

### Nightly Build (Full)
```yaml
- name: Start Infrastructure
  run: docker-compose -f docker-compose.e2e.yml up -d

- name: Wait for Services
  run: ./scripts/wait-for-services.ps1

- name: Run All Tests
  run: dotnet test --settings tests/all.runsettings

- name: Cleanup
  run: docker-compose -f docker-compose.e2e.yml down
```

---

## Implementation Roadmap

### Phase 1: ✅ Complete
- [x] API Integration Tests (Level 1)
- [x] WebApplicationFactory setup
- [x] Test agent fixtures
- [x] In-memory database isolation

### Phase 2: Next
- [ ] Create `Aura.Integration.Tests` project
- [ ] Add Ollama health check skip logic
- [ ] Implement chat agent quality test
- [ ] Implement coding agent generation test
- [ ] Add `integration.runsettings`
- [ ] Update CI to optionally run integration tests

### Phase 3: Future
- [ ] Create `Aura.E2E.Tests` project
- [ ] Docker Compose for test infrastructure
- [ ] Silver thread: Hello World chat
- [ ] Silver thread: Issue breakdown
- [ ] Infrastructure wait scripts
- [ ] Nightly CI job

---

## Quality Gates

| Gate | Tests Required | When |
|------|---------------|------|
| Pre-push | Level 1 (92 tests) | Every commit |
| PR Merge | Level 1 + Level 2 (if available) | Pull requests |
| Release | All levels | Before tagging |

---

## Appendix: Test Project Structure

```
tests/
├── Aura.Foundation.Tests/        # Unit tests (75 tests)
│   ├── Agents/
│   └── Llm/
├── Aura.Api.Tests/               # Level 1: API integration (17 tests)
│   ├── AuraApiFactory.cs
│   └── Endpoints/
├── Aura.Integration.Tests/       # Level 2: LLM integration (TODO)
│   ├── Fixtures/
│   │   └── OllamaFixture.cs
│   └── Agents/
│       ├── ChatAgentTests.cs
│       ├── CodingAgentTests.cs
│       └── EnrichmentAgentTests.cs
├── Aura.E2E.Tests/               # Level 3: Silver threads (TODO)
│   ├── Infrastructure/
│   │   └── DockerComposeFixture.cs
│   └── Scenarios/
│       ├── HelloWorldChatTests.cs
│       └── IssueBreakdownTests.cs
├── .runsettings                   # Default: Unit + API only
├── integration.runsettings        # Level 2 only
└── all.runsettings                # All levels
```
