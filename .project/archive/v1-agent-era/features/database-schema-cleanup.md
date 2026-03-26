# Database Schema Cleanup

**Status:** ✅ Complete
**Completed:** 2026-01-30
**Priority:** High (pre-ship blocker)
**Complexity:** Medium

## Problem

The database schema is inconsistent:
1. **Mixed naming conventions** - Some columns are PascalCase (`CurrentWave`), some are snake_case (`agent_id`)
2. **Stale entity names** - Table is `workflows` but code uses `Story` entity
3. **Migration cruft** - Multiple migrations that could be flattened since we have zero customers

## Requirements

### 1. Flatten Migrations
- Delete all existing migrations in both contexts:
  - `src/Aura.Foundation/Data/Migrations/`
  - `src/Aura.Module.Developer/Data/Migrations/`
- Create single clean `InitialCreate` migration for each
- Update any seed data scripts

### 2. Rename `workflows` → `stories`
- Rename table: `workflows` → `stories`
- Rename table: `workflow_steps` → `story_steps`
- Add `story_tasks` table (currently missing from schema?)
- Update all foreign key references

### 3. Consistent Snake Case Columns
All columns should use `snake_case`:
- `CurrentWave` → `current_wave`
- `OrchestratorStatus` → `orchestrator_status`
- `GitBranch` → `git_branch`
- etc.

### 4. Verify Entity Mappings
Ensure EF Core entity configurations use explicit column mappings:
```csharp
entity.ToTable("stories");
entity.Property(e => e.CurrentWave).HasColumnName("current_wave");
```

## Implementation Steps

1. **Backup** - Export current data if any test data worth keeping
2. **Drop migrations** - Delete all migration files
3. **Update DbContext** - Add explicit `ToTable()` and `HasColumnName()` mappings
4. **Regenerate** - `dotnet ef migrations add InitialCreate`
5. **Test locally** - Drop and recreate database
6. **Update installer** - Ensure fresh install works

## Files Affected

- `src/Aura.Foundation/Data/AuraDbContext.cs`
- `src/Aura.Foundation/Data/Migrations/*` (delete all, regenerate)
- `src/Aura.Module.Developer/Data/DeveloperDbContext.cs`
- `src/Aura.Module.Developer/Data/Migrations/*` (delete all, regenerate)
- `src/Aura.Module.Developer/Data/Entities/Story.cs` (verify mappings)

## Acceptance Criteria

- [ ] All tables use snake_case names
- [ ] All columns use snake_case names
- [ ] `workflows` table renamed to `stories`
- [ ] Single clean migration per DbContext
- [ ] Fresh install creates clean schema
- [ ] All existing tests pass
