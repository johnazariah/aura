# Phase 7: Migration and Cleanup

**Duration:** 2-3 hours  
**Dependencies:** All previous phases  
**Output:** Working system, old code deleted

## Objective

Finalize the migration, run tests, and clean up the old codebase.

## Tasks

### 7.1 Integration Testing

Run the new system end-to-end:

```powershell
# Terminal 1: Start Ollama
ollama serve

# Terminal 2: Start PostgreSQL (via Docker)
docker run -d --name aura-db -e POSTGRES_PASSWORD=aura -p 5432:5432 postgres:16

# Terminal 3: Start Aura.Host
cd src/Aura.Host
dotnet run

# Terminal 4: Test endpoints
curl http://localhost:5258/health
curl http://localhost:5258/api/agents
curl http://localhost:5258/api/workflows
```

### 7.2 Run Test Suite

```powershell
# Unit tests
dotnet test src/Aura.Tests --filter "Category!=Integration"

# Integration tests (requires running services)
dotnet test src/Aura.Tests --filter "Category=Integration"

# Extension tests
cd extension
npm test
```

### 7.3 Verify Agent Loading

Check that existing agents load correctly:

```powershell
# Should show all agents from agents/*.md
curl http://localhost:5258/api/agents | jq '.agents[] | {id, name, capabilities}'
```

Expected agents:
- business-analyst-agent
- coding-agent
- documentation-agent
- issue-enrichment-agent
- testing-agent
- pr-health-monitor-agent

### 7.4 Verify Workflow Lifecycle

Test the 3-phase workflow:

```powershell
# Create workflow
$workflow = curl -X POST http://localhost:5258/api/workflows `
  -H "Content-Type: application/json" `
  -d '{"workItemId":"test-1","workItemTitle":"Add hello world function","workspacePath":"/path/to/repo"}'

$id = ($workflow | ConvertFrom-Json).id

# ENRICH
curl -X POST "http://localhost:5258/api/workflows/$id/enrich"

# Plan
curl -X POST "http://localhost:5258/api/workflows/$id/plan"

# Get workflow with steps
curl "http://localhost:5258/api/workflows/$id" | jq

# Execute first step
$stepId = (curl "http://localhost:5258/api/workflows/$id" | ConvertFrom-Json).steps[0].id
curl -X POST "http://localhost:5258/api/workflows/$id/steps/$stepId/execute"
```

### 7.5 Verify Extension

1. Open VS Code with extension loaded (F5)
2. Check Aura sidebar shows:
   - Workflows panel (with any existing workflows)
   - Agents panel (with loaded agents)
3. Create a new workflow via command palette
4. Open workflow, verify phases are visible
5. Test ENRICH, Plan, Execute buttons

### 7.6 Update Solution File

Create new solution with only new projects:

```powershell
# Create new solution
dotnet new sln -n Aura -o src/

# Add projects
dotnet sln src/Aura.sln add src/Aura/Aura.csproj
dotnet sln src/Aura.sln add src/Aura.Host/Aura.Host.csproj
dotnet sln src/Aura.sln add src/Aura.Tests/Aura.Tests.csproj
```

### 7.7 Delete Old Projects

Once new system is verified working:

```powershell
# Remove old projects from solution
$oldProjects = @(
    "AgentOrchestrator.Agents",
    "AgentOrchestrator.AgentService",
    "AgentOrchestrator.AI",
    "AgentOrchestrator.Api",
    "AgentOrchestrator.AppHost",
    "AgentOrchestrator.CodeAnalysis",
    "AgentOrchestrator.Contracts",
    "AgentOrchestrator.Core",
    "AgentOrchestrator.Data",
    "AgentOrchestrator.Git",
    "AgentOrchestrator.Orchestration",
    "AgentOrchestrator.Plugins",
    "AgentOrchestrator.Providers",
    "AgentOrchestrator.Roslyn",
    "AgentOrchestrator.ServiceDefaults"
)

foreach ($proj in $oldProjects) {
    Remove-Item -Recurse -Force "src/$proj" -ErrorAction SilentlyContinue
}

# Keep for now (separate concerns):
# - AgentOrchestrator.Installer
# - AgentOrchestrator.Tray
```

### 7.8 Update Extension to Remove Old Files

```powershell
cd extension/src

# Remove old service files (backup first)
Move-Item agentOrchestratorService.ts agentOrchestratorService.ts.bak
Move-Item gitHubService.ts gitHubService.ts.bak

# Remove old panel files if replaced
# Keep tree providers if updated in place
```

### 7.9 Update Documentation

**Update README.md:**

```markdown
# Aura

Local-first AI development automation.

## Quick Start

```bash
# Start services
docker-compose up -d postgres
ollama serve &
dotnet run --project src/Aura.Host

# Open VS Code with extension
code --extensionDevelopmentPath=./extension .
```

## Project Structure

```
src/
├── Aura/           # Core library
├── Aura.Host/      # API host
└── Aura.Tests/     # Tests

extension/          # VS Code extension
agents/             # Agent definitions (.md files)
```

## Development

```bash
# Run tests
dotnet test

# Build extension
cd extension && npm run compile
```
```

### 7.10 Create Migration Notes

**MIGRATION.md:**

```markdown
# Migration from AgentOrchestrator to Aura

## Breaking Changes

1. **API URL** - Now `/api/workflows` instead of `/api/orchestration/workflows`
2. **Agent registration** - POST to `/api/agents` instead of gRPC
3. **Workflow phases** - Explicit `/enrich`, `/plan`, `/execute` endpoints
4. **Step execution** - POST to `/steps/{id}/execute` with optional `agentId`

## Removed Features

1. Autonomous orchestration loop - replaced with HITL model
2. Execution planner - planning is now a single agent call
3. Agent selector service - agents selected by capability + priority

## Database Migration

Run the new migrations:

```bash
cd src/Aura.Host
dotnet ef database update
```

Or start fresh:

```bash
# Drop and recreate database
docker-compose down -v
docker-compose up -d postgres
dotnet ef database update
```
```

### 7.11 Final Verification Checklist

- [ ] All unit tests pass
- [ ] Integration tests pass (or skip with documented reasons)
- [ ] Extension loads without errors
- [ ] Agents load from `agents/` folder
- [ ] Hot-reload works (edit agent .md, verify changes)
- [ ] Workflow CRUD works
- [ ] ENRICH phase calls agent
- [ ] Plan phase creates steps
- [ ] Execute step calls agent
- [ ] Git worktree creation works
- [ ] Health endpoints respond

### 7.12 Line Count Verification

Compare before/after:

```powershell
# Before (current)
Get-ChildItem -Path "src" -Filter "*.cs" -Recurse | 
    Where-Object { $_.FullName -notmatch "\\obj\\|\\bin\\" } |
    Get-Content | Measure-Object -Line
# Expected: ~38,000 lines

# After (new)
Get-ChildItem -Path "src/Aura*" -Filter "*.cs" -Recurse |
    Where-Object { $_.FullName -notmatch "\\obj\\|\\bin\\" } |
    Get-Content | Measure-Object -Line
# Target: < 5,000 lines
```

## Success Criteria

| Metric | Before | After | Target |
|--------|--------|-------|--------|
| Projects | 17 | 3 | ✅ |
| Lines of C# | ~38,000 | < 5,000 | ✅ |
| Extension TS | ~3,000 | < 500 | ✅ |
| Build time | ~30s | < 10s | ✅ |
| Test coverage | 40%? | > 70% | ✅ |

## Rollback Plan

If issues are found:

1. Keep old solution file as `AgentOrchestrator.sln.bak`
2. Keep old projects in `src-old/` directory
3. Extension has `.bak` files for old services
4. Can restore by:
   ```powershell
   Move-Item src-old/* src/
   Move-Item AgentOrchestrator.sln.bak AgentOrchestrator.sln
   ```

## Post-Migration Tasks

1. Update CI/CD pipelines for new project structure
2. Update deployment scripts
3. Update developer documentation
4. Announce migration to team
5. Archive old project files (don't delete from git history)
