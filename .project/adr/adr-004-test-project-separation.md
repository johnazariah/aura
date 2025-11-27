# ADR-004: Test Project Separation by Execution Profile

## Status
Accepted

## Date
2025-11-27

## Context

As Aura's test suite grows, we need to organize tests by their execution characteristics:

1. **Unit tests** - Fast, isolated, run frequently (every save/build)
2. **API integration tests** - Medium speed, in-memory database, run on pre-push
3. **LLM integration tests** - Slow, require Ollama, run on-demand/nightly
4. **E2E tests** (future) - Slowest, require full infrastructure

These test types have different:
- **Dependencies**: Unit tests need only xUnit; LLM tests need xUnit v3 for `Assert.Skip()`
- **External requirements**: Unit tests are self-contained; integration tests need Ollama
- **Execution time**: Milliseconds vs seconds vs minutes
- **Failure semantics**: Unit test failures block commit; integration failures may indicate infrastructure issues

## Decision

We will separate tests into multiple projects based on execution profile:

```
tests/
├── Aura.Foundation.Tests/     # Unit tests - pure logic, mocked deps
├── Aura.Api.Tests/            # API tests - WebApplicationFactory, in-memory DB
├── Aura.Integration.Tests/    # LLM tests - real Ollama, quality assertions
└── Aura.E2E.Tests/            # Silver threads - full stack (future)
```

### Configuration via Runsettings

```xml
<!-- tests/.runsettings (default - fast feedback) -->
<TestCaseFilter>Category!=Integration&amp;Category!=E2E</TestCaseFilter>

<!-- tests/integration.runsettings (when Ollama available) -->
<TestCaseFilter>Category=Integration</TestCaseFilter>

<!-- tests/all.runsettings (nightly/release) -->
<!-- No filter - runs everything -->
```

### Trait-Based Categorization

```csharp
[Trait("Category", "Unit")]        // Aura.Foundation.Tests
[Trait("Category", "Api")]         // Aura.Api.Tests (implicit - no trait needed)
[Trait("Category", "Integration")] // Aura.Integration.Tests
[Trait("Category", "E2E")]         // Aura.E2E.Tests
```

## Consequences

### Positive

1. **Dependency isolation** - Different xUnit versions (v2 vs v3) don't conflict
2. **Clear execution semantics** - `dotnet test tests/Aura.Foundation.Tests` is unambiguous
3. **CI/CD optimization** - Can parallelize test jobs with different runners
4. **Skip logic isolation** - Only integration tests need graceful skip behavior
5. **Build time** - Can rebuild/test individual projects independently

### Negative

1. **More projects to maintain** - 4 test projects instead of 1
2. **Potential duplication** - Helper code might be duplicated across projects
3. **Solution complexity** - More entries in Aura.sln

### Mitigations

- Create `Aura.Testing.Common` if helpers need sharing (not yet needed)
- Use consistent naming conventions across projects
- Document project purposes in each .csproj file

## Alternatives Considered

### Single Test Project with Categories

```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task MyIntegrationTest() { ... }
```

**Rejected because:**
- Can't use different xUnit versions
- All dependencies pulled into one project
- Less clear project structure

### Test per Assembly

One test project per production assembly (e.g., `Aura.Foundation.Tests`, `Aura.Api.Tests`).

**Partially adopted:** We do have per-assembly tests, but we also separate by execution profile (integration tests span multiple assemblies but share execution characteristics).

## Related

- Test Strategy: `.project/plan/testing/test-strategy.md`
- Level 1 Tests: `tests/Aura.Api.Tests/`
- Level 2 Tests: `tests/Aura.Integration.Tests/`
