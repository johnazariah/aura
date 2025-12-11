# Implementation Plans

Step-by-step guides for implementing features.

## Status Key

| Icon | Status |
|------|--------|
| ✅ | Complete |
| 🔄 | In Progress |
| 🔲 | Not Started |

## Plan Index

### Core Infrastructure (Complete)

| # | Plan | Status | Effort |
|---|------|--------|--------|
| 00 | [00-overview.md](implementation/00-overview.md) | ✅ | - |
| 01 | [01-core-infrastructure.md](implementation/01-core-infrastructure.md) | ✅ | 2-3h |
| 02 | [02-llm-providers.md](implementation/02-llm-providers.md) | ✅ | 1-2h |
| 03 | [03-data-layer.md](implementation/03-data-layer.md) | ✅ | 1-2h |
| 04 | [04-api-endpoints.md](implementation/04-api-endpoints.md) | ✅ | 2-3h |
| 05 | [05-git-worktrees.md](implementation/05-git-worktrees.md) | ✅ | 1-2h |
| 06 | [06-extension.md](implementation/06-extension.md) | ✅ | 3-4h |
| 07 | [07-migration.md](implementation/07-migration.md) | ✅ | 2-3h |
| 08 | [08-agent-implementation.md](implementation/08-agent-implementation.md) | ✅ | - |
| 09 | [09-rag-pipeline.md](implementation/09-rag-pipeline.md) | ✅ | - |
| 10 | [10-installable-service.md](implementation/10-installable-service.md) | ✅ | - |
| 11 | [11-developer-module-tools.md](implementation/11-developer-module-tools.md) | ✅ | - |

### Post-MVP Enhancements

| # | Plan | Status | Effort | Priority |
|---|------|--------|--------|----------|
| 12 | [12-dependency-graph-edges.md](implementation/12-dependency-graph-edges.md) | 🔲 | 4-6h | P0 |
| 13 | [13-unified-capability-model.md](implementation/13-unified-capability-model.md) | 🔲 | 6-8h | P0 |
| 14 | [14-post-mvp-roadmap.md](implementation/14-post-mvp-roadmap.md) | 📋 | - | - |

### Testing

| Plan | Status | Description |
|------|--------|-------------|
| [test-strategy.md](testing/test-strategy.md) | ✅ | Testing approach |

## Effort Estimates

| Total MVP | ~15h |
|-----------|------|
| Post-MVP P0 | 10-14h |
| Post-MVP P1 | 22-32h |
| Post-MVP P2 | 16-24h |
| Post-MVP P3 | 30-40h |

See [14-post-mvp-roadmap.md](implementation/14-post-mvp-roadmap.md) for detailed breakdown.

## Adding a New Plan

```markdown
# Implementation Plan: [Title]

## Status
Not Started | In Progress | Complete

## Estimated Effort
X-Y hours

## Prerequisites
[Dependencies]

## Steps
1. Step 1
2. Step 2

## Acceptance Criteria
- [ ] Criterion 1
- [ ] Criterion 2
```
