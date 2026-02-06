---
title: "ADR-001: Testing Strategy and TDD Approach"
status: "Accepted"
date: "2026-01-30"
authors: "Anvil Team"
tags: ["testing", "tdd", "architecture", "quality"]
supersedes: ""
superseded_by: ""
---

# ADR-001: Testing Strategy and TDD Approach

## Status

Accepted

## Context

Anvil is a test harness for the Aura system (REST API, VS Code Extension, shell commands). We need a consistent testing approach for Anvil's own code to ensure quality, maintainability, and alignment with the Aura monorepo patterns.

Engineers need clear guidance on:

- How to write tests that serve as documentation
- How to isolate tests from external dependencies (Aura API, file system)
- How to follow TDD discipline (red-green-refactor cycle)
- How to structure test documentation for maximum value

Without standardized testing practices, we risk inconsistent test quality, flaky tests due to service dependencies, and tests that fail to communicate their purpose.

## Decision

We adopt a **Full TDD approach** with the following mandatory practices:

1. **Red-Green-Refactor Cycle**: Write failing tests first, then implement code to make them pass
2. **Test Isolation**: Tests MUST use fakes (preferred) or mocks—never real network calls or Aura service dependencies
3. **Test Doc Format**: Every test MUST include a 5-field documentation comment
4. **Single Responsibility**: Test one behavior per test case
5. **Aura Stack Consistency**: Use xUnit + FluentAssertions + NSubstitute to match the Aura monorepo

### Test Doc Format (5 Required Fields)

```csharp
[Fact]
public void Should_DoSomething_When_ConditionIsMet()
{
    // Test Doc:
    // - Why: [Business/regression reason for test existence]
    // - Contract: [Plain-English invariants being asserted]
    // - Usage Notes: [How to call the API, gotchas to avoid]
    // - Quality Contribution: [What failures this test catches]
    // - Worked Example: [Concrete inputs/outputs]

    // Arrange
    var fake = new FakeAuraClient();
    var sut = new StoryRunner(fake);

    // Act
    var result = sut.Execute(storyId);

    // Assert
    result.Should().BeSuccessful();
}
```

### Test Stack

| Component | Choice | Rationale |
|-----------|--------|-----------|
| **Framework** | xUnit | Consistent with Aura monorepo |
| **Assertions** | FluentAssertions | Readable, expressive assertions |
| **Mocking** | NSubstitute | When fakes are impractical |
| **Fakes** | Hand-rolled | Preferred over mocks for richer behavior |

## Consequences

**Positive**
- **POS-001**: Tests serve as living documentation with clear purpose and examples
- **POS-002**: Isolated tests run fast and deterministically (no flaky service failures)
- **POS-003**: TDD discipline catches design issues early before implementation
- **POS-004**: Consistent test structure enables easier code review and onboarding
- **POS-005**: Stack consistency with Aura reduces cognitive load

**Negative**
- **NEG-001**: Requires discipline to write tests first
- **NEG-002**: Creating fakes adds initial development overhead
- **NEG-003**: 5-field Test Doc adds verbosity to test files
- **NEG-004**: May slow initial velocity while adopting new practices

## Alternatives Considered

### Alternative 1: Test-After Development
- **Description**: Write implementation first, then add tests afterward
- **Rejection Reason**: Leads to tests that verify implementation rather than behavior; harder to achieve high coverage; misses design feedback that TDD provides

### Alternative 2: Minimal Test Documentation
- **Description**: Use only test method names for documentation (no Test Doc comments)
- **Rejection Reason**: Method names cannot capture why a test exists, usage gotchas, or worked examples; reduces test value as documentation

### Alternative 3: Direct Service Calls in Tests
- **Description**: Allow tests to make real HTTP calls to Aura API
- **Rejection Reason**: Creates flaky tests dependent on Aura service availability; slower test execution; cannot test error conditions reliably

### Alternative 4: Different Test Stack (MSTest, Moq)
- **Description**: Use MSTest or a different assertion/mocking library
- **Rejection Reason**: Inconsistent with Aura monorepo; increases cognitive load when switching between projects

## Implementation Notes

- **IMP-001**: Prefer fakes over mocks—fakes implement the same interface and provide richer test assertions
- **IMP-002**: Use Arrange-Act-Assert (AAA) pattern within each test for clarity
- **IMP-003**: Run tests with `dotnet test`; all tests must pass before merge
- **IMP-004**: Test projects live in `tests/` directory (e.g., `tests/Anvil.Tests/`)
- **IMP-005**: Fakes live in `tests/Anvil.Tests/Fakes/` directory

## References

- [Aura Testing Patterns](../../README.md) - Monorepo test conventions
- [xUnit Documentation](https://xunit.net/) - Test framework
- [FluentAssertions](https://fluentassertions.com/) - Assertion library
- Test-Driven Development by Kent Beck - TDD principles
