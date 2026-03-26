# Common Issues

## Ollama Not Running

**Symptoms:** `/health/rag` returns 503, embedding failures, search returns no results.

**Fix:**

```powershell
# Check if Ollama is running
ollama list

# Start Ollama (if not running)
ollama serve

# Pull the required model
ollama pull nomic-embed-text
```

Verify:

```bash
curl http://localhost:11434/api/tags
```

## PostgreSQL Connection Failed

**Symptoms:** `/health/db` returns 503, service won't start, `NpgsqlException: Connection refused`.

**Fix (Windows):**

```powershell
# Check the AuraDB service
Get-Service AuraDB
Start-Service AuraDB

# Verify the port (default: 5433)
Test-NetConnection -ComputerName localhost -Port 5433
```

**Fix (macOS):**

```bash
brew services start postgresql@17
psql -d auradb -c "SELECT 1;"
```

**Connection string mismatch:** Ensure `appsettings.json` matches your PostgreSQL setup:

```json
{
  "ConnectionStrings": {
    "auradb": "Host=localhost;Port=5433;Database=auradb;Username=postgres"
  }
}
```

## Embedding Failures

**Symptoms:** Indexing completes but search returns no results, RAG health shows 0 chunks.

**Possible causes:**

1. **Ollama model not pulled:** Run `ollama pull nomic-embed-text`
2. **Wrong embedding provider:** Check `Aura:Embedding:Provider` in settings
3. **OpenAI API key missing:** If provider is `openai` or `auto`, ensure the API key is set
4. **Dimension mismatch:** If you switched models, the `Aura:Rag:EmbeddingDimension` must match

**Debug:**

```powershell
curl http://localhost:5300/health/rag
```

Check the response for chunk counts and error messages.

## Service Won't Start (Windows)

**Symptoms:** AuraService shows Stopped, `Start-Service` fails.

**Check logs:**

```powershell
# Recent event log entries
Get-EventLog -LogName Application -Source ".NET Runtime","AuraService" -Newest 20

# File-based logs
Get-Content "C:\ProgramData\Aura\logs\aura-*.log" -Tail 50
```

**Common causes:**

| Cause | Fix |
|-------|-----|
| Port 5300 in use | Stop the conflicting process or change the Kestrel port |
| Database unavailable | Start AuraDB first |
| Missing .NET runtime | Reinstall Aura (bundles the runtime) |
| Corrupt config | Check `appsettings.json` for syntax errors |

## MCP Tools Not Appearing in Copilot

**Symptoms:** Copilot doesn't show Aura tools, MCP tool calls fail.

**Checklist:**

1. Verify Aura is running: `curl http://localhost:5300/health/mcp`
2. Check VS Code settings:

```json
{
  "mcp": {
    "servers": {
      "aura": {
        "type": "sse",
        "url": "http://localhost:5300/mcp"
      }
    }
  }
}
```

3. Restart VS Code after changing MCP settings
4. Check the MCP tool count: the `/health/mcp` response should list 10 tools

## Search Returns No Results

**Possible causes:**

1. **Workspace not indexed:** Check index status via `GET /api/workspaces/{id}/index`
2. **Wrong workspace:** Ensure `workspacePath` matches a registered workspace
3. **Content type filter too narrow:** Try `contentType: "all"` or omit it
4. **MinRelevanceScore too high:** Lower `Aura:Rag:MinRelevanceScore` (default: 0.3)

## Indexing Stuck or Slow

**Check job status:**

```bash
curl http://localhost:5300/api/workspaces/{id}/index/jobs
```

**Common causes:**

- Large repository with many files — indexing is sequential per file
- Ollama running on CPU only — set `NumGpu: -1` to use GPU
- PDF files without `pdftotext` installed — install poppler-utils

## Windows SmartScreen Warning

When running the installer, Windows SmartScreen may show a warning because the installer is not code-signed.

**Fix:** Click "More info" → "Run anyway". This is safe for builds you downloaded from the official GitHub Releases page.

## Port Conflicts

The installer checks for port conflicts on **5433** (PostgreSQL) and **5300** (API).

To find what's using a port:

```powershell
Get-NetTCPConnection -LocalPort 5300 | Select-Object OwningProcess
Get-Process -Id <PID>
```
