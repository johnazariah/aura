---
description: Investigate Aura service failures using logs, process state, and recent changes.
---

# Investigate Service Failure

You are investigating an Aura service failure. Follow this structured approach — **every failure is a bug until proven otherwise**.

## Step 1: Check Service Status

```powershell
# Is the service running?
Get-Service AuraService | Select-Object Name, Status, StartType

# What port is it listening on?
netstat -ano | Select-String ":5300"

# Quick health check
curl -s http://localhost:5300/health
```

If the service isn't running, check why it stopped:
```powershell
# Windows Event Log
Get-WinEvent -LogName Application -MaxEvents 20 | 
    Where-Object { $_.ProviderName -like "*Aura*" -or $_.Message -like "*Aura*" } |
    Format-List TimeCreated, Message
```

## Step 2: Read the Logs

Aura logs to `C:\ProgramData\Aura\logs\aura-YYYYMMDD.log`:

```powershell
# Today's log
$logFile = "C:\ProgramData\Aura\logs\aura-$(Get-Date -Format 'yyyyMMdd').log"

# Last 50 lines (most recent activity)
Get-Content $logFile -Tail 50

# Search for errors
Select-String -Path $logFile -Pattern "ERR|Exception|FAIL|Error" -Context 2,5

# Search for specific operation
Select-String -Path $logFile -Pattern "{operation or story ID}" -Context 2,5
```

**Key log patterns:**
- `[ERR]` — errors that need investigation
- `[WRN]` — warnings (may indicate issues)
- `[INF]` — informational (normal operation)
- `Exception` — unhandled or caught exceptions with stack traces

## Step 3: Classify the Failure

| Symptom | Likely Cause | Investigation |
|---------|-------------|---------------|
| Service won't start | Config error, port conflict, missing dependency | Check Event Log + startup logs |
| 500 on API call | Unhandled exception in endpoint | Check logs for stack trace |
| MCP tool "disconnected" | HTTP client timeout or service crash | Check if service is still running |
| Story execution fails | LLM error, build error, or git error | Check story-specific logs |
| Database errors | PostgreSQL not running or migration needed | Check connection string + DB status |
| Timeout errors | LLM provider slow or network issue | Check Azure OpenAI status |
| "Restore failed" / build errors | SDK resolution, NuGet config issues | Check captured build output in logs |

## Step 4: Reproduce the Issue

```powershell
# Try the exact API call that failed
curl -v http://localhost:5300/{endpoint}

# If it's a story execution failure, check the story state
curl -s http://localhost:5300/api/developer/stories/{storyId} | ConvertFrom-Json | Format-List
```

## Step 5: Check Recent Changes

```powershell
# What changed recently?
git log --oneline -10

# Any uncommitted changes?
git status --short

# Diff against last known good state
git diff HEAD~3 -- src/
```

## Step 6: Common Root Causes

### Azure OpenAI connection failure
- Check `appsettings.json` → `LlmProviders:AzureOpenAI` for endpoint and key
- Verify the endpoint is accessible: `curl -s https://{endpoint}/openai/models?api-version=2024-02-01`
- Check for rate limiting (429 responses in logs)

### PostgreSQL not running
```powershell
# Check if Podman container is running
podman ps | Select-String "postgres"

# Start if needed
podman start aura-postgres
```

### dotnet build failures during story execution
- The service runs as **LocalSystem** — different PATH and SDK resolution than your user
- Check if the target project has workload dependencies (e.g., Aspire SDK)
- Look for `--source` overrides that break SDK resolution
- Consider NuGet config: does the project need specific feeds?

### MCP handler crashes
- Check for unhandled exceptions in `McpHandler` partial files
- JSON deserialization errors (malformed input from Copilot)
- Missing service dependencies (null services)

## Step 7: Fix in Product, Not Environment

**CRITICAL**: If you need a workaround, that's a bug in Aura. Fix it properly:

| ❌ Don't | ✅ Do |
|----------|-------|
| Set env var on this machine only | Add proper config fallback in code |
| Restart and hope | Find root cause in logs |
| "It works for me locally" | Consider the service runs as LocalSystem |
| Skip the test | Fix the underlying issue |
| Add retry without understanding why | Fix the failure, then add retry as resilience |

## Step 8: Verify the Fix

```powershell
# Build
dotnet build

# Test
dotnet test

# Ask user to update the service
# "Please run Update-LocalInstall.ps1 as Administrator"

# After service restart, verify
curl -s http://localhost:5300/health
```

## Checklist

- [ ] Service status checked (running, port, health)
- [ ] Logs read for errors and exceptions
- [ ] Failure classified (config, runtime, LLM, DB, build)
- [ ] Issue reproduced with specific API call
- [ ] Root cause identified (not dismissed as transient)
- [ ] Recent changes reviewed for correlation
- [ ] Fix applied in product code (not environment workaround)
- [ ] Build and tests pass
- [ ] Service health verified after update
