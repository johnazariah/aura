# Troubleshooting Guide

**Last Updated**: 2026-01-27  
**Version**: 1.3.1

## Overview

This guide covers common issues with Aura setup and usage, along with diagnostic commands and solutions.

## Quick Diagnostics

### Health Check Commands

```powershell
# Overall API health
curl http://localhost:5300/health

# LLM provider status
curl http://localhost:5300/health/ollama

# Database connectivity
curl http://localhost:5300/health/db

# RAG service status
curl http://localhost:5300/health/rag

# Agent registry status
curl http://localhost:5300/health/agents
```

### Common Log Locations

| Environment | Log Path |
|-------------|----------|
| Production (Windows) | `C:\ProgramData\Aura\logs\aura-YYYYMMDD.log` |
| Development | Console output |
| Container | `docker logs <container-id>` |

---

## Common Issues

### 1. API Not Responding

**Symptom**: `curl http://localhost:5300/health` fails or times out.

**Causes**:
- API server not running
- Port 5300 already in use
- Firewall blocking connections

**Solutions**:

```powershell
# Check if server is running
Get-Process -Name "Aura.Api" -ErrorAction SilentlyContinue

# Check port usage
netstat -ano | findstr :5300

# Start the API (for production install)
# Run as Administrator
.\scripts\Start-Api.ps1

# Or for development
cd src/Aura.AppHost
dotnet run
```

**If port 5300 is in use**:
```powershell
# Find the process using port 5300
netstat -ano | findstr :5300
# Example output: TCP 0.0.0.0:5300 0.0.0.0:0 LISTENING 12345

# Stop the process (replace 12345 with actual PID)
Stop-Process -Id 12345 -Force
```

---

### 2. "Workspace Not Indexed"

**Symptom**: MCP tools return "Workspace not found" or "No indexed content for workspace".

**Cause**: Workspace has not been onboarded or indexing is incomplete.

**Solution**:

```powershell
# 1. Check if workspace exists
curl "http://localhost:5300/api/workspaces/lookup?path=C%3A%5Cwork%5Cmyrepo"

# 2. If not found, onboard the workspace
curl -X POST http://localhost:5300/api/workspaces `
  -H "Content-Type: application/json" `
  -d '{"path":"C:\\work\\myrepo"}'

# 3. Wait for indexing to complete (check status)
curl http://localhost:5300/api/workspaces

# 4. If indexing failed, re-index
curl -X POST "http://localhost:5300/api/workspaces/{workspace-id}/reindex"
```

**Check indexing progress**:
```powershell
# RAG statistics
curl http://localhost:5300/api/rag/stats

# Workspace details
curl http://localhost:5300/api/workspaces/{workspace-id}
```

---

### 3. LLM Provider Connection Failed

**Symptom**: API health shows Ollama or Azure OpenAI as unhealthy.

#### For Ollama

```powershell
# Check if Ollama is running
curl http://localhost:11434/api/tags

# If not running, start Ollama
ollama serve

# Pull required models
ollama pull qwen2.5-coder:7b
ollama pull nomic-embed-text

# Test model
ollama run qwen2.5-coder:7b "Hello"
```

**Check Ollama configuration** in `appsettings.json`:
```json
{
  "Aura": {
    "Llm": {
      "Providers": {
        "Ollama": {
          "BaseUrl": "http://localhost:11434",
          "DefaultModel": "qwen2.5-coder:7b",
          "DefaultEmbeddingModel": "nomic-embed-text"
        }
      }
    }
  }
}
```

#### For Azure OpenAI

**Symptom**: "Unauthorized" or "Resource not found" errors.

**Solutions**:

```powershell
# 1. Verify API key is set
# Check environment variable
$env:AURA_AZUREOPENAI_APIKEY

# 2. Test endpoint manually
curl -X POST "https://your-resource.cognitiveservices.azure.com/openai/deployments/gpt-4o/chat/completions?api-version=2024-02-15-preview" `
  -H "api-key: your-api-key" `
  -H "Content-Type: application/json" `
  -d '{"messages":[{"role":"user","content":"Hello"}]}'

# 3. Check configuration in appsettings.json
```

**Common configuration errors**:
- Wrong endpoint URL (should end with `/`)
- Invalid API key
- Deployment name mismatch
- API version mismatch

---

### 4. Database Connection Failed

**Symptom**: API health shows database as unhealthy.

**Solutions**:

```powershell
# 1. Check if PostgreSQL is running
Get-Service -Name postgresql*

# 2. Start PostgreSQL if stopped
Start-Service postgresql-x64-13

# 3. Test connection manually
psql -h localhost -p 5433 -U postgres -d auradb

# 4. Verify connection string in appsettings.json
```

**Connection string format**:
```json
{
  "ConnectionStrings": {
    "auradb": "Host=localhost;Port=5433;Database=auradb;Username=postgres;Password=your-password"
  }
}
```

**If database doesn't exist**:
```sql
-- Connect to PostgreSQL
psql -h localhost -p 5433 -U postgres

-- Create database
CREATE DATABASE auradb;

-- Grant permissions
GRANT ALL PRIVILEGES ON DATABASE auradb TO postgres;
```

---

### 5. MCP Tool Errors

#### "Symbol Not Found"

**Cause**: Symbol name is incorrect, ambiguous, or workspace not indexed.

**Solutions**:

```javascript
// 1. Search first to find exact name
aura_search({ query: "UserService", workspacePath: "..." })

// 2. Provide containingType for disambiguation
aura_navigate({
  operation: "callers",
  symbolName: "CreateUser",
  containingType: "UserService",  // Add this
  solutionPath: "..."
})

// 3. Ensure workspace is indexed
curl "http://localhost:5300/api/workspaces/lookup?path=..."
```

#### "Compilation Failed"

**Cause**: Code has errors preventing compilation.

**Solution**:

```javascript
// 1. Check compilation status
aura_validate({
  operation: "compilation",
  solutionPath: "...",
  includeWarnings: true
})

// 2. Fix reported errors

// 3. Retry operation
```

#### "Solution Path Required"

**Cause**: C# operation needs `solutionPath` but it wasn't provided.

**Solution**:

```javascript
// Always provide solutionPath for C# operations
aura_refactor({
  operation: "rename",
  symbolName: "Workflow",
  newName: "Story",
  solutionPath: "C:\\work\\myrepo\\App.sln"  // Add this
})
```

---

### 6. Workflow Creation Failed

**Symptom**: `POST /api/developer/workflows` returns error.

**Common causes**:

#### Repository path doesn't exist
```powershell
# Verify path exists
Test-Path "C:\work\myrepo"
```

#### Not a git repository
```powershell
# Check if .git exists
Test-Path "C:\work\myrepo\.git"

# Initialize git if needed
cd C:\work\myrepo
git init
```

#### Worktree creation failed
```powershell
# Check for existing worktree
git worktree list

# Remove stale worktree
git worktree remove .worktrees/abc123 --force

# Clean up
git worktree prune
```

---

### 7. RAG Search Returns No Results

**Symptoms**:
- `aura_search` returns `resultCount: 0`
- `aura_docs` returns no documentation

**Causes**:
- Workspace not indexed
- Search query too specific
- Content type filter too restrictive

**Solutions**:

```javascript
// 1. Broaden search query
aura_search({
  query: "auth",  // Instead of "authentication service implementation"
  workspacePath: "..."
})

// 2. Remove content type filter
aura_search({
  query: "auth",
  contentType: "all"  // Instead of "code"
})

// 3. Check if workspace is indexed
curl http://localhost:5300/api/workspaces/lookup?path=...

// 4. Re-index workspace
curl -X POST http://localhost:5300/api/workspaces/{id}/reindex
```

**Check RAG statistics**:
```powershell
curl http://localhost:5300/api/rag/stats
```

Expected output:
```json
{
  "totalChunks": 1523,
  "totalWorkspaces": 3,
  "averageChunkSize": 1200
}
```

---

### 8. Test Generation Issues

**Symptom**: `aura_generate` with `operation: "tests"` fails or produces invalid code.

**Common issues**:

#### Missing test project
```powershell
# Verify test project exists
Test-Path "tests\MyApp.Tests\MyApp.Tests.csproj"
```

#### Wrong test framework detected
```javascript
// Explicitly specify framework
aura_generate({
  operation: "tests",
  target: "UserService",
  testFramework: "xunit",  // Add this
  solutionPath: "..."
})
```

#### Compilation validation fails
```javascript
// 1. Generate without validation first
aura_generate({
  operation: "tests",
  target: "UserService",
  solutionPath: "...",
  validateCompilation: false
})

// 2. Check compilation errors manually
aura_validate({
  operation: "compilation",
  solutionPath: "..."
})

// 3. Fix errors in generated code
```

---

### 9. Extension Not Connecting to API

**Symptom**: VS Code extension shows "Disconnected" or errors.

**Solutions**:

```powershell
# 1. Check API is running
curl http://localhost:5300/health

# 2. Check extension settings in VS Code
# Settings > Extensions > Aura > API URL
# Should be: http://localhost:5300

# 3. Reload VS Code window
# Ctrl+Shift+P > "Reload Window"

# 4. Check extension logs
# Output panel > "Aura Extension"
```

---

### 10. Refactoring Doesn't Update All References

**Symptom**: After `aura_refactor` rename, some references remain unchanged.

**Causes**:
- Roslyn workspace incomplete
- String literals not updated (by design)
- Comments not updated (by design)

**Solutions**:

```javascript
// 1. Use analyze mode to see blast radius
aura_refactor({
  operation: "rename",
  symbolName: "Workflow",
  newName: "Story",
  analyze: true,  // Default
  solutionPath: "..."
})

// 2. After executing, validate compilation
aura_validate({
  operation: "compilation",
  solutionPath: "..."
})

// 3. Search for residual occurrences
aura_search({
  query: "Workflow",
  workspacePath: "..."
})

// 4. Manually update string literals/comments if needed
aura_edit({
  operation: "replace_lines",
  filePath: "...",
  startLine: 10,
  endLine: 10,
  content: '  // Updated to Story'
})
```

---

## Diagnostic Workflows

### Full System Check

```powershell
# 1. API Health
curl http://localhost:5300/health

# 2. LLM Provider
curl http://localhost:5300/health/ollama

# 3. Database
curl http://localhost:5300/health/db

# 4. RAG Service
curl http://localhost:5300/health/rag

# 5. Agent Registry
curl http://localhost:5300/health/agents

# 6. List Workspaces
curl http://localhost:5300/api/workspaces

# 7. List Agents
curl http://localhost:5300/api/agents

# 8. Check logs (production)
Get-Content "C:\ProgramData\Aura\logs\aura-$(Get-Date -Format yyyyMMdd).log" -Tail 50
```

### Workspace Troubleshooting

```powershell
# 1. Check if workspace is registered
curl "http://localhost:5300/api/workspaces/lookup?path=C%3A%5Cwork%5Cmyrepo"

# 2. If not found, onboard
curl -X POST http://localhost:5300/api/workspaces `
  -H "Content-Type: application/json" `
  -d '{"path":"C:\\work\\myrepo"}'

# 3. Wait for indexing (check progress)
curl http://localhost:5300/api/index/status

# 4. Check workspace details
curl http://localhost:5300/api/workspaces/{workspace-id}

# 5. Test search
curl -X POST http://localhost:5300/api/rag/search `
  -H "Content-Type: application/json" `
  -d '{"query":"test","workspacePath":"C:\\work\\myrepo"}'
```

### MCP Tool Debugging

```javascript
// 1. Enable preview mode to see changes without applying
aura_refactor({
  operation: "rename",
  symbolName: "Workflow",
  newName: "Story",
  solutionPath: "...",
  preview: true  // Add this
})

// 2. Use analyze mode (default for refactoring)
aura_refactor({
  operation: "rename",
  symbolName: "Workflow",
  newName: "Story",
  analyze: true,  // Default
  solutionPath: "..."
})

// 3. Validate after changes
aura_validate({
  operation: "compilation",
  solutionPath: "..."
})
```

---

## Performance Issues

### Slow Search Results

**Symptoms**: `aura_search` takes more than 5 seconds.

**Solutions**:

```javascript
// 1. Reduce result count
aura_search({
  query: "auth",
  limit: 5  // Instead of default 10
})

// 2. Filter by content type
aura_search({
  query: "auth",
  contentType: "code"  // Exclude docs/config
})

// 3. Specify workspace path
aura_search({
  query: "auth",
  workspacePath: "C:\\work\\myrepo"  // Don't search all workspaces
})
```

### Slow Refactoring Operations

**Symptoms**: `aura_refactor` takes more than 30 seconds.

**Causes**:
- Large solution with many projects
- Roslyn workspace building for first time

**Solutions**:

```javascript
// 1. Use analyze mode first (faster)
aura_refactor({
  operation: "rename",
  symbolName: "Workflow",
  newName: "Story",
  analyze: true  // Default - returns quickly
})

// 2. For execution, be patient (first run is slower)
aura_refactor({
  operation: "rename",
  symbolName: "Workflow",
  newName: "Story",
  analyze: false  // May take 20-30s for large solutions
})
```

**Note**: Subsequent operations are faster as Roslyn workspace is cached.

---

## Reporting Issues

### Information to Include

When reporting issues, please provide:

1. **Error message** (full text)
2. **Log excerpt** (last 50 lines from log file)
3. **Configuration** (anonymize sensitive data):
   - LLM provider and model
   - OS and version
   - .NET SDK version (`dotnet --version`)
4. **Repro steps** (exact commands or MCP tool calls)
5. **Expected vs. actual behavior**

### Where to Report

- **GitHub Issues**: [Create issue](https://github.com/yourusername/aura/issues/new)
- **Logs Location**:
  - Production: `C:\ProgramData\Aura\logs\`
  - Development: Console output

### Collecting Logs

```powershell
# Get recent logs (production)
Get-Content "C:\ProgramData\Aura\logs\aura-$(Get-Date -Format yyyyMMdd).log" -Tail 100 > aura-logs.txt

# Get API health dump
curl http://localhost:5300/health > health-dump.json

# Get workspace info
curl http://localhost:5300/api/workspaces > workspaces-dump.json
```

---

## FAQ

### Q: Why does `aura_search` return different results than `aura_navigate`?

**A**: `aura_search` uses semantic similarity (RAG), while `aura_navigate` uses exact symbol matching (Roslyn). Use `aura_search` for concepts, `aura_navigate` for precise code relationships.

### Q: Can I use Aura without internet connection?

**A**: Yes, if using Ollama for LLM. Azure OpenAI and OpenAI require internet.

### Q: How do I clear the RAG index?

**A**:
```powershell
curl -X DELETE http://localhost:5300/api/workspaces/{workspace-id}
```
This removes the workspace and all indexed data.

### Q: Does Aura support languages other than C#?

**A**: Yes. RAG indexing supports C#, Python, JavaScript, TypeScript, Java, Go, Rust, and more. Refactoring tools support C#, Python, and TypeScript.

### Q: How long does workspace indexing take?

**A**: Depends on codebase size:
- Small (< 100 files): 10-30 seconds
- Medium (100-1000 files): 1-5 minutes
- Large (> 1000 files): 5-15 minutes

Check progress: `curl http://localhost:5300/api/index/status`

---

## Next Steps

- **Agent Integration**: See [agents.md](agents.md) for workflows and best practices
- **Configuration**: See [configuration.md](configuration.md) for LLM and RAG settings
- **Tools Reference**: See [tools-reference.md](tools-reference.md) for complete API documentation
