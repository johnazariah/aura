# Architectural Principles

> Language-agnostic structural principles that define how systems are organized. These are about **what** components exist and **how they relate**, not how to write code.

## Layer Architecture

All projects follow **Clean Architecture** with clear separation of concerns:

```
┌─────────────────────────────────────────────────────────┐
│                   Presentation Layer                    │
│         (CLI, Web UI, API Controllers)                  │
│         Handles user/external interaction only          │
└─────────────────────┬───────────────────────────────────┘
                      │ calls
                      ▼
┌─────────────────────────────────────────────────────────┐
│                   Business Layer                        │
│              (Services, Use Cases)                      │
│         Core business logic, orchestration              │
└─────────────────────┬───────────────────────────────────┘
                      │ calls
                      ▼
┌─────────────────────────────────────────────────────────┐
│                 Infrastructure Layer                    │
│              (Adapters, Repositories)                   │
│         External I/O (HTTP, files, databases)           │
└─────────────────────────────────────────────────────────┘
```

### Layer Responsibilities

| Layer | Responsibilities | Forbidden |
|-------|------------------|-----------|
| **Presentation** | Parse input, format output, invoke services | Business logic, direct I/O |
| **Business** | Domain logic, validation, orchestration | Knowing about presentation, doing I/O |
| **Infrastructure** | HTTP calls, file I/O, database queries | Business logic |

### Dependency Direction

```
Presentation → Business → Infrastructure
```

- Higher layers depend on lower layers
- Lower layers **never** depend on higher layers
- Business layer depends on **abstractions**, not concrete implementations

---

## Dependency Inversion

### Principle

High-level modules should not depend on low-level modules. Both should depend on abstractions.

```
┌─────────────┐
│   Service   │ ──depends on──► │ Interface │ ◄──implements── │ Adapter │
└─────────────┘                 └───────────┘                  └─────────┘
```

### Application

- Services define **what they need** (interface/port)
- Adapters implement **how to provide it**
- Wiring happens at the composition root (entry point)

---

## Dependency Injection

### Strategy

Dependencies are **passed in**, not created internally.

| Approach | Description | Used When |
|----------|-------------|-----------|
| **Constructor Injection** | Dependencies passed at construction | Default for all services |
| **Composition Root** | Single place where dependencies are wired | Application entry point |

### Benefits

- Explicit dependencies (visible in constructor)
- Testability (inject fakes)
- Flexibility (swap implementations)

### Anti-patterns

- ❌ Services creating their own dependencies
- ❌ Global singletons accessed directly
- ❌ Service locator pattern

---

## Error Handling Strategy

### Principle: Layer-Appropriate Errors

Each layer handles errors appropriate to its abstraction level:

```
Infrastructure  →  Business  →  Presentation  →  User
      │               │              │
      │               │              └─ User-friendly message
      │               └─ Business exception ("User not found")
      └─ Technical error ("Connection timeout")
```

### Layer Responsibilities

| Layer | Error Behavior |
|-------|----------------|
| **Infrastructure** | Throw/return technical errors with context |
| **Business** | Catch infra errors, translate to business errors |
| **Presentation** | Catch business errors, display user-friendly messages |

### Principles

- Fail fast at boundaries
- Wrap errors with context when crossing layers
- Never expose internal details to users

---

## Security Principles

### Defense in Depth

Multiple layers of security; never rely on a single control.

### Input Validation

| Layer | Validation Type |
|-------|-----------------|
| **Presentation** | Format (types, lengths) |
| **Business** | Rules (permissions, limits) |
| **Infrastructure** | Sanitization (SQL injection, XSS) |

### Secrets Management

- Configuration injected at runtime
- Never in source code or logs
- Environment variables or secret managers

---

## Performance Principles

### Approach

1. **Measure first**: Don't optimize without data
2. **Optimize hot paths**: Focus on frequently executed code
3. **Consider latency budgets**: Know your SLAs

### Common Patterns

| Pattern | Use When |
|---------|----------|
| Caching | Repeated reads of same data |
| Connection pooling | Frequent external calls |
| Async/concurrent | I/O-bound operations |
| Pagination | Large data sets |

---

## Testing Strategy

### Test Pyramid

```
        /\
       /  \      E2E Tests (few)
      /────\     
     /      \    Integration Tests (some)
    /────────\   
   /          \  Unit Tests (many)
  /────────────\ 
```

### Test Isolation

| Test Type | Dependencies | Environment |
|-----------|--------------|-------------|
| **Unit** | Fakes/stubs | In-memory |
| **Integration** | Real (some) | Isolated |
| **E2E** | Real (all) | Production-like |

### Test Doubles Strategy

**Prefer fakes over mocks**: Fakes implement real interfaces, catch more bugs, and serve as documentation.

---

## Summary

Before designing any component:

- [ ] Which layer does it belong to?
- [ ] What are its dependencies?
- [ ] How will dependencies be injected?
- [ ] What errors can it produce/handle?
- [ ] How will it be tested?
