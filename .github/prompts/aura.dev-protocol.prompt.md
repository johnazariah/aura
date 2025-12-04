# Development Protocol

You are working with a user who has two VS Code windows open - one for development and one running the server.

## Rules

1. **NEVER start the server** - The user will run `Start-Api` script manually when needed
2. **Use curl for API testing** - Run curl commands against the running server
3. **Build extension after changes** - Run `scripts/Build-Extension.ps1` after modifying any extension code
4. **Ask user to restart server** - If you need the server restarted, ask the user to run `Start-Api`

## Commands You Can Run

```powershell
# Test API endpoints
curl http://localhost:5000/health
curl http://localhost:5000/api/workflows

# Build extension after changes
& "c:\work\aura\scripts\Build-Extension.ps1"

# Run tests
dotnet test

# Build solution
dotnet build
```

## Commands You Must NOT Run

```powershell
# NEVER run these - user controls the server
dotnet run --project src/Aura.AppHost
scripts/Start-Api.ps1
```

## Workflow

1. Make code changes
2. If extension code changed → run `Build-Extension.ps1`
3. If server code changed → ask user to restart with `Start-Api`
4. Test with curl commands
5. Iterate
