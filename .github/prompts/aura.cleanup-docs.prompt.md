# Docs Cleanup Prompt

Run this prompt periodically to clean up documentation sprawl in the `/docs` folder.

## Goals

1. **Archive stale content** - Move implementation details, resolved issues, and historical docs to `/docs/archive/`
2. **Consolidate duplicates** - Merge overlapping docs into single authoritative sources  
3. **Remove obsolete content** - Delete docs that are no longer accurate or relevant
4. **Maintain clean hierarchy** - Keep `/docs/` focused on current, actionable documentation

## Cleanup Rules

### Move to `/docs/archive/implementation-history/`
- Implementation summaries for completed stories
- Historical analysis documents
- Superseded proposals
- Demo scripts and talk outlines

### Move to `/docs/archive/resolved-issues/`
- Bug analysis documents
- Performance investigations (resolved)
- Migration notes (completed)

### Move to `/docs/archive/completed-stories/`
- Story implementation details
- Story completion summaries

### Consolidate (merge into existing docs)
- Multiple testing docs → single `TESTING.md` or keep only `TESTING-QUICK-START.md`
- Multiple architecture docs → single `ARCHITECTURE.md`
- Provider-specific docs → `CONFIGURATION.md` or `EXTENDING-AURA.md`

### Keep in `/docs/` (current, actionable)
- `README.md` - Entry point
- `ARCHITECTURE.md` - Current system design
- `CODING-STANDARDS.md` - Active development standards
- `CONFIGURATION.md` - How to configure
- `TESTING-QUICK-START.md` - How to run tests
- `NEW-MACHINE-SETUP.md` - Getting started
- `EXTENDING-AURA.md` - How to extend
- `USAGE.md` - How to use
- `PRODUCT-MISSION.md` - Product vision

### Delete candidates
- Duplicate content already captured elsewhere
- Outdated proposals that were never implemented
- Temporary working documents

## Execution

1. List all files in `/docs/` with sizes
2. Read first 30-50 lines of each to assess content
3. Categorize each file: keep, archive, consolidate, delete
4. Execute moves/deletions
5. Update any cross-references in remaining docs
6. Report summary of changes

## Example Commands

```powershell
# Move to archive
Move-Item "docs/SOME-OLD-DOC.md" "docs/archive/implementation-history/"

# Delete obsolete
Remove-Item "docs/OBSOLETE.md"

# Check remaining
Get-ChildItem "docs/*.md" | Measure-Object
```
