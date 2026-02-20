# Python Inspect & Validate — Completing Polyglot Parity

**Status:** 📋 Proposed
**Created:** 2026-02-06
**Priority:** Medium
**Estimated Effort:** 3-4 days (2 phases)
**Dependencies:** Polyglot MCP Tools (completed 2026-02-06), Python Refactoring (completed 2026-01-30)

## Overview

Extend `aura_inspect` and `aura_validate` to support Python projects. Today, Python has refactoring (`rename`, `extract_method`, `extract_variable`) and basic navigation (`references`, `definition`) via the `rope` library, but agents cannot inspect type structure or validate compilation/tests in Python projects.

**Goal:** An agent working in a Python project can use `aura_inspect` and `aura_validate` with the same UX as C# and TypeScript.

## Motivation

1. **Parity gap** — TypeScript now has full tool coverage. Python is the #1 language on GitHub but gets a degraded Aura experience.
2. **Agent quality** — Agents editing Python code can't verify their changes compile or pass tests without falling back to `run_in_terminal`.
3. **Dogfooding** — Aura has Python scripts (`scripts/python/refactor.py`) and sample projects (`samples/python/`) that would benefit.

## Current State

### Python Tools Today

| Tool | Operation | Status | Implementation |
|------|-----------|--------|----------------|
| `aura_refactor` | `rename` | ✅ | rope via `refactor.py` |
| `aura_refactor` | `extract_method` | ✅ | rope via `refactor.py` |
| `aura_refactor` | `extract_variable` | ✅ | rope via `refactor.py` |
| `aura_navigate` | `references` | ✅ | rope via `refactor.py` |
| `aura_navigate` | `definition` | ✅ | rope via `refactor.py` |
| `aura_navigate` | `callers` | ❌ | Not implemented |
| `aura_navigate` | `implementations` | ❌ | Not applicable (duck typing) |
| `aura_inspect` | `type_members` | ❌ | Falls through to C# Roslyn path |
| `aura_inspect` | `list_types` | ❌ | Falls through to C# Roslyn path |
| `aura_validate` | `compilation` | ❌ | Not implemented |
| `aura_validate` | `tests` | ❌ | Not implemented |

### Existing Infrastructure

- **Python CLI script:** `scripts/python/refactor.py` (410 lines, 5 commands via `rope`)
- **C# service:** `IPythonRefactoringService` + `PythonRefactoringService` (shells out to `python refactor.py`)
- **Dependencies:** `rope>=1.11.0` (already in `requirements.txt`)
- **Language detection:** `DetectLanguageFromArgs` already detects Python from `projectPath` (checks for `pyproject.toml`, `setup.py`, `requirements.txt`) and from `.py` file extensions

## Design

### Phase 1: Inspect for Python

**Approach:** Add `inspect-type` and `list-types` commands to `refactor.py` using Python's `ast` module (stdlib — no new dependencies needed). The `rope` library is focused on refactoring; Python's built-in `ast` module is better suited for type inspection.

#### Why `ast` instead of `rope`?

- `ast` is stdlib — zero additional dependencies
- `ast` gives clean access to class members, function signatures, decorators, type annotations
- `rope` is optimized for refactoring, not inspection
- `ast` handles Python 3.8+ syntax including dataclasses, protocols, TypedDict, etc.

#### `inspect-type` command

```bash
python refactor.py inspect-type --project /path --type-name MyClass [--file src/module.py]
```

Returns:
```json
{
  "success": true,
  "typeName": "MyClass",
  "kind": "class",
  "filePath": "src/models.py",
  "line": 15,
  "bases": ["BaseModel"],
  "decorators": ["dataclass"],
  "members": [
    { "name": "__init__", "kind": "method", "line": 20, "parameters": "self, name: str, age: int", "returnType": "None" },
    { "name": "full_name", "kind": "property", "line": 30, "type": "str" },
    { "name": "validate", "kind": "method", "line": 35, "parameters": "self", "returnType": "bool", "isStatic": false, "isAsync": false },
    { "name": "MAX_AGE", "kind": "variable", "line": 16, "type": "int" }
  ]
}
```

#### `list-types` command

```bash
python refactor.py list-types --project /path [--name-filter Model]
```

Returns:
```json
{
  "success": true,
  "types": [
    { "name": "UserModel", "kind": "class", "filePath": "src/models.py", "line": 10, "memberCount": 5 },
    { "name": "UserProtocol", "kind": "protocol", "filePath": "src/protocols.py", "line": 3, "memberCount": 2 },
    { "name": "Status", "kind": "enum", "filePath": "src/enums.py", "line": 8, "memberCount": 4 }
  ],
  "count": 3
}
```

**Type kinds to detect:**
- `class` — regular classes, dataclasses
- `protocol` — `typing.Protocol` subclasses
- `enum` — `enum.Enum` subclasses
- `typeddict` — `TypedDict` subclasses
- `namedtuple` — `NamedTuple` subclasses
- `function` — top-level functions (when listed)

#### C# DTOs

```csharp
// Request/Response records in IPythonRefactoringService.cs
public record PythonInspectTypeRequest { ProjectPath, TypeName, FilePath? }
public record PythonInspectTypeResult { Success, Error?, TypeName?, Kind?, FilePath?, Line?, Bases?, Decorators?, Members? }
public record PythonMemberInfo { Name, Kind, Type?, Line, Parameters?, ReturnType?, IsStatic, IsAsync }

public record PythonListTypesRequest { ProjectPath, NameFilter? }
public record PythonListTypesResult { Success, Error?, Types?, Count }
public record PythonTypeInfo { Name, Kind, FilePath, Line, MemberCount }
```

#### Routing

In `McpHandler.Inspect.cs`, update the switch:
```csharp
("type_members", "python") => await PythonInspectTypeAsync(args, ct),
("list_types", "python") => await PythonListTypesAsync(args, ct),
```

### Phase 2: Validate for Python

#### Compilation: type checking with `mypy` or `pyright`

**Approach:** Similar to TypeScript — detect the type checker from project configuration:
1. Check for `mypy.ini`, `.mypy.ini`, or `[tool.mypy]` in `pyproject.toml` → run `mypy --output-format=json`
2. Check for `pyrightconfig.json` or `[tool.pyright]` in `pyproject.toml` → run `pyright --outputjson`
3. If neither → run `python -m py_compile` on individual files (basic syntax check only)

**Note:** Unlike TypeScript where `tsc` is always available (it's a dev dependency), Python projects vary widely in type checker usage. The fallback to `py_compile` ensures we always return *something* useful even without a type checker.

This detection and execution happens **directly in `McpHandler.Validate.cs`** (not via `refactor.py`), following the same pattern as TypeScript test runner detection.

#### Tests: detect and run pytest/unittest

**Approach:** Detect test runner from project configuration:
1. Check for `pytest` in dependencies (pyproject.toml, requirements.txt, setup.cfg) → run `python -m pytest --tb=short -q --json-report` (requires `pytest-json-report`)
2. Fallback: `python -m pytest --tb=short -q` and parse text output
3. If no pytest → `python -m unittest discover -v` and parse text output

Return structured results matching the `aura_validate(tests)` shape: passed, failed, skipped, total.

#### Routing

Update `DetectLanguageForValidation` in `McpHandler.Validate.cs`:
```csharp
// Currently: projectPath (without solutionPath) → typescript
// After: check for Python markers before defaulting to typescript
if (args.Value.TryGetProperty("projectPath", out var ppEl))
{
    var pp = ppEl.GetString();
    if (!string.IsNullOrEmpty(pp))
    {
        // Check for Python project markers
        if (File.Exists(Path.Combine(pp, "pyproject.toml")) ||
            File.Exists(Path.Combine(pp, "setup.py")) ||
            File.Exists(Path.Combine(pp, "requirements.txt")))
            return "python";

        return "typescript"; // default for non-C# projects
    }
}
```

Add cases to the validate switch:
```csharp
("compilation", "python") => await PythonCheckAsync(args, ct),
("tests", "python") => await PythonRunTestsAsync(args, ct),
```

## What We Will NOT Build

| Capability | Why Not |
|-----------|---------|
| `aura_navigate(callers)` for Python | Duck typing makes static call graph analysis unreliable. `rope` doesn't support this well. |
| `aura_navigate(implementations)` for Python | No interfaces in Python (protocols are structural). Not meaningful. |
| `aura_generate` for Python | Python code generation doesn't need AST tools. LLM + `aura_edit` suffices. |

## File Change Inventory

| File | Change |
|------|--------|
| `scripts/python/refactor.py` | Add `inspect-type` and `list-types` commands (~150 lines) |
| `scripts/python/requirements.txt` | No change (uses stdlib `ast`) |
| `src/Aura.Module.Developer/Services/IPythonRefactoringService.cs` | Add `InspectTypeAsync`, `ListTypesAsync`, DTOs |
| `src/Aura.Module.Developer/Services/PythonRefactoringService.cs` | Implement new methods |
| `src/Aura.Api/Mcp/McpHandler.Inspect.cs` | Add Python routing |
| `src/Aura.Api/Mcp/McpHandler.Validate.cs` | Add Python routing + type checker/test runner detection |
| `src/Aura.Api/Mcp/McpHandler.Languages.cs` | Add `PythonInspectTypeAsync`, `PythonListTypesAsync`, `PythonCheckAsync`, `PythonRunTestsAsync` |
| `tests/Aura.Api.Tests/Mcp/McpHandlerPythonTests.cs` | New test file or extend existing |
| `.github/copilot-instructions.md` | Update aura_inspect and aura_validate to show Python support |

## Success Criteria

1. `aura_inspect(operation: "type_members", projectPath: "/my/python/project", typeName: "MyClass")` returns class members with types
2. `aura_inspect(operation: "list_types", projectPath: "/my/python/project")` returns all classes/protocols/enums
3. `aura_validate(operation: "compilation", projectPath: "/my/python/project")` runs mypy/pyright/py_compile and returns diagnostics
4. `aura_validate(operation: "tests", projectPath: "/my/python/project")` runs pytest/unittest and returns structured results
5. All existing C# and TypeScript operations continue to work unchanged
6. All tests pass (existing + new)

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Python not installed on user machine | Clear error: "Python 3 is required for Python operations" |
| `rope` not installed | Already handled — `refactor.py` returns clear error |
| `mypy`/`pyright` not installed | Fallback to `py_compile` for basic syntax checking |
| `pytest-json-report` not installed | Fallback to text parsing of `pytest -q` output |
| Python's dynamic nature limits inspect accuracy | `ast` gives static structure; document that runtime-generated members won't appear |
| Virtual environments | Run commands from `projectPath`; respect `VIRTUAL_ENV` if set |

## Phased Delivery

### Phase 1: Inspect (2 days)
1. Add `inspect-type` and `list-types` to `refactor.py` using `ast`
2. Add C# DTOs + interface methods + service implementation
3. Add routing in `McpHandler.Inspect.cs`
4. Add handler methods in `McpHandler.Languages.cs`
5. Add tests

### Phase 2: Validate (1-2 days)
1. Add type checker detection + execution in `McpHandler.Validate.cs`
2. Add test runner detection + execution
3. Update `DetectLanguageForValidation` for Python markers
4. Add handler methods
5. Add tests
6. Update copilot-instructions.md
