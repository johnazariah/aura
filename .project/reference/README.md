# Quick Reference

Essential documentation for day-to-day development.

## Contents

| Document | Description |
|----------|-------------|
| [api-cheat-sheet.md](api-cheat-sheet.md) | All API endpoints with curl examples |
| [architecture-quick-reference.md](architecture-quick-reference.md) | File locations, debugging tips |
| [coding-standards.md](coding-standards.md) | Code style and patterns |
| [functional-patterns.md](functional-patterns.md) | Functional programming patterns |

## Most Used

### Start Development

```powershell
.\scripts\Start-Api.ps1     # Start API + infrastructure
.\scripts\Build-Extension.ps1  # Build VS Code extension
```

### Test API

```powershell
curl http://localhost:5300/health
curl http://localhost:5300/api/developer/workflows
```

### Run Tests

```powershell
.\scripts\Run-UnitTests.ps1
dotnet test
```

### Key Locations

| What | Where |
|------|-------|
| All API endpoints | `src/Aura.Api/Program.cs` |
| Agent definitions | `agents/*.md` |
| Prompt templates | `prompts/*.prompt` |
| Extension code | `extension/src/` |

See [api-cheat-sheet.md](api-cheat-sheet.md) for complete API reference.
