# Architecture Decision Records (ADRs)

This directory contains the architecture decision records for the Aura project.

## ADR Index

| ADR | Title | Status | Date |
|-----|-------|--------|------|
| [ADR-001](001-local-first-architecture.md) | Local-First, Privacy-Safe Architecture | Accepted | 2025-11-25 |
| [ADR-002](002-greenfield-rewrite.md) | Greenfield Rewrite from hve-hack | Accepted | 2025-11-25 |
| [ADR-003](003-composable-modules.md) | Composable Module System | Accepted | 2025-11-25 |
| [ADR-004](004-markdown-agent-definitions.md) | Markdown Agent Definitions | Accepted | 2025-11-25 |
| [ADR-005](005-aspire-orchestration.md) | .NET Aspire for Development Orchestration | Accepted | 2025-11-26 |
| [ADR-006](006-foundation-vs-module-data.md) | Foundation vs Module Data Separation | Accepted | 2025-11-27 |
| [ADR-007](007-provider-registry-pattern.md) | LLM Provider Registry Pattern | Accepted | 2025-11-26 |
| [ADR-008](008-local-rag-foundation.md) | Local RAG as Foundation Component | Accepted | 2025-11-27 |
| [ADR-009](009-lessons-from-previous-attempts.md) | Lessons Learned from Previous Attempts | Accepted | 2025-11-27 |

## ADR Template

When creating new ADRs, use the following template:

```markdown
# ADR-XXX: [Title]

## Status
[Proposed | Accepted | Rejected | Deprecated | Superseded by ADR-XXX]

## Date
YYYY-MM-DD

## Context
[What is the issue that we're seeing that is motivating this decision or change?]

## Decision
[What is the change that we're proposing or have agreed to implement?]

## Consequences
[What becomes easier or more difficult to do and any risks introduced by this change?]

## Alternatives Considered
[What other options were considered and why were they rejected?]
```

## Guidelines

- ADRs are numbered sequentially (001, 002, 003, etc.)
- ADRs should be written when the decision is made, not after implementation
- ADRs are immutable once accepted - if you need to change a decision, create a new ADR that supersedes the old one
- Keep ADRs focused on architectural decisions, not implementation details
- Include enough context so that future team members can understand the reasoning
