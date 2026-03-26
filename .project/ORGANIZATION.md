# .project Directory Organization

## Current Structure

```
.project/
├── README.md                    # Quick navigation, project overview
├── STATUS.md                    # Current state (for AI context)
├── ORGANIZATION.md              # This file
│
├── features/                    # Feature Documentation
│   ├── README.md                # Index of all features
│   ├── completed/               # Shipped capabilities (kebab-case.md)
│   ├── in-progress/             # Active work (kebab-case.md)
│   ├── proposed/                # Backlog ideas (kebab-case.md)
│   ├── spikes/                  # Time-boxed research
│   └── templates/               # Feature and spike templates
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
- `llm-providers.md`, `git-worktrees.md`, `mcp-server.md`
- Each completed feature has a `Completed:` date in its header
- The README index lists features alphabetically

### ADRs (Numbered)

ADRs keep numbers for historical sequencing:
- Format: `NNN-kebab-case-title.md`
- Three-digit padding: `001`, `012`, `099`

### Date-Stamped Documents

For progress reports and handoffs:
- Format: `YYYY-MM-DD.md` or `YYYY-MM-DD-topic.md`
- ISO 8601 date format

## Status Icons (for index files)

| Icon | Meaning |
|------|---------|
| ✅ | Complete |
| 🔄 | In Progress |
| 🔲 | Not Started |
| 📋 | Planned |
| ⏸️ | On Hold |
| ❌ | Cancelled |

## Document Types Summary

| Type | Location | Naming | Purpose |
|------|----------|--------|---------|
| **Feature (completed)** | `features/completed/` | `kebab-case.md` | Shipped features |
| **Feature (in-progress)** | `features/in-progress/` | `kebab-case.md` | Active work |
| **Feature (proposed)** | `features/proposed/` | `kebab-case.md` | Backlog ideas |
| **ADR** | `adr/` | `NNN-title.md` | Architectural decisions |
| **Progress** | `progress/` | `YYYY-MM-DD.md` | Status snapshots |
| **Reference** | `reference/` | `title.md` | Quick reference |
| **Troubleshooting** | `troubleshooting/` | `title.md` | Problem guides |
| **Explore** | `explore/` | `title.md` | Ideas, research |
| **Archive** | `archive/` | `title.md` | Historical docs |
