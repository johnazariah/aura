# Common Issues

Solutions to frequently encountered problems.

## Installation Issues

### "Windows protected your PC" SmartScreen Warning

**Problem:** Windows blocks the installer from running.

**Solution:**

1. Click "More info"
2. Click "Run anyway"

This is normal for new/unsigned applications.

### MCP Tools Not Appearing

**Problem:** Copilot does not show Aura tools.

**Solution:**

1. Verify MCP health:

   ```powershell
   curl http://localhost:5300/health/mcp
   ```

2. Verify global config exists:

   ```powershell
   type "$env:USERPROFILE\.copilot\mcp-config.json"
   ```

3. Start a new Copilot session so tools are rediscovered.

### Installer Fails with "Access Denied"

**Problem:** Permission error during installation.

**Solution:**

1. Run installer as Administrator
2. Or install to a user-writable location

## Startup Issues

### "Aura API not responding"

**Problem:** VS Code shows API as disconnected.

**Solutions:**

1. **Check Windows Service:**

   ```powershell
   Get-Service AuraService
   # If stopped:
   Start-Service AuraService
   ```

2. **Check manually:**

   ```powershell
   curl http://localhost:5300/health
   ```

3. **Check logs:**
   - Open Event Viewer
   - Windows Logs → Application
   - Filter by Source: "AuraService"

### "Database connection failed"

**Problem:** Can't connect to PostgreSQL.

**Solutions:**

1. **Check PostgreSQL service:**

   ```powershell
   Get-Service AuraDB
   # If stopped:
   Start-Service AuraDB
   ```

2. **Verify port:**

   ```powershell
   Test-NetConnection localhost -Port 5432
   ```

3. **Check connection string** in `appsettings.json`

### "No LLM provider available"

**Problem:** Aura can't connect to any LLM.

**Solutions:**

1. **Check Ollama is running:**
   - Look for Ollama in system tray
   - Or run: `ollama list`

2. **Pull a model:**

   ```powershell
   ollama pull qwen2.5-coder:7b
   ```

3. **Check provider config** in `appsettings.json`

## Search Issues

### Search Not Finding Relevant Content

**Problem:** Aura search returns weak or empty results.

**Solutions:**

1. Re-index the workspace
2. Confirm the workspace is registered
3. Check supported content types and exclusions
4. Check embedding provider health (Ollama or OpenAI)

## Indexing Issues

### Indexing Takes Forever

**Problem:** Indexing never completes for large repos.

**Solutions:**

1. **Add exclusions** to skip unnecessary files:
   - `node_modules/`
   - `vendor/`
   - Large generated files

2. **Index specific directories** instead of whole repo

3. **Check for very large files** (>1MB)

### "Out of memory" During Indexing

**Problem:** Process crashes during indexing.

**Solutions:**

1. **Increase exclusions**
2. **Index in smaller batches**
3. **Ensure 8GB+ RAM available**

## Performance Issues

### High CPU Usage

**Problem:** Aura using too much CPU.

**Causes:**

- Active indexing
- LLM processing
- Multiple workflows

**Solutions:**

1. Wait for indexing to complete
2. Run one workflow at a time
3. Use cloud LLM to offload processing

### High Memory Usage

**Problem:** Aura using too much RAM.

**Causes:**

- Large model loaded in Ollama
- Many files indexed

**Solutions:**

1. Use a smaller model
2. Restart services to clear memory
3. Close unused workflows

## Getting More Help

If these solutions don't help:

1. Check [GitHub Issues](https://github.com/johnazariah/aura/issues)
2. See [Logs & Diagnostics](logs.md) for debugging
3. See [Getting Help](support.md) for support options
