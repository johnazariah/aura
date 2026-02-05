# Handoff: Anvil Core → Implementation

> **Story**: anvil-core  
> **From Stage**: Plan  
> **To Stage**: Implement  
> **Date**: 2026-01-30

## Context

The design phase is complete. Anvil's architecture, components, and interfaces are fully specified. The implementation agent should execute the plan using TDD.

## Required Reading

| Document | Purpose |
|----------|---------|
| [plan-anvil-core-2026-01-30.md](../plans/plan-anvil-core-2026-01-30.md) | Implementation specification |
| [ADRs](../../.sdd/ADR/) | Constraints and decisions |
| [coding-guidelines](../../.sdd/coding-guidelines/csharp.md) | Coding standards |

## Key Decisions Made

| Decision | Choice |
|----------|--------|
| Base URL | `--url` → `AURA_URL` env → `localhost:5300` fallback |
| Scenario discovery | Auto-discover `./scenarios/` from CWD |
| Report output | `./reports/anvil-{timestamp}.json` default |
| Console verbosity | Verbose default, `-q` quiet, `-qq` silent |
| Log verbosity | Always verbose to `./logs/anvil-{timestamp}.log` |

## Implementation Phases

Execute in order, committing after each phase:

### Phase 1: Foundation
- Create `Anvil.Cli.csproj` with dependencies
- Create `Anvil.Cli.Tests.csproj` 
- Create `Anvil.sln` (separate from main Aura.sln)
- Add data models (Scenario, Results, ApiContracts)
- Add exception types

### Phase 2: Scenario Loading
- Interface: `IScenarioLoader`
- Tests: `ScenarioLoaderTests` (write first, should fail)
- Implementation: `ScenarioLoader`

### Phase 3: Aura Client
- Interface: `IAuraClient`
- Fake: `FakeAuraClient`
- Tests: `AuraClientTests`
- Implementation: `AuraClient`

### Phase 4: Story Runner
- Interface: `IExpectationValidator`
- Tests: `ExpectationValidatorTests`
- Implementation: `ExpectationValidator`
- Interface: `IStoryRunner`
- Tests: `StoryRunnerTests`
- Implementation: `StoryRunner`

### Phase 5: Reporting
- Interface: `IReportGenerator`
- Tests: `ReportGeneratorTests`
- Implementation: `ReportGenerator`

### Phase 6: CLI Commands
- `Program.cs` with DI setup
- `HealthCommand`
- `ValidateCommand`
- `RunCommand`

### Phase 7: Sample Scenario
- Create `scenarios/csharp/hello-world.yaml`
- Create `scenarios/README.md`

## TDD Workflow

```
1. Write interface
2. Write test (must fail - red)
3. Write minimal implementation (make it pass - green)
4. Refactor if needed
5. Run all tests: dotnet test
6. Commit with: feat(anvil): <description>
```

## Key Dependencies (NuGet)

```xml
<!-- Anvil.Cli.csproj -->
<PackageReference Include="Spectre.Console" Version="0.49.*" />
<PackageReference Include="Spectre.Console.Cli" Version="0.49.*" />
<PackageReference Include="YamlDotNet" Version="16.*" />
<PackageReference Include="System.IO.Abstractions" Version="21.*" />
<PackageReference Include="Microsoft.Extensions.Http" Version="9.*" />
<PackageReference Include="Microsoft.Extensions.Logging" Version="9.*" />
<PackageReference Include="Microsoft.Extensions.Logging.Console" Version="9.*" />

<!-- Anvil.Cli.Tests.csproj -->
<PackageReference Include="xunit" Version="2.*" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
<PackageReference Include="FluentAssertions" Version="7.*" />
<PackageReference Include="NSubstitute" Version="5.*" />
<PackageReference Include="System.IO.Abstractions.TestingHelpers" Version="21.*" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
```

## Validation Commands

After each phase:
```powershell
cd c:\work\aura-anvil\anvil
dotnet build
dotnet test
```

## Success Criteria

- [ ] All 7 phases complete
- [ ] All tests pass
- [ ] `anvil health` command works
- [ ] `anvil validate scenarios/` works
- [ ] Sample scenario loads successfully

## Constraints

- Follow TDD strictly (tests before implementation)
- Do not deviate from the plan without escalating
- Do not add features not in the plan
- Do not modify anything outside `anvil/` directory
- Commit after each phase with proper message format

## Escalation

If blocked on design questions, escalate to user. Design agent context may be re-engaged.

## Start Command

```powershell
cd c:\work\aura-anvil\anvil
# Begin Phase 1
```
