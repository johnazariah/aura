# Logs & Diagnostics

How to find and interpret Aura logs for troubleshooting.

## Log Locations

### Aura API Logs

When running as a service, logs go to Windows Event Log:

1. Open **Event Viewer**
2. Navigate to: Windows Logs → Application
3. Filter by Source: "AuraService" or ".NET Runtime"

### Console Output

When running manually (development):

```powershell
cd "C:\Program Files\Aura\api"
.\Aura.Api.exe
```

Logs appear in the console with timestamps.

### Extension Logs

In VS Code:

1. View → Output (`Ctrl+Shift+U`)
2. Select "Aura" from the dropdown

## Log Levels

| Level | Color | Meaning |
|-------|-------|---------|

| **TRC** | Gray | Trace - very detailed |
| **DBG** | Blue | Debug - debugging info |
| **INF** | Green | Info - normal operations |
| **WRN** | Yellow | Warning - potential issues |
| **ERR** | Red | Error - something failed |
| **CRT** | Magenta | Critical - fatal error |

## Common Log Patterns

### Successful Startup

```text
[INF] Starting Aura API...
[INF] Database connection established
[INF] Loaded 12 agents from agents/
[INF] Ollama provider available at http://localhost:11434
[INF] Listening on http://localhost:5300
```

### LLM Connection Failed

```text
[WRN] Failed to connect to Ollama at http://localhost:11434
[ERR] HttpRequestException: Connection refused
[INF] Falling back to next provider...
```

**Solution:** Start Ollama or check the URL.

### Database Connection Failed

```text
[ERR] Failed to connect to database
[ERR] NpgsqlException: Connection refused
[CRT] Cannot start without database connection
```

**Solution:** Start the AuraDB service.

### Agent Loading Error

```text
[WRN] Failed to load agent from agents/broken-agent.md
[WRN] Parse error at line 15: Invalid YAML
[INF] Loaded 11 agents (1 failed)
```

**Solution:** Fix the agent file syntax.

## Enabling Debug Logging

For more detailed logs, edit `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Aura": "Debug"
    }
  }
}
```

Then restart the service:

```powershell
Restart-Service AuraService
```

### Specific Component Debugging

```json
{
  "Logging": {
    "LogLevel": {
      "Aura.Foundation.Llm": "Trace",
      "Aura.Foundation.Rag": "Debug",
      "Aura.Module.Developer": "Debug"
    }
  }
}
```

## Collecting Diagnostics

### Quick Health Check

```powershell
# API health
curl http://localhost:5300/health

# Services status
Get-Service AuraService, AuraDB | Format-Table Name, Status

# Ollama status
ollama list
```

### Full Diagnostic Report

```powershell
# Create diagnostic folder
$diagDir = "$env:TEMP\aura-diagnostics"
New-Item -ItemType Directory -Path $diagDir -Force

# Collect service status
Get-Service AuraService, AuraDB | Out-File "$diagDir\services.txt"

# Collect API info
try {
    Invoke-RestMethod http://localhost:5300/health | Out-File "$diagDir\health.txt"
} catch {
    "API not responding: $_" | Out-File "$diagDir\health.txt"
}

# Collect config (redact secrets)
Get-Content "$env:ProgramFiles\Aura\api\appsettings.json" | 
    ForEach-Object { $_ -replace '"ApiKey":\s*"[^"]*"', '"ApiKey": "***REDACTED***"' } |
    Out-File "$diagDir\config.txt"

# Collect recent event logs
Get-EventLog -LogName Application -Source ".NET Runtime","AuraService" -Newest 50 -ErrorAction SilentlyContinue |
    Out-File "$diagDir\eventlog.txt"

Write-Host "Diagnostics saved to: $diagDir"
```

## PostgreSQL Logs

If database issues occur:

```powershell
# Check PostgreSQL log
Get-Content "$env:ProgramFiles\Aura\data\log\postgresql-*.log" -Tail 50
```

### Common Database Errors

| Error | Meaning | Solution |
|-------|---------|----------|

| `FATAL: database "auradb" does not exist` | DB not created | Reinstall or manually create |
| `FATAL: password authentication failed` | Wrong credentials | Check connection string |
| `could not connect to server` | PostgreSQL not running | Start AuraDB service |

## Ollama Logs

For Ollama issues:

```powershell
# Ollama logs (if running as service)
Get-Content "$env:LOCALAPPDATA\Ollama\logs\server.log" -Tail 50
```

## Extension Diagnostics

In VS Code, open Developer Tools:

1. Help → Toggle Developer Tools (`Ctrl+Shift+I`)
2. Click "Console" tab
3. Filter by "aura" to see extension logs

## Reporting Issues

When reporting a bug, include:

1. **Description** of what happened
2. **Steps to reproduce**
3. **Expected vs actual behavior**
4. **Log excerpts** (sanitized of secrets)
5. **Version info:**

   ```powershell
   Get-Content "$env:ProgramFiles\Aura\version.json"
   ollama --version
   code --version
   ```

Submit at: [GitHub Issues](https://github.com/johnazariah/aura/issues)
