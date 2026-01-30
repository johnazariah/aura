# Development Protocol

You are working on the Aura project. Aura runs as a Windows Service.

## Rules

1. **NEVER run Update-LocalInstall** - Ask the user to run it as Administrator when needed
2. **Use curl for API testing** - Run curl commands against the running service
3. **Build extension after changes** - Run `scripts/Build-Extension.ps1` after modifying any extension code
4. **Ask user to update install** - If server code changed, ask user to run `Update-LocalInstall.ps1` as Administrator

## Commands You Can Run

```powershell
# Test API endpoints
curl http://localhost:5300/health
curl http://localhost:5300/api/developer/workflows

# Build extension after changes
.\scripts\Build-Extension.ps1

# Run tests
dotnet test
.\scripts\Run-UnitTests.ps1

# Build solution
dotnet build
```

## Commands You Must NOT Run

```powershell
# NEVER run these - require elevation, ask user to run
.\scripts\Update-LocalInstall.ps1
```

## Workflow

1. Make code changes
2. If extension code changed → run `Build-Extension.ps1`
3. If server code changed → ask user to run `Update-LocalInstall.ps1` as Administrator
4. Test with curl commands
5. Iterate
