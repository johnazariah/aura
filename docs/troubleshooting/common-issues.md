# Common Issues

Solutions to frequently encountered problems.

## Installation Issues

### "Windows protected your PC" SmartScreen Warning

**Problem:** Windows blocks the installer from running.

**Solution:**

1. Click "More info"
2. Click "Run anyway"

This is normal for new/unsigned applications.

### VS Code Extension Not Installed

**Problem:** Extension wasn't installed during setup.

**Solution:**

```powershell
& "$env:ProgramFiles\Aura\scripts\install-extension.ps1"
```

Then reload VS Code.

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
   Test-NetConnection localhost -Port 5433
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

## Workflow Issues

### Workflow Stuck in "Analyzing"

**Problem:** Workflow never completes analysis.

**Solutions:**

1. **Check LLM is responding:**

   ```powershell
   curl http://localhost:11434/api/generate -d '{"model":"qwen2.5-coder:7b","prompt":"Hi"}'
   ```

2. **Increase timeout** in settings

3. **Try a smaller model** for faster response

### "Failed to create worktree"

**Problem:** Git worktree creation fails.

**Solutions:**

1. **Check you're in a git repository:**

   ```powershell
   git status
   ```

2. **Check worktree directory is writable:**

   ```powershell
   Test-Path .worktrees
   ```

3. **Clean up old worktrees:**

   ```powershell
   git worktree prune
   ```

### Steps Generating Wrong Code

**Problem:** AI-generated code doesn't match expectations.

**Solutions:**

1. **Be more specific** in workflow description
2. **Add context** about existing patterns
3. **Try a better model** (gpt-4o vs local)
4. **Edit steps** before approving

## Chat Issues

### Chat Not Finding Code

**Problem:** Chat can't find relevant code.

**Solutions:**

1. **Re-index the repository:**
   - Aura panel → Code Graph → Index Repository

2. **Check file is in supported language**

3. **Verify file isn't excluded** from indexing

### Chat Responses Are Slow

**Problem:** Long wait times for chat responses.

**Solutions:**

1. **Use a smaller model:**

   ```powershell
   ollama pull llama3.2:3b
   ```

2. **Check system resources:**
   - RAM usage
   - GPU availability

3. **Reduce context** by being more specific

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

## Extension Issues

### Extension Not Visible in VS Code

**Problem:** Aura icon missing from sidebar.

**Solutions:**

1. **Check extension is installed:**
   - `Ctrl+Shift+X` → Search "Aura"

2. **Reinstall:**

   ```powershell
   & "$env:ProgramFiles\Aura\scripts\install-extension.ps1"
   ```

3. **Reload VS Code:**
   - `Ctrl+Shift+P` → "Developer: Reload Window"

### Extension Shows Errors

**Problem:** Red error indicators in extension.

**Solutions:**

1. **Check Output panel:**
   - View → Output
   - Select "Aura" from dropdown

2. **Check API connection**

3. **Restart extension host:**
   - `Ctrl+Shift+P` → "Developer: Restart Extension Host"

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
