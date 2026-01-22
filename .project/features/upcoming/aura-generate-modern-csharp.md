# aura_generate: Modern C# Support

> **Status:** In Progress
> **Created:** 2026-01-23
> **Priority:** Medium
> **Estimated Effort:** 1-2 weeks

## Progress

| Phase | Features | Status |
|-------|----------|--------|
| 1 | `required` properties, `init` setters | ✅ Complete |
| 2 | `virtual`/`override`/`abstract` methods, static methods | ✅ Complete |
| 3 | Primary constructors (C# 12), positional records | ✅ Complete |
| 4 | Generic types and methods with constraints | ✅ Complete |
| 5 | Attributes on members | ✅ Complete |
| 6 | Extension methods, XML docs, record structs | ⏳ Not started |

## Overview

Extend `aura_generate` to support modern C# (12/13) language features. The current implementation covers basic scenarios but lacks support for many commonly-used modern patterns.

## Background

### Current Capabilities

| Operation | What It Does |
|-----------|--------------|
| `create_type` | class, interface, record, struct |
| `property` | Properties and fields with modifiers |
| `method` | Methods with parameters, async, body |
| `constructor` | Constructors with member initialization |
| `implement_interface` | Implement interface members |
| `tests` | Generate unit tests |

### Current Limitations

During an agentic workflow session, the following gaps were identified:

1. Cannot generate `required` properties (C# 11)
2. Cannot generate `init` setters (C# 9)
3. Cannot create primary constructor types (C# 12)
4. Cannot add `virtual`/`override`/`abstract` modifiers
5. Cannot create generic types or methods
6. Cannot add attributes to generated members
7. Cannot generate positional records

## Goals

1. Support commonly-used C# 9-13 features in code generation
2. Maintain backward compatibility with existing parameters
3. Keep the API surface manageable (don't add 50 new parameters)
4. Prioritize features used in everyday development

## Non-Goals

1. 100% C# language coverage (rare features like operators, indexers)
2. Automatic detection of "best" modern syntax
3. Refactoring existing code to use modern syntax

---

## Feature Categories

### Phase 1: Property & Field Modifiers (Priority: High)

These are used constantly in modern C# codebases.

#### 1.1 Required Properties

```csharp
// Target output
public required string Name { get; set; }
```

**API Addition:**
```json
{
  "operation": "property",
  "propertyName": "Name",
  "propertyType": "string",
  "isRequired": true
}
```

#### 1.2 Init-Only Setters

```csharp
// Target output
public string Name { get; init; }
```

**API Addition:**
```json
{
  "operation": "property",
  "propertyName": "Name",
  "propertyType": "string",
  "hasInit": true,  // mutually exclusive with hasSetter
  "hasSetter": false
}
```

#### 1.3 Combined Example

```csharp
// Target output
public required string Name { get; init; }
```

```json
{
  "operation": "property",
  "propertyName": "Name",
  "propertyType": "string",
  "isRequired": true,
  "hasInit": true
}
```

### Phase 2: Method Modifiers (Priority: High)

Essential for inheritance hierarchies.

#### 2.1 Virtual/Override/Abstract Methods

```csharp
// Target outputs
public virtual void Process() { }
public override void Process() { }
public abstract void Process();
```

**API Addition:**
```json
{
  "operation": "method",
  "methodName": "Process",
  "returnType": "void",
  "methodModifier": "virtual"  // "virtual" | "override" | "abstract" | "sealed"
}
```

#### 2.2 Static Methods

Already have `isStatic` for types, extend to methods:

```json
{
  "operation": "method",
  "methodName": "Create",
  "returnType": "MyClass",
  "isStatic": true
}
```

### Phase 3: Primary Constructors (Priority: Medium)

C# 12's most impactful feature for reducing boilerplate.

#### 3.1 Primary Constructor Classes

```csharp
// Target output
public class UserService(ILogger<UserService> logger, IUserRepository repo)
{
    private readonly ILogger<UserService> _logger = logger;
}
```

**API Addition:**
```json
{
  "operation": "create_type",
  "typeName": "UserService",
  "typeKind": "class",
  "primaryConstructor": [
    { "name": "logger", "type": "ILogger<UserService>" },
    { "name": "repo", "type": "IUserRepository" }
  ],
  "captureAsFields": ["logger"]  // Optional: which params to capture as fields
}
```

#### 3.2 Positional Records

```csharp
// Target output
public record Person(string FirstName, string LastName);
```

**API Addition:**
```json
{
  "operation": "create_type",
  "typeName": "Person",
  "typeKind": "record",
  "positionalParameters": [
    { "name": "FirstName", "type": "string" },
    { "name": "LastName", "type": "string" }
  ]
}
```

### Phase 4: Generics (Priority: Medium)

Common need that currently requires manual editing.

#### 4.1 Generic Types

```csharp
// Target output
public class Repository<TEntity> where TEntity : class, IEntity
{
}
```

**API Addition:**
```json
{
  "operation": "create_type",
  "typeName": "Repository",
  "typeKind": "class",
  "typeParameters": [
    {
      "name": "TEntity",
      "constraints": ["class", "IEntity"]
    }
  ]
}
```

#### 4.2 Generic Methods

```csharp
// Target output
public T Create<T>() where T : new()
{
    return new T();
}
```

**API Addition:**
```json
{
  "operation": "method",
  "methodName": "Create",
  "returnType": "T",
  "typeParameters": [
    { "name": "T", "constraints": ["new()"] }
  ]
}
```

### Phase 5: Attributes (Priority: Medium)

Essential for frameworks (ASP.NET, EF Core, xUnit).

#### 5.1 Member Attributes

```csharp
// Target output
[JsonPropertyName("user_name")]
[Required]
public string UserName { get; set; }
```

**API Addition:**
```json
{
  "operation": "property",
  "propertyName": "UserName",
  "propertyType": "string",
  "attributes": [
    { "name": "JsonPropertyName", "arguments": ["\"user_name\""] },
    { "name": "Required" }
  ]
}
```

#### 5.2 Method Attributes

```csharp
// Target output
[HttpGet("{id}")]
[ProducesResponseType(typeof(User), 200)]
public async Task<IActionResult> GetUser(int id)
```

```json
{
  "operation": "method",
  "methodName": "GetUser",
  "attributes": [
    { "name": "HttpGet", "arguments": ["\"{id}\""] },
    { "name": "ProducesResponseType", "arguments": ["typeof(User)", "200"] }
  ]
}
```

### Phase 6: Record Enhancements (Priority: Low)

#### 6.1 Record Structs

```csharp
// Target output
public readonly record struct Point(int X, int Y);
```

**API Addition:**
```json
{
  "operation": "create_type",
  "typeName": "Point",
  "typeKind": "record",
  "isRecordStruct": true,
  "isReadonly": true,
  "positionalParameters": [
    { "name": "X", "type": "int" },
    { "name": "Y", "type": "int" }
  ]
}
```

### Phase 7: Edge Cases (Priority: Low)

#### 7.1 Extension Methods

```csharp
// Target output
public static class StringExtensions
{
    public static bool IsNullOrEmpty(this string? value)
        => string.IsNullOrEmpty(value);
}
```

**API Addition:**
```json
{
  "operation": "method",
  "className": "StringExtensions",
  "methodName": "IsNullOrEmpty",
  "returnType": "bool",
  "isStatic": true,
  "isExtension": true,
  "parameters": [
    { "name": "value", "type": "string?" }
  ]
}
```

#### 7.2 XML Documentation on Members

```csharp
/// <summary>
/// Gets or sets the user's name.
/// </summary>
public string Name { get; set; }
```

**API Addition:**
```json
{
  "operation": "property",
  "propertyName": "Name",
  "propertyType": "string",
  "documentation": "Gets or sets the user's name."
}
```

---

## API Schema Changes

### New Parameters for `aura_generate`

| Parameter | Type | Operations | Description |
|-----------|------|------------|-------------|
| `isRequired` | boolean | property | Add `required` modifier (C# 11) |
| `hasInit` | boolean | property | Use `init` setter instead of `set` (C# 9) |
| `methodModifier` | string | method | `virtual`, `override`, `abstract`, `sealed` |
| `primaryConstructor` | array | create_type | Primary constructor parameters (C# 12) |
| `positionalParameters` | array | create_type | Positional record parameters (C# 9) |
| `typeParameters` | array | create_type, method | Generic type parameters with constraints |
| `attributes` | array | property, method, create_type | Attributes to add |
| `isExtension` | boolean | method | Generate as extension method |
| `documentation` | string | property, method | XML doc summary |
| `captureAsFields` | array | create_type | Primary constructor params to capture as fields |
| `isRecordStruct` | boolean | create_type | Create `record struct` |

### Validation Rules

1. `hasInit` and `hasSetter` are mutually exclusive
2. `methodModifier: "abstract"` requires containing type to be abstract
3. `isExtension` requires `isStatic: true` and class to be static
4. `positionalParameters` only valid for `typeKind: "record"` or `"struct"`
5. `primaryConstructor` not valid for interfaces

---

## Implementation Plan

### Phase 1: Property Modifiers (Day 1-2)

**Files to modify:**
- `src/Aura.Api/Mcp/McpHandler.cs` - Add schema parameters
- `src/Aura.Module.Developer/Services/IRoslynRefactoringService.cs` - Extend `AddPropertyRequest`
- `src/Aura.Module.Developer/Services/RoslynRefactoringService.cs` - Generate modifiers

**Tasks:**
1. Add `isRequired` and `hasInit` to schema
2. Extend `AddPropertyRequest` record
3. Modify `AddPropertyAsync` to emit `required` and `init`
4. Add unit tests

### Phase 2: Method Modifiers (Day 2-3)

**Files to modify:**
- `src/Aura.Api/Mcp/McpHandler.cs`
- `src/Aura.Module.Developer/Services/IRoslynRefactoringService.cs`
- `src/Aura.Module.Developer/Services/RoslynRefactoringService.cs`

**Tasks:**
1. Add `methodModifier` and `isStatic` to method schema
2. Extend `AddMethodRequest` record
3. Modify `AddMethodAsync` to emit modifiers
4. Add unit tests

### Phase 3: Primary Constructors (Day 3-4)

**Files to modify:**
- `src/Aura.Api/Mcp/McpHandler.cs`
- `src/Aura.Module.Developer/Services/IRoslynRefactoringService.cs`
- `src/Aura.Module.Developer/Services/RoslynRefactoringService.cs`

**Tasks:**
1. Add `primaryConstructor` and `positionalParameters` to schema
2. Extend `CreateTypeRequest` record
3. Modify `CreateTypeAsync` to generate primary constructor syntax
4. Add unit tests for classes and records

### Phase 4: Generics (Day 4-5)

**Tasks:**
1. Add `typeParameters` to schema (for types and methods)
2. Generate `<T>` syntax with constraints
3. Handle `where` clauses
4. Add unit tests

### Phase 5: Attributes (Day 5-6)

**Tasks:**
1. Add `attributes` array to schema
2. Generate `[Attribute("args")]` syntax
3. Handle multiple attributes
4. Add unit tests

### Phase 6: Polish & Edge Cases (Day 6-7)

**Tasks:**
1. Extension methods
2. XML documentation on members
3. Record structs
4. Integration testing
5. Update documentation

---

## Testing Strategy

### Unit Tests

| Test | Scenario |
|------|----------|
| `Property_Required_GeneratesRequiredModifier` | `isRequired: true` |
| `Property_Init_GeneratesInitSetter` | `hasInit: true` |
| `Property_RequiredInit_GeneratesBoth` | Combined modifiers |
| `Method_Virtual_GeneratesVirtualModifier` | `methodModifier: "virtual"` |
| `Method_Override_GeneratesOverrideModifier` | `methodModifier: "override"` |
| `Method_Abstract_GeneratesAbstractModifier` | `methodModifier: "abstract"` |
| `CreateType_PrimaryConstructor_GeneratesSyntax` | C# 12 primary constructors |
| `CreateType_PositionalRecord_GeneratesSyntax` | Positional records |
| `CreateType_Generic_GeneratesTypeParameters` | `<T>` syntax |
| `Method_Generic_GeneratesTypeParameters` | Generic methods |
| `Property_Attributes_GeneratesAttributes` | Attribute syntax |
| `Method_Extension_GeneratesThisParameter` | Extension methods |

### Integration Tests

1. Generate class with primary constructor, verify compiles
2. Generate record with positional parameters, verify compiles
3. Generate generic repository class, verify compiles
4. Generate controller with attributes, verify compiles

---

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Schema bloat | Medium | Group related params, use objects for complex types |
| Roslyn API complexity | Medium | Reuse existing patterns, unit test thoroughly |
| Backward compatibility | Low | All new params are optional with sensible defaults |
| Edge case interactions | Medium | Validation rules, clear error messages |

---

## Success Metrics

| Metric | Target |
|--------|--------|
| New parameters added | 10-12 |
| Unit test coverage | 100% for new code paths |
| Breaking changes | 0 |
| Agent adoption | Agents use new features within 1 week |

---

## Out of Scope (Future Work)

These features are rare enough to defer:

1. Events
2. Indexers
3. Operators
4. Finalizers / Dispose pattern
5. Nested types
6. File-scoped types
7. Partial types/methods
8. Ref/out/in parameters
9. Unsafe code
10. Fixed-size buffers

---

## References

- [C# 12 What's New](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-12)
- [C# 11 What's New](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-11)
- [Primary Constructors](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/tutorials/primary-constructors)
- [Required Members](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/required)
