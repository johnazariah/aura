# .project Directory Organization

## Current Structure

```
.project/
├── README.md                    # Quick navigation, project overview
├── STATUS.md                    # Current state (for AI context)
│
├── features/                    # Feature Documentation (unified spec+plan)
│   ├── README.md                # Index with completion dates
│   ├── completed/               # Implemented features (kebab-case.md)
│   ├── upcoming/                # Not yet implemented (kebab-case.md)
│   └── roadmap.md               # Prioritized sequencing
│
├── adr/                         # Architecture Decision Records
│   ├── README.md                # Index with status table
│   └── NNN-kebab-case-title.md  # e.g., 001-local-first-architecture.md
│
├── progress/                    # Date-stamped Status Reports
│   └── YYYY-MM-DD.md            # e.g., 2025-12-12.md
│
├── reference/                   # Quick Reference Docs
│   ├── api-cheat-sheet.md
│   ├── architecture-quick-reference.md
│   └── coding-standards.md
│
├── troubleshooting/             # Problem-solving guides
│   └── kebab-case-topic.md
│
├── explore/                     # Exploratory notes, ideas
│   └── kebab-case-topic.md
│
└── archive/                     # Historical docs
    ├── origin-story.md
    └── handoff/                 # Session handoffs
```

## Naming Conventions

### General Rules

- **All lowercase** with **kebab-case**: `my-document-title.md`
- **No spaces** in filenames
- **Descriptive names** over cryptic abbreviations

### Feature Documents

Features use descriptive kebab-case names (no numbers):
- `llm-providers.md`, `git-worktrees.md`, `smart-content.md`
- Each completed feature has a `Completed:` date in its header
- The README index provides chronological ordering

### ADRs (Numbered)

ADRs keep numbers for historical sequencing:
- Format: `NNN-kebab-case-title.md`
- Three-digit padding: `001`, `012`, `099`

### Date-Stamped Documents

For progress reports and handoffs:
- Format: `YYYY-MM-DD.md` or `YYYY-MM-DD-topic.md`
- ISO 8601 date format
- Descriptive kebab-case: `smart-content-llm-summaries.md`
- Group related tasks with prefix: `indexing-*.md`, `ui-*.md`

## Status Tracking

### Option A: Frontmatter (Recommended)
Each document includes YAML frontmatter with status:

```yaml
---
title: Smart Content (LLM Summaries)
status: not-started  # not-started | in-progress | complete | archived
priority: medium     # low | medium | high | critical
created: 2025-12-01
updated: 2025-12-12
---
```

### Option B: Index Files (Current Approach)
Each directory has a README.md with a status table:

```markdown
| Document | Status | Priority |
|----------|--------|----------|
| smart-content.md | 🔲 Not Started | Medium |
| dependency-graph.md | 🔄 In Progress | High |
| treesitter-ingesters.md | ✅ Complete | - |
```

### Option C: Both (Best)
Use both frontmatter AND index files:
- Frontmatter is the source of truth
- Index files are generated or manually maintained for quick scanning

## Status Icons (for index files)

| Icon | Meaning |
|------|---------|
| ✅ | Complete |
| 🔄 | In Progress |
| 🔲 | Not Started |
| 📋 | Planned |
| ⏸️ | On Hold |
| ❌ | Cancelled |
| 🗄️ | Archived |

## Why NOT Mark Status in Filename?

1. **Broken links**: Renaming files breaks all references to them
2. **Git history**: `git log --follow` gets confused by renames
3. **Churn**: Status changes frequently, filenames shouldn't
4. **IDE support**: Frontmatter is searchable, filenames less so

## Migration Plan

### Phase 1: Fix Naming Consistency
1. Rename `adr-004-test-project-separation.md` → `004-test-project-separation.md`
2. Rename uppercase files to lowercase

### Phase 2: Create Reference Directory
1. Create `reference/` directory
2. Move quick reference docs there
3. Keep `STATUS.md` at root (special case for AI context)

### Phase 3: Add Index Files
1. Create README.md in each directory
2. Include status tables
3. Add frontmatter to all docs

### Phase 4: Renumber Specs
1. Fill gaps in spec numbering
2. Or consolidate related specs

## Document Types Summary

| Type | Location | Naming | Purpose |
|------|----------|--------|---------|
| **Feature (completed)** | `features/completed/` | `NNN-title.md` | Implemented features |
| **Feature (upcoming)** | `features/upcoming/` | `title.md` | Planned features |
| **ADR** | `adr/` | `NNN-title.md` | Architectural decisions |
| **Progress** | `progress/` | `YYYY-MM-DD.md` | Status snapshots |
| **Reference** | `reference/` | `title.md` | Quick reference |
| **Troubleshooting** | `troubleshooting/` | `title.md` | Problem guides |
| **Explore** | `explore/` | `title.md` | Ideas, research |
| **Archive** | `archive/` | `title.md` | Historical docs |
